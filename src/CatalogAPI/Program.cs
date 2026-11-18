using CatalogAPI.Data;
using CatalogAPI.Messaging;
using CatalogAPI.Reviews;
using CatalogAPI.Search;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Prometheus;
using System.Text;
var b = WebApplication.CreateBuilder(args);
b.Logging.ClearProviders();
b.Logging.AddJsonConsole();
b.Services.AddControllers();
b.Services.AddEndpointsApiExplorer();
b.Services.AddSwaggerGen();
b.Services.AddProblemDetails();
var postgres = b.Configuration["Database:Provider"] == "PostgreSQL";
var connection = b.Configuration.GetConnectionString("Db") ?? throw new InvalidOperationException("ConnectionStrings:Db ausente");
b.Services.AddDbContext<CatalogDbContext>(options =>
{
    if (postgres)
        options.UseNpgsql(connection);
    else
        options.UseSqlite(connection);
});
b.Services.AddSingleton<IMongoClient>(_ => new MongoClient(b.Configuration["Mongo:ConnectionString"] ?? throw new InvalidOperationException("Mongo:ConnectionString ausente")));
b.Services.AddSingleton<IReviewStore, MongoReviewStore>();
b.Services.AddStackExchangeRedisCache(o => {
    o.Configuration = b.Configuration["Redis:ConnectionString"] ?? throw new InvalidOperationException("Redis:ConnectionString ausente");
    o.InstanceName = "fcg:";
});
b.Services.AddScoped<ReviewService>();
var searchUrl = b.Configuration["Search:Url"];
b.Services.AddHttpClient<GameSearchIndex>(client =>
{
    if (!string.IsNullOrWhiteSpace(searchUrl))
        client.BaseAddress = new Uri(searchUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(10);
    var authorization = b.Configuration["Search:Authorization"];
    if (!string.IsNullOrWhiteSpace(authorization))
        client.DefaultRequestHeaders.Authorization = System.Net.Http.Headers.AuthenticationHeaderValue.Parse(authorization);
});
if (!string.IsNullOrWhiteSpace(searchUrl))
    b.Services.AddHostedService<GameIndexWorker>();
b.Services.AddMassTransit(x => {
    x.AddConsumer<PaymentProcessedConsumer>();
    x.UsingRabbitMq((c, q) => {
        q.Host(b.Configuration["RabbitMq:Host"] ?? "localhost", h => {
            h.Username(b.Configuration["RabbitMq:Username"] ?? throw new InvalidOperationException("Configuração RabbitMQ ausente"));
            h.Password(b.Configuration["RabbitMq:Password"] ?? throw new InvalidOperationException("Configuração RabbitMQ ausente"));
        });
        q.ReceiveEndpoint("catalog-payment-processed", e => {
            e.ConcurrentMessageLimit = 1;
            e.UseMessageRetry(r => r.Intervals(1000, 3000, 5000));
            e.ConfigureConsumer<PaymentProcessedConsumer>(c);
        });
    });
});
var key = b.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key ausente");
b.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
    o.TokenValidationParameters = new() {
        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer = b.Configuration["Jwt:Issuer"], ValidAudience = b.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), ClockSkew = TimeSpan.Zero
    });
b.Services.AddAuthorization();
var app = b.Build();
Directory.CreateDirectory("data");
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    if (postgres)
    {
        db.Database.OpenConnection();
        db.Database.ExecuteSqlRaw("SELECT pg_advisory_lock(42001)");
    }
    db.Database.EnsureCreated();
    if (!string.IsNullOrWhiteSpace(searchUrl))
        GameIndexWorker.Prepare(db);
    if (postgres)
        db.Database.ExecuteSqlRaw("SELECT pg_advisory_unlock(42001)");
}
app.UseRouting();
app.UseHttpMetrics();
app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));
app.MapGet("/health/ready", async (CatalogDbContext db) =>
    await db.Database.CanConnectAsync() ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503));
app.MapMetrics();
app.MapControllers();
app.Run();
public partial class Program { }
