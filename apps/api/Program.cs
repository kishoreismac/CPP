using Cpp.Api;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDb>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Cpp") ?? "Data Source=cpp-v3.db"));
builder.Services.AddScoped<IOrderRuleService, OrderRuleService>(); builder.Services.AddSingleton<IFulfillmentGateway, MockJdeFulfillmentGateway>();
builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter())); builder.Services.AddOpenApi();
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173", "http://127.0.0.1:5173"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build(); using (var s = app.Services.CreateScope()) SeedData.Seed(s.ServiceProvider.GetRequiredService<AppDb>());
app.Use(async (ctx, next) => { ctx.Response.Headers["X-Correlation-ID"] = ctx.TraceIdentifier; await next(); }); app.UseCors(); if (app.Environment.IsDevelopment()) app.MapOpenApi(); app.MapGet("/health", () => Results.Ok(new { status = "Healthy" })); app.MapControllers(); app.Run(); public partial class Program { }
