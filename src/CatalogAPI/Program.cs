using CatalogAPI.Data;
using CatalogAPI.Messaging;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var b = WebApplication.CreateBuilder(args);

b.Services.AddControllers();
b.Services.AddEndpointsApiExplorer();
b.Services.AddSwaggerGen();
b.Services.AddDbContext<CatalogDbContext>(o => o.UseSqlite(b.Configuration.GetConnectionString("Db")));
b.Services.AddMassTransit(x =>
{
    x.AddConsumer<PaymentProcessedConsumer>();
    x.UsingRabbitMq((c, q) =>
    {
        q.Host(b.Configuration["RabbitMq:Host"] ?? "localhost", h =>
        {
            h.Username("guest"); h.Password("guest");
        });
        q.ReceiveEndpoint("catalog-payment-processed", e => e.ConfigureConsumer<PaymentProcessedConsumer>(c));
    });
});

var k = b.Configuration["Jwt:Key"]!;

b.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = b.Configuration["Jwt:Issuer"],
        ValidAudience = b.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(k))
    });

b.Services.AddAuthorization();

var app = b.Build();

Directory.CreateDirectory("data");

using (var s = app.Services.CreateScope())
    s.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.EnsureCreated();

app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
public partial class Program { }
