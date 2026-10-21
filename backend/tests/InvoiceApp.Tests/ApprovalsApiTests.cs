using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using InvoiceApp.Api.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InvoiceApp.Tests;

public class ApprovalsApiTests
{
    private static async Task<HttpClient> Login(WebApplicationFactory<Program> app, string username)
    {
        var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password = "ChangeMe123!" });
        response.EnsureSuccessStatusCode();
        var body = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body["token"]!.GetValue<string>());
        return client;
    }

    private static async Task<string> Create(HttpClient client, decimal amount, bool submit = true)
    {
        var created = await client.PostAsJsonAsync("/api/invoices", new { customerName = "API School", amount });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<string>();
        if (submit) (await client.PostAsync($"/api/invoices/{id}/submit", null)).EnsureSuccessStatusCode();
        return id;
    }

    private static async Task<string> Snapshot(HttpClient client, string id) =>
        await client.GetStringAsync($"/api/invoices/{id}");

    [Theory]
    [InlineData(null)]
    [InlineData("invalid-session")]
    public async Task Approve_WithoutValidSession_ReturnsUnauthorizedAndPreservesInvoice(string? token)
    {
        using var app = new WebApplicationFactory<Program>();
        using var manager = await Login(app, "demo.approver");
        var id = await Create(manager, 100m);
        var before = await Snapshot(manager, id);
        using var anonymous = app.CreateClient();
        if (token is not null) anonymous.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await anonymous.PostAsJsonAsync($"/api/approvals/{id}/approve", new { role = "Director" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(before, await Snapshot(manager, id));
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Approver")]
    [InlineData("Unknown")]
    public async Task Approve_DisallowedSessionRole_ReturnsForbiddenAndPreservesInvoice(string role)
    {
        using var app = new WebApplicationFactory<Program>();
        using var manager = await Login(app, "demo.approver");
        var id = await Create(manager, 100m);
        var before = await Snapshot(manager, id);
        using var disallowed = role == "Admin" ? await Login(app, "demo.admin") : app.CreateClient();
        if (role != "Admin")
        {
            var token = app.Services.GetRequiredService<SessionStore>().Create(new UserSession("unknown.user", role));
            disallowed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await disallowed.PostAsJsonAsync($"/api/approvals/{id}/approve", new { role = "Director" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("This action requires one of these roles: Manager, Director", await response.Content.ReadAsStringAsync());
        Assert.Equal(before, await Snapshot(manager, id));
    }

    [Theory]
    [InlineData("demo.approver", 50000)]
    [InlineData("demo.director", 50000.01)]
    [InlineData("demo.director", 500000)]
    [InlineData("demo.director2", 500000)]
    public async Task Approve_PermittedAccount_UsesSessionIdentityDespiteForgedBody(string username, decimal amount)
    {
        using var app = new WebApplicationFactory<Program>();
        using var client = await Login(app, username);
        var id = await Create(client, amount);

        var response = await client.PostAsJsonAsync($"/api/approvals/{id}/approve", new
        {
            username = "forged.user", approver = "forged.user", role = "Admin"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var approved = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("Approved", approved["status"]!.GetValue<string>());
        Assert.Equal(username, approved["decidedBy"]!.GetValue<string>());
        Assert.NotNull(approved["decidedAt"]);
        Assert.Equal(amount, approved["amount"]!.GetValue<decimal>());
    }

    [Theory]
    [InlineData("demo.approver", 50000.01, "Manager approval limit is 50,000")]
    [InlineData("demo.approver", 500000.01, "two distinct approvers")]
    [InlineData("demo.director", 500000.01, "two distinct approvers")]
    public async Task Approve_OverLimit_IgnoresForgedDirectorAndPreservesInvoice(string username, decimal amount, string reason)
    {
        using var app = new WebApplicationFactory<Program>();
        using var client = await Login(app, username);
        var id = await Create(client, amount);
        var before = await Snapshot(client, id);

        var response = await client.PostAsJsonAsync($"/api/approvals/{id}/approve", new { username = "demo.director2", role = "Director" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Contains(reason, error["error"]!.GetValue<string>());
        Assert.Equal(before, await Snapshot(client, id));
    }

    [Fact]
    public async Task Approve_Draft_ReturnsConflictAndPreservesInvoice()
    {
        using var app = new WebApplicationFactory<Program>();
        using var client = await Login(app, "demo.director");
        var id = await Create(client, 100m, submit: false);
        var before = await Snapshot(client, id);

        var response = await client.PostAsync($"/api/approvals/{id}/approve", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("only pending invoices", await response.Content.ReadAsStringAsync());
        Assert.Equal(before, await Snapshot(client, id));
    }

    [Fact]
    public async Task Approve_MissingInvoice_ReturnsNotFoundWithReason()
    {
        using var app = new WebApplicationFactory<Program>();
        using var client = await Login(app, "demo.director");

        var response = await client.PostAsync("/api/approvals/missing/approve", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("missing was not found", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("demo.approver")]
    [InlineData("demo.director")]
    [InlineData("demo.admin")]
    public async Task Reject_ExistingAuthorizedAccounts_PreserveRejectionRights(string username)
    {
        using var app = new WebApplicationFactory<Program>();
        using var client = await Login(app, username);
        var id = await Create(client, 100m);

        var response = await client.PostAsJsonAsync($"/api/approvals/{id}/reject", new { reason = "Duplicate invoice" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invoice = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("Rejected", invoice["status"]!.GetValue<string>());
        Assert.Equal(username, invoice["decidedBy"]!.GetValue<string>());
        Assert.Equal("Duplicate invoice", invoice["decisionNote"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("demo.admin", HttpStatusCode.NoContent)]
    [InlineData("demo.approver", HttpStatusCode.Forbidden)]
    [InlineData("demo.director", HttpStatusCode.Forbidden)]
    public async Task Delete_RemainsAdminOnly(string username, HttpStatusCode expected)
    {
        using var app = new WebApplicationFactory<Program>();
        using var client = await Login(app, username);
        var id = await Create(client, 100m, submit: false);
        var before = await Snapshot(client, id);

        var response = await client.DeleteAsync($"/api/invoices/{id}");

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.NoContent)
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/invoices/{id}")).StatusCode);
        else
            Assert.Equal(before, await Snapshot(client, id));
    }
}
