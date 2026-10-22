using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace InvoiceApp.Tests;

public partial class ApprovalsApiTests
{
    [Theory]
    [InlineData("demo.approver", "Manager")]
    [InlineData("demo.director", "Director")]
    public async Task Approve_HighValueSequence_UsesSessionHistoryAndDistinctDirector(string firstUser, string firstRole)
    {
        using var app = new WebApplicationFactory<Program>();
        using var first = await Login(app, firstUser);
        using var second = await Login(app, "demo.director2");
        var id = await Create(first, 500000.01m);
        var firstResponse = await first.PostAsJsonAsync($"/api/approvals/{id}/approve", new { username = "forged", role = "Director" });
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var pending = (await firstResponse.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("PendingApproval", pending["status"]!.GetValue<string>());
        Assert.Null(pending["decidedBy"]);
        Assert.Null(pending["decidedAt"]);
        var firstEntry = Assert.Single(pending["approvals"]!.AsArray())!;
        Assert.Equal(firstUser, firstEntry["username"]!.GetValue<string>());
        Assert.Equal(firstRole, firstEntry["role"]!.GetValue<string>());
        Assert.EndsWith("Z", firstEntry["approvedAt"]!.GetValue<string>());

        var secondResponse = await second.PostAsJsonAsync($"/api/approvals/{id}/approve", new { username = firstUser, role = "Manager" });

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var approved = (await secondResponse.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("Approved", approved["status"]!.GetValue<string>());
        Assert.Equal("demo.director2", approved["decidedBy"]!.GetValue<string>());
        Assert.Equal(2, approved["approvals"]!.AsArray().Count);
        Assert.Equal(firstEntry.ToJsonString(), approved["approvals"]![0]!.ToJsonString());
        Assert.Equal("demo.director2", approved["approvals"]![1]!["username"]!.GetValue<string>());
        Assert.Equal("Director", approved["approvals"]![1]!["role"]!.GetValue<string>());
        Assert.Equal(approved["decidedAt"]!.GetValue<string>(), approved["approvals"]![1]!["approvedAt"]!.GetValue<string>());
        var stored = JsonNode.Parse(await Snapshot(second, id))!;
        Assert.True(JsonNode.DeepEquals(approved, stored));
    }

    [Fact]
    public async Task Approve_SameDirectorInDifferentSessions_IsDeniedWithoutAnyStateChange()
    {
        using var app = new WebApplicationFactory<Program>();
        using var firstSession = await Login(app, "demo.director");
        using var otherSession = await Login(app, "demo.director");
        var id = await Create(firstSession, 700000m);
        (await firstSession.PostAsync($"/api/approvals/{id}/approve", null)).EnsureSuccessStatusCode();
        var before = await Snapshot(firstSession, id);

        var response = await otherSession.PostAsJsonAsync($"/api/approvals/{id}/approve", new { username = "demo.director2", role = "Director" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("different approver", await response.Content.ReadAsStringAsync());
        Assert.Equal(before, await Snapshot(firstSession, id));
    }

    [Theory]
    [InlineData("demo.approver", HttpStatusCode.Conflict, "second approver must be a different Director")]
    [InlineData("demo.admin", HttpStatusCode.Forbidden, "requires one of these roles")]
    public async Task Approve_UnauthorizedSecondRole_PreservesFullApprovalHistory(string username, HttpStatusCode status, string reason)
    {
        using var app = new WebApplicationFactory<Program>();
        using var director = await Login(app, "demo.director");
        using var attempted = await Login(app, username);
        var id = await Create(director, 700000m);
        (await director.PostAsync($"/api/approvals/{id}/approve", null)).EnsureSuccessStatusCode();
        var before = await Snapshot(director, id);

        var response = await attempted.PostAsJsonAsync($"/api/approvals/{id}/approve", new { username = "demo.director2", role = "Director" });

        Assert.Equal(status, response.StatusCode);
        Assert.Contains(reason, await response.Content.ReadAsStringAsync());
        Assert.Equal(before, await Snapshot(director, id));
    }

    [Fact]
    public async Task Approve_ConcurrentDuplicateHttpRequests_OnlyOneHistoryEntrySurvives()
    {
        using var app = new WebApplicationFactory<Program>();
        using var client = await Login(app, "demo.director");
        var id = await Create(client, 700000m);

        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => client.PostAsync($"/api/approvals/{id}/approve", null)));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Equal(11, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        var stored = JsonNode.Parse(await Snapshot(client, id))!;
        Assert.Equal("PendingApproval", stored["status"]!.GetValue<string>());
        Assert.Null(stored["decidedBy"]);
        Assert.Null(stored["decidedAt"]);
        var history = Assert.Single(stored["approvals"]!.AsArray())!;
        Assert.Equal("demo.director", history["username"]!.GetValue<string>());
        foreach (var response in responses) response.Dispose();
    }
}
