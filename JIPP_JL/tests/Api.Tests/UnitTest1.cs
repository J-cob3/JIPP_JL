﻿using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class BasicTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public BasicTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
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
        // Zaktualizowano: testujemy tylko /health, bo tak jest w Program.cs
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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