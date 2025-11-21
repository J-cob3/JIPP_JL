﻿using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class BasicTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient _client;

    public BasicTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Tasks_RequireAuthorization()
    {
        var response = await _client.PostAsJsonAsync("/tasks", new { UserId = 1, Title = "X" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Tasks_Create_WithValidToken()
    {
        var username = $"task{Guid.NewGuid():N}";
        var (userId, token) = await RegisterAndLoginAsync(username);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/tasks")
        {
            Content = JsonContent.Create(new { UserId = userId, Title = "Task", Description = "Test", DueDate = DateTime.UtcNow })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<(int userId, string token)> RegisterAndLoginAsync(string username)
    {
        var email = $"{username}@example.com";
        const string password = "Secret1!";

        var register = await _client.PostAsJsonAsync("/auth/register", new { username, email, password });
        register.EnsureSuccessStatusCode();
        var created = await register.Content.ReadFromJsonAsync<CreatedUserDto>();

        var login = await _client.PostAsJsonAsync("/auth/login", new { username, password });
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponseDto>();

        return (created!.Id, auth!.Token);
    }

    private record CreatedUserDto(int Id, string Username, string Email);
}