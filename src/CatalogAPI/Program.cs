using CatalogAPI.Data;
using CatalogAPI.Messaging;
using CatalogAPI.Reviews;
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
b.Services.AddDbContext<CatalogDbContext>(o => o.UseSqlite(b.Configuration.GetConnectionString("Db")));
b.Services.AddSingleton<IMongoClient>(_ => new MongoClient(b.Configuration["Mongo:ConnectionString"] ?? "mongodb://localhost:27017/?serverSelectionTimeoutMS=3000"));
b.Services.AddSingleton<IReviewStore, MongoReviewStore>();
b.Services.AddStackExchangeRedisCache(o => {
    o.Configuration = b.Configuration["Redis:ConnectionString"] ?? "localhost:6379,abortConnect=false,connectTimeout=1000,syncTimeout=1000,asyncTimeout=1000";
    o.InstanceName = "fcg:";
});
b.Services.AddScoped<ReviewService>();
b.Services.AddMassTransit(x => {
    x.AddConsumer<PaymentProcessedConsumer>();
    x.UsingRabbitMq((c, q) => {
        q.Host(b.Configuration["RabbitMq:Host"] ?? "localhost", h => {
            h.Username(b.Configuration["RabbitMq:Username"] ?? "guest");
            h.Password(b.Configuration["RabbitMq:Password"] ?? "guest");
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
    scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.EnsureCreated();
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
