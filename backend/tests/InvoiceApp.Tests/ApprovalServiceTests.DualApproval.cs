using System.Text.Json;
using InvoiceApp.Domain;
using Xunit;

namespace InvoiceApp.Tests;

public partial class ApprovalServiceTests
{
    [Theory]
    [InlineData(ApprovalRole.Manager)]
    [InlineData(ApprovalRole.Director)]
    public void Approve_FirstHighValueApproval_RecordsHistoryAndStaysPending(ApprovalRole role)
    {
        var invoice = Pending(500000.01m);
        var repository = new FakeInvoiceRepository(invoice);
        var before = DateTime.UtcNow;

        var result = new ApprovalService(repository).Approve(invoice.Id, "first.user", role);

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
        Assert.Null(result.DecidedBy);
        Assert.Null(result.DecidedAt);
        var entry = Assert.Single(result.Approvals);
        Assert.Equal("first.user", entry.Username);
        Assert.Equal(role, entry.Role);
        Assert.InRange(entry.ApprovedAt, before, DateTime.UtcNow);
        Assert.Equal(DateTimeKind.Utc, entry.ApprovedAt.Kind);
        Assert.Equal(1, repository.UpdateCount);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(repository.GetById(invoice.Id)));
    }

    [Theory]
    [InlineData(ApprovalRole.Manager)]
    [InlineData(ApprovalRole.Director)]
    public void Approve_SecondDistinctDirector_CompletesHighValueInvoice(ApprovalRole firstRole)
    {
        var invoice = Pending(750000m);
        var repository = new FakeInvoiceRepository(invoice);
        var first = new ApprovalService(repository).Approve(invoice.Id, "first.user", firstRole);

        var result = new ApprovalService(repository).Approve(invoice.Id, "second.director", ApprovalRole.Director);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("second.director", result.DecidedBy);
        Assert.Equal(2, result.Approvals.Count);
        Assert.Equal(first.Approvals[0], result.Approvals[0]);
        Assert.Equal("second.director", result.Approvals[1].Username);
        Assert.Equal(ApprovalRole.Director, result.Approvals[1].Role);
        Assert.Equal(result.Approvals[1].ApprovedAt, result.DecidedAt);
        Assert.Single(first.Approvals);
        Assert.Equal(InvoiceStatus.PendingApproval, first.Status);
        Assert.Null(first.DecidedBy);
        Assert.Equal(2, repository.UpdateCount);
    }

    [Theory]
    [InlineData("first.user", ApprovalRole.Director, "different approver", typeof(InvalidOperationException))]
    [InlineData("second.manager", ApprovalRole.Manager, "second approver must be a different Director", typeof(InvalidOperationException))]
    [InlineData("unknown", ApprovalRole.Unknown, "Only a Manager or Director", typeof(ArgumentException))]
    [InlineData("", ApprovalRole.Director, "identity is required", typeof(ArgumentException))]
    [InlineData("   ", ApprovalRole.Director, "identity is required", typeof(ArgumentException))]
    [InlineData(null, ApprovalRole.Director, "identity is required", typeof(ArgumentException))]
    public void Approve_InvalidSecondAttempt_PreservesFullHistoryWithoutWrite(string? username, ApprovalRole role, string reason, Type expectedType)
    {
        var invoice = Pending(500000.01m);
        var repository = new FakeInvoiceRepository(invoice);
        new ApprovalService(repository).Approve(invoice.Id, "first.user", ApprovalRole.Director);
        var before = JsonSerializer.Serialize(repository.GetById(invoice.Id));
        var writes = repository.UpdateCount;

        var error = Assert.Throws(expectedType, () =>
            new ApprovalService(repository).Approve(invoice.Id, username!, role));

        Assert.Contains(reason, error.Message);
        Assert.Equal(before, JsonSerializer.Serialize(repository.GetById(invoice.Id)));
        Assert.Equal(writes, repository.UpdateCount);
    }

    [Theory]
    [InlineData(InvoiceStatus.Draft)]
    [InlineData(InvoiceStatus.Approved)]
    [InlineData(InvoiceStatus.Rejected)]
    public void Approve_NonPendingWithHistory_PreservesHistoryAndDecisionMetadata(InvoiceStatus status)
    {
        var invoice = Pending(700000m);
        invoice.Status = status;
        invoice.Approvals = new[] { new ApprovalEntry("first.user", ApprovalRole.Director, invoice.CreatedAt) };
        invoice.DecidedBy = "previous.user";
        invoice.DecidedAt = invoice.CreatedAt;
        var repository = new FakeInvoiceRepository(invoice);
        var before = JsonSerializer.Serialize(repository.GetById(invoice.Id));

        Assert.Throws<InvalidOperationException>(() =>
            new ApprovalService(repository).Approve(invoice.Id, "second.user", ApprovalRole.Director));

        Assert.Equal(before, JsonSerializer.Serialize(repository.GetById(invoice.Id)));
        Assert.Equal(0, repository.UpdateCount);
    }

    [Fact]
    public void Approve_ThirdApproval_PreservesCompletedInvoiceWithoutWrite()
    {
        var invoice = Pending(700000m);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);
        service.Approve(invoice.Id, "first.user", ApprovalRole.Manager);
        service.Approve(invoice.Id, "second.user", ApprovalRole.Director);
        var before = JsonSerializer.Serialize(repository.GetById(invoice.Id));

        Assert.Throws<InvalidOperationException>(() => service.Approve(invoice.Id, "third.user", ApprovalRole.Director));

        Assert.Equal(before, JsonSerializer.Serialize(repository.GetById(invoice.Id)));
        Assert.Equal(2, repository.UpdateCount);
    }

    [Fact]
    public void Reject_AfterFirstApproval_PreservesHistoryAndRecordsFinalRejection()
    {
        var invoice = Pending(700000m);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);
        var first = service.Approve(invoice.Id, "first.user", ApprovalRole.Manager);

        var result = service.Reject(invoice.Id, "demo.admin", "Duplicate invoice");

        Assert.Equal(InvoiceStatus.Rejected, result.Status);
        Assert.Equal("demo.admin", result.DecidedBy);
        Assert.Equal("Duplicate invoice", result.DecisionNote);
        Assert.NotNull(result.DecidedAt);
        Assert.Equal(first.Approvals, result.Approvals);
        Assert.Equal(2, repository.UpdateCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reject_EmptyReasonAfterFirstApproval_PreservesHistoryWithoutWrite(string reason)
    {
        var invoice = Pending(700000m);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);
        service.Approve(invoice.Id, "first.user", ApprovalRole.Manager);
        var before = JsonSerializer.Serialize(repository.GetById(invoice.Id));

        Assert.Throws<ArgumentException>(() => service.Reject(invoice.Id, "admin", reason));

        Assert.Equal(before, JsonSerializer.Serialize(repository.GetById(invoice.Id)));
        Assert.Equal(1, repository.UpdateCount);
    }
}
