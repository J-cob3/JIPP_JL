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

// --- 1. KONFIGURACJA SERWISÓW (Przed Build) ---

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
// Dla .NET 9 warto dodać AddOpenApi() jeśli używasz MapOpenApi()
builder.Services.AddOpenApi(); 

// Baza danych
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=jippjl.db"));

// AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

// Konfiguracja JSON (cykle)
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

// JWT Configuration
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtIssuer = jwtSection.GetValue<string>("Issuer");
var jwtAudience = jwtSection.GetValue<string>("Audience");
// ZMIANA: Pobieramy klucz z configu, żeby tokeny działały po restarcie. 
// Jeśli brak w configu - generujemy losowy (tylko dla dev).
var jwtKeyString = jwtSection.GetValue<string>("Key");
byte[] jwtKeyBytes;

if (!string.IsNullOrEmpty(jwtKeyString))
{
    jwtKeyBytes = Encoding.UTF8.GetBytes(jwtKeyString);
}
else
{
    // Fallback dla bezpieczeństwa dev
    jwtKeyBytes = RandomNumberGenerator.GetBytes(32);
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(jwtKeyBytes)
    };
});

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// Serilog Configuration
builder.Host.UseSerilog((context, services, config) =>
    config.ReadFrom.Configuration(context.Configuration)
          .ReadFrom.Services(services)
          .Enrich.FromLogContext()
          .WriteTo.Console()
          .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "Logs", "api-.log"), rollingInterval: RollingInterval.Day));

// USUNIĘTO: builder.Host.ConfigureLogging(...) - Serilog to obsługuje.

// --- 2. BUDOWANIE APLIKACJI ---
var app = builder.Build();

// Migracje przy starcie
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Obsługa błędów
app.UseExceptionHandler(handler =>
{
    handler.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerPathFeature>();
        if (feature is not null)
        {
            // Używamy app.Logger (który teraz jest Serilogiem)
            app.Logger.LogError(feature.Error, "Unhandled exception at {Path}", feature.Path);
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = "Unexpected error occurred." });
    });
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// USUNIĘTO: Zduplikowany AddDbContext po Build() - to powodowało błąd!

// --- 3. PIPELINE I ENDPOINTY ---

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .AllowAnonymous();

app.MapGet("/hello/{name}", (string name) => $"Hello, {name}!")
   .AllowAnonymous();

app.UseAuthentication();
app.UseAuthorization();

app.MapUsers(jwtKeyBytes, jwtIssuer, jwtAudience);
app.MapTasks();

// Logowanie zamknięcia aplikacji
app.Lifetime.ApplicationStopped.Register(Log.CloseAndFlush);

app.Run();

public partial class Program { }