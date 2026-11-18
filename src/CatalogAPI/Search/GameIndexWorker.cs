using CatalogAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace CatalogAPI.Search;

public sealed class GameIndexWorker(IServiceScopeFactory scopes, ILogger<GameIndexWorker> logger) : BackgroundService
{
    public static void Prepare(CatalogDbContext db)
    {
        if (db.Database.IsNpgsql())
        {
            db.Database.ExecuteSqlRaw("""
                CREATE TABLE IF NOT EXISTS "GameSearchQueue" ("GameId" integer PRIMARY KEY, "Version" text NOT NULL);
                CREATE OR REPLACE FUNCTION queue_game_search() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE game_id integer;
                BEGIN
                    IF TG_OP = 'DELETE' THEN game_id := OLD."Id"; ELSE game_id := NEW."Id"; END IF;
                    INSERT INTO "GameSearchQueue" VALUES (game_id, gen_random_uuid()::text)
                    ON CONFLICT ("GameId") DO UPDATE SET "Version" = EXCLUDED."Version";
                    RETURN NULL;
                END;
                $$;
                DROP TRIGGER IF EXISTS game_search_change ON "Games";
                CREATE TRIGGER game_search_change AFTER INSERT OR UPDATE OR DELETE ON "Games"
                    FOR EACH ROW EXECUTE FUNCTION queue_game_search();
                INSERT INTO "GameSearchQueue" SELECT "Id", gen_random_uuid()::text FROM "Games" ON CONFLICT DO NOTHING;
                """);
            return;
        }

        // The triggers write to the queue in the same transaction as the catalog change.
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS GameSearchQueue (GameId INTEGER PRIMARY KEY, Version TEXT NOT NULL);
            CREATE TRIGGER IF NOT EXISTS game_search_insert AFTER INSERT ON Games BEGIN
                INSERT OR REPLACE INTO GameSearchQueue VALUES (NEW.Id, hex(randomblob(16)));
            END;
            CREATE TRIGGER IF NOT EXISTS game_search_update AFTER UPDATE ON Games BEGIN
                INSERT OR REPLACE INTO GameSearchQueue VALUES (NEW.Id, hex(randomblob(16)));
            END;
            CREATE TRIGGER IF NOT EXISTS game_search_delete AFTER DELETE ON Games BEGIN
                INSERT OR REPLACE INTO GameSearchQueue VALUES (OLD.Id, hex(randomblob(16)));
            END;
            INSERT OR IGNORE INTO GameSearchQueue SELECT Id, hex(randomblob(16)) FROM Games;
            """);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DrainAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error)
            {
                logger.LogWarning(error, "A sincronização da busca será tentada novamente.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    private async Task DrainAsync(CancellationToken token)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var index = scope.ServiceProvider.GetRequiredService<GameSearchIndex>();
        await using var transaction = db.Database.IsNpgsql()
            ? await db.Database.BeginTransactionAsync(token)
            : null;
        if (transaction is not null)
        {
            var acquired = await db.Database.SqlQueryRaw<bool>(
                """SELECT pg_try_advisory_xact_lock(42002) AS "Value" """).SingleAsync(token);
            if (!acquired)
                return;
        }

        await index.InitializeAsync(token);
        var pending = await db.Database.SqlQueryRaw<PendingGame>(
            """SELECT "GameId", "Version" FROM "GameSearchQueue" ORDER BY "GameId" LIMIT 50""").ToListAsync(token);

        foreach (var item in pending)
        {
            var game = await db.Games.AsNoTracking().Where(g => g.Id == item.GameId)
                .Select(g => new GameSearchHit(g.Id, g.Name, g.Price)).SingleOrDefaultAsync(token);
            await index.SyncAsync(item.GameId, game, token);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""DELETE FROM "GameSearchQueue" WHERE "GameId" = {item.GameId} AND "Version" = {item.Version}""", token);
        }
        if (transaction is not null)
            await transaction.CommitAsync(token);
    }

    public sealed record PendingGame(int GameId, string Version);
}
