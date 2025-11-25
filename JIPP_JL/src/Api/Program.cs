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

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();

// Podłączenie bazy danych
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=jippjl.db"));

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtIssuer = jwtSection.GetValue<string>("Issuer");
var jwtAudience = jwtSection.GetValue<string>("Audience");
var jwtKeyBytes = RandomNumberGenerator.GetBytes(32);

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

builder.Host.UseSerilog((context, services, config) =>
    config.ReadFrom.Configuration(context.Configuration)
          .ReadFrom.Services(services)
          .Enrich.FromLogContext()
          .WriteTo.Console()
          .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "Logs", "api-.log"), rollingInterval: RollingInterval.Day));

builder.Host.ConfigureLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseExceptionHandler(handler =>
{
    handler.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerPathFeature>();
        if (feature is not null)
        {
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

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .AllowAnonymous();

app.MapGet("/hello/{name}", (string name) => $"Hello, {name}!")
   .AllowAnonymous();

app.UseAuthentication();
app.UseAuthorization();

app.MapUsers(jwtKeyBytes, jwtIssuer, jwtAudience);
app.MapTasks();
app.Run();

app.Lifetime.ApplicationStopped.Register(Log.CloseAndFlush);

public partial class Program { }