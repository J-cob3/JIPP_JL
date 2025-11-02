using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Api.Endpoints;
using Api.Data;
using Api.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();

// Podłączenie bazy danych
var connection = builder.Configuration.GetConnectionString("Sql");
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connection));
    
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapUsers();
app.MapTasks();
app.Run();

public partial class Program { }