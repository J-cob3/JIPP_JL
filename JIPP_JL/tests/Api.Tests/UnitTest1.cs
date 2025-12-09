﻿using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Models;
using Api.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests
{
    public class BasicTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;

        public BasicTests(WebApplicationFactory<Program> factory)
        {
            _client = factory.CreateClient();
        }

        [Theory]
        [InlineData("Jan")]
        [InlineData("Anna")]
        public async Task Hello_ReturnsGreeting(string name)
        {
            var response = await _client.GetAsync($"/hello/{name}");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains(name, content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Health_ReturnsOk()
        {
            var response = await _client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("ok", content);
        }

        [Fact]
        public async Task Users_CrudLifecycle_Works()
        {
            var username = $"user{Guid.NewGuid():N}";
            var email = $"{username}@example.com";

            // Create
            var createResponse = await _client.PostAsJsonAsync("/users", new { Username = username, Email = email });
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var createdUser = await createResponse.Content.ReadFromJsonAsync<UserDto>();
            Assert.NotNull(createdUser);

            // Read All
            var users = await _client.GetFromJsonAsync<List<UserDto>>("/users");
            Assert.NotNull(users);
            Assert.Contains(users!, u => u.Id == createdUser!.Id);

            // Read One
            var userDetails = await _client.GetFromJsonAsync<UserDto>($"/users/{createdUser!.Id}");
            Assert.NotNull(userDetails);
            Assert.Equal(username, userDetails!.Username);
            Assert.Equal(email, userDetails.Email);

            // Update
            var updatedUsername = $"{username}_updated";
            var updatedEmail = $"{username}@updated.test";
            var updateResponse = await _client.PutAsJsonAsync($"/users/{createdUser.Id}", new { Username = updatedUsername, Email = updatedEmail });
            Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

            // Verify Update
            var updatedUser = await _client.GetFromJsonAsync<UserDto>($"/users/{createdUser.Id}");
            Assert.NotNull(updatedUser);
            Assert.Equal(updatedUsername, updatedUser!.Username);
            Assert.Equal(updatedEmail, updatedUser.Email);

            // Delete
            var deleteResponse = await _client.DeleteAsync($"/users/{createdUser.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            // Verify Delete
            var afterDelete = await _client.GetAsync($"/users/{createdUser.Id}");
            Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
        }

        // NOWY TEST: Sprawdza walidację e-maila
        [Fact]
        public async Task Users_Create_InvalidEmail_ReturnsBadRequest()
        {
            var invalidUser = new { Username = "Test", Email = "to-nie-jest-email" };
            var response = await _client.PostAsJsonAsync("/users", invalidUser);
            
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // NOWY TEST: Sprawdza endpoint raportowy
        [Fact]
        public async Task Reports_NewUsers_ReturnsFilteredList()
        {
            // Arrange - tworzymy usera "teraz"
            var username = $"report{Guid.NewGuid():N}";
            var email = $"{username}@test.com";
            var createResponse = await _client.PostAsJsonAsync("/users", new { Username = username, Email = email });
            createResponse.EnsureSuccessStatusCode();

            // Act - pobieramy raport z zakresu +/- 1 minuta od teraz
            var from = DateTime.UtcNow.AddMinutes(-1).ToString("o");
            var to = DateTime.UtcNow.AddMinutes(1).ToString("o");
            
            var response = await _client.GetAsync($"/reports/new-users?from={from}&to={to}");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var users = await response.Content.ReadFromJsonAsync<List<UserDto>>();
            Assert.NotNull(users);
            Assert.Contains(users, u => u.Username == username);
        }

        [Fact]
        public async Task Tasks_RequireAuthorization()
        {
            var response = await _client.PostAsJsonAsync("/tasks", new { UserId = 1, Title = "Testtitle" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Tasks_Create_WithValidToken()
        {
            var username = $"task{Guid.NewGuid():N}";
            var (userId, token) = await RegisterAndLoginAsync(username);

            using var request = new HttpRequestMessage(HttpMethod.Post, "/tasks")
            {
                Content = JsonContent.Create(new
                {
                    UserId = userId,
                    Title = "Testtitle",
                    Description = "test OK",
                    DueDate = DateTime.UtcNow
                })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var createdTask = await response.Content.ReadFromJsonAsync<UserTask>();
            Assert.NotNull(createdTask);
            Assert.Equal(userId, createdTask!.UserId);

            var userTasks = await _client.GetFromJsonAsync<List<UserTask>>($"/users/{userId}/tasks");
            Assert.NotNull(userTasks);
            Assert.Contains(userTasks!, t => t.Id == createdTask.Id);
        }

        private async Task<(int userId, string token)> RegisterAndLoginAsync(string username)
        {
            var email = $"{username}@example.com";
            const string password = "Secret1!";

            var register = await _client.PostAsJsonAsync("/auth/register", new { Username = username, Email = email, Password = password });
            register.EnsureSuccessStatusCode();
            // Tutaj API zwraca anonimowy obiekt lub UserDto, dostosuj w razie potrzeby
            var created = await register.Content.ReadFromJsonAsync<CreatedUserDto>();

            var login = await _client.PostAsJsonAsync("/auth/login", new { Username = username, Password = password });
            login.EnsureSuccessStatusCode();
            var auth = await login.Content.ReadFromJsonAsync<AuthResponseDto>();

            return (created!.Id, auth!.Token);
        }

        private record CreatedUserDto(int Id, string Username, string Email);
    }

    public class HealthEndpointTests : IAsyncLifetime
    {
        private WebApplicationFactory<Program> _factory = null!;
        private HttpClient _client = null!;

        public async Task InitializeAsync()
        {
            _factory = new WebApplicationFactory<Program>();
            _client = _factory.CreateClient();
        }

        public async Task DisposeAsync()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }

        [Fact]
        public async Task Health_ReturnsOk()
        {
            // Arrange
            var endpoint = "/health";

            // Act
            var response = await _client.GetAsync(endpoint);
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("\"status\":\"ok\"", content);
        }
    }

    // --- NOWE TESTY INTEGRACYJNE (AUTH + SEEDING) ---

    public class AuthIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;

        public AuthIntegrationTests(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Register_ValidUser_ReturnsCreated()
        {
            // Arrange
            var newUser = new
            {
                Username = "integrationUser",
                Email = "integration@test.com",
                Password = "StrongPassword123!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/auth/register", newUser);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var createdUser = await response.Content.ReadFromJsonAsync<UserDto>();
            Assert.NotNull(createdUser);
            Assert.Equal(newUser.Username, createdUser.Username);
        }

        [Fact]
        public async Task Register_DuplicateEmail_ReturnsBadRequest()
        {
            // Arrange - używamy danych użytkownika, który został dodany w Seedingu
            var duplicateUser = new
            {
                Username = "AnotherName",
                Email = "seeded@admin.com", // Ten email już istnieje w bazie (z seedingu)
                Password = "NewPassword123!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/auth/register", duplicateUser);

            // Assert - Oczekujemy błędu walidacji (duplikat)
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Login_SeededUser_And_AccessProtectedResource()
        {
            // 1. Logowanie na użytkownika z Seedingu
            var loginData = new
            {
                Username = "seededAdmin",
                Password = "SeededPassword1!"
            };

            var loginResponse = await _client.PostAsJsonAsync("/auth/login", loginData);
            
            // Jeśli tu jest błąd, to znaczy że hasło w seedingu nie zostało poprawnie zapisane
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

            var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
            Assert.NotNull(authResult);
            Assert.False(string.IsNullOrEmpty(authResult.Token));

            // 2. Dostęp do zasobu chronionego (Tasks) bez tokena (powinien być 401)
            // Zakładamy, że seedowany user ma ID = 1 (pierwszy w bazie in-memory)
            var unauthorizedResponse = await _client.GetAsync("/users/1/tasks"); 
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedResponse.StatusCode);

            // 3. Dostęp do zasobu chronionego Z tokenem
            using var request = new HttpRequestMessage(HttpMethod.Get, "/users/1/tasks");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.Token);

            var authorizedResponse = await _client.SendAsync(request);
            
            // Assert
            Assert.Equal(HttpStatusCode.OK, authorizedResponse.StatusCode);
        }
    }

    // --- KONFIGURACJA FABRYKI I SEEDING ---

    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // 1. Znajdź i usuń rejestrację prawdziwego DbContext
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // 2. Dodaj bazę In-Memory dla testów
                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseInMemoryDatabase("IntegrationTestDb");
                });

                // 3. Zbuduj ServiceProvider, aby wykonać Seeding
                var sp = services.BuildServiceProvider();

                using (var scope = sp.CreateScope())
                {
                    var scopedServices = scope.ServiceProvider;
                    var db = scopedServices.GetRequiredService<AppDbContext>();
                    var passwordHasher = scopedServices.GetRequiredService<IPasswordHasher<User>>();

                    // Upewnij się, że baza jest utworzona
                    db.Database.EnsureCreated();

                    // 4. Wykonaj Seeding użytkowników
                    SeedUsers(db, passwordHasher);
                }
            });
        }

        private void SeedUsers(AppDbContext db, IPasswordHasher<User> hasher)
        {
            if (!db.Users.Any())
            {
                var adminUser = new User
                {
                    Username = "seededAdmin",
                    Email = "seeded@admin.com",
                    CreatedAt = DateTime.UtcNow // Ważne dla spójności danych
                };
                
                // POPRAWKA: Przypisanie wygenerowanego hasha do właściwości obiektu
                adminUser.PasswordHash = hasher.HashPassword(adminUser, "SeededPassword1!");
                db.Users.Add(adminUser);
                db.SaveChanges();
            }
        }
    }
}