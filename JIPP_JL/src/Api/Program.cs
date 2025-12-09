using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using System.Text;
using Api.Endpoints;
using Api.Data;
using Api.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// --- 1. KONFIGURACJA SERWISÓW ---
builder.Services.AddAuthorization();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=jippjl.db"));
builder.Services.AddAutoMapper(typeof(Program));
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);

// JWT Configuration
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKeyBytes = GetOrGenerateJwtKey(jwtSection);

// Dodaj '?? "wartosc_domyslna"', aby zmienne nigdy nie były null
var jwtIssuer = jwtSection.GetValue<string>("Issuer") ?? "https://localhost";
var jwtAudience = jwtSection.GetValue<string>("Audience") ?? "https://localhost";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(jwtKeyBytes)
    });

builder.Host.UseSerilog((context, services, config) =>
    config.ReadFrom.Configuration(context.Configuration)
          .ReadFrom.Services(services)
          .Enrich.FromLogContext()
          .WriteTo.Console()
          .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "Logs", "api-.log"), 
              rollingInterval: RollingInterval.Day));

// --- 2. BUDOWANIE APLIKACJI ---
var app = builder.Build();

// Migracje
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var feature = context.Features.Get<IExceptionHandlerPathFeature>();
    if (feature?.Error is not null)
        app.Logger.LogError(feature.Error, "Unhandled exception at {Path}", feature.Path);
    
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new { error = "Unexpected error occurred." });
}));

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// --- 3. ENDPOINTY ---
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .AllowAnonymous()
    .WithName("Health")
    .WithOpenApi();
app.MapGet("/hello/{name}", (string name) => $"Hello, {name}!")
    .AllowAnonymous();

app.UseAuthentication();
app.UseAuthorization();

app.MapUsers(jwtKeyBytes, jwtIssuer, jwtAudience);
app.MapTasks();

app.Lifetime.ApplicationStopped.Register(Log.CloseAndFlush);

app.Run();

// --- HELPER FUNCTION ---
static byte[] GetOrGenerateJwtKey(IConfigurationSection jwtSection)
{
    var keyString = jwtSection.GetValue<string>("Key");
    return !string.IsNullOrEmpty(keyString) 
        ? Encoding.UTF8.GetBytes(keyString) 
        : RandomNumberGenerator.GetBytes(32);
}

public partial class Program { }