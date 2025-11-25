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
        var response = await _client.GetAsync("/api/v1/health");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            response = await _client.GetAsync("/health");
        }

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Users_CrudLifecycle_Works()
    {
        var username = $"user{Guid.NewGuid():N}";
        var email = $"{username}@example.com";

        var createResponse = await _client.PostAsJsonAsync("/users", new { Username = username, Email = email });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(createdUser);

        var users = await _client.GetFromJsonAsync<List<UserDto>>("/users");
        Assert.NotNull(users);
        Assert.Contains(users!, u => u.Id == createdUser!.Id);

        var userDetails = await _client.GetFromJsonAsync<UserDto>($"/users/{createdUser!.Id}");
        Assert.NotNull(userDetails);
        Assert.Equal(username, userDetails!.Username);
        Assert.Equal(email, userDetails.Email);

        var updatedUsername = $"{username}_updated";
        var updatedEmail = $"{username}@updated.test";
        var updateResponse = await _client.PutAsJsonAsync($"/users/{createdUser.Id}", new { Username = updatedUsername, Email = updatedEmail });
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        var updatedUser = await _client.GetFromJsonAsync<UserDto>($"/users/{createdUser.Id}");
        Assert.NotNull(updatedUser);
        Assert.Equal(updatedUsername, updatedUser!.Username);
        Assert.Equal(updatedEmail, updatedUser.Email);

        var deleteResponse = await _client.DeleteAsync($"/users/{createdUser.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var afterDelete = await _client.GetAsync($"/users/{createdUser.Id}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
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
        var created = await register.Content.ReadFromJsonAsync<CreatedUserDto>();

        var login = await _client.PostAsJsonAsync("/auth/login", new { Username = username, Password = password });
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponseDto>();

        return (created!.Id, auth!.Token);
    }

    private record CreatedUserDto(int Id, string Username, string Email);
}