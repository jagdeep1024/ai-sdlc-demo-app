using System.Text.Json;
using InvoiceApp.Domain;
using Xunit;

namespace InvoiceApp.Tests;

public partial class ApprovalServiceTests
{
    private static Invoice InvoiceWith(string id, InvoiceStatus status) =>
        new() { Id = id, CustomerName = "Test School", Amount = 100m, Status = status };

    private static ApprovalService ServiceFor(params Invoice[] invoices) =>
        new(new FakeInvoiceRepository(invoices));

    [Fact]
    public void Submit_MovesDraftToPendingApproval()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.Draft);

        var result = ServiceFor(invoice).Submit("INV-1");

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
    }

    [Fact]
    public void Submit_RejectsInvoiceThatIsNotADraft()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.Approved));

        Assert.Throws<InvalidOperationException>(() => service.Submit("INV-1"));
    }

    [Fact]
    public void Approve_SetsStatusApproverAndTimestamp()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        var result = service.Approve("INV-1", "demo.approver", ApprovalRole.Manager);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.approver", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
    }

    [Fact]
    public void Approve_RejectsInvoiceThatIsStillADraft()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.Draft));

        Assert.Throws<InvalidOperationException>(() => service.Approve("INV-1", "demo.approver", ApprovalRole.Manager));
    }

    [Fact]
    public void Approve_ThrowsWhenInvoiceDoesNotExist()
    {
        var service = ServiceFor();

        Assert.Throws<KeyNotFoundException>(() => service.Approve("INV-404", "demo.approver", ApprovalRole.Manager));
    }

    [Fact]
    public void Reject_RecordsReasonAndApprover()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        var result = service.Reject("INV-1", "demo.approver", "Duplicate invoice");

        Assert.Equal(InvoiceStatus.Rejected, result.Status);
        Assert.Equal("Duplicate invoice", result.DecisionNote);
        Assert.Equal("demo.approver", result.DecidedBy);
    }

    [Fact]
    public void Reject_RequiresAReason()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        Assert.Throws<ArgumentException>(() => service.Reject("INV-1", "demo.approver", "  "));
    }

    private static Invoice Pending(decimal amount) => new()
    {
        Id = "limit-test", CustomerName = "Boundary School", Amount = amount,
        Status = InvoiceStatus.PendingApproval,
        CreatedAt = new DateTime(2026, 10, 21, 1, 2, 3, DateTimeKind.Utc),
        DecisionNote = "Preserve this note"
    };

    [Theory]
    [InlineData(ApprovalRole.Manager, 49999.99)]
    [InlineData(ApprovalRole.Manager, 50000)]
    [InlineData(ApprovalRole.Director, 49999.99)]
    [InlineData(ApprovalRole.Director, 50000)]
    [InlineData(ApprovalRole.Director, 50000.01)]
    [InlineData(ApprovalRole.Director, 499999.99)]
    [InlineData(ApprovalRole.Director, 500000)]
    public void Approve_AllowedBoundaries_RecordAuthenticatedDecision(ApprovalRole role, decimal amount)
    {
        var invoice = Pending(amount);
        var repository = new FakeInvoiceRepository(invoice);
        var before = DateTime.UtcNow;

        var result = new ApprovalService(repository).Approve(invoice.Id, "named.user", role);

        Assert.NotSame(invoice, result);
        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("named.user", result.DecidedBy);
        Assert.InRange(result.DecidedAt!.Value, before, DateTime.UtcNow);
        Assert.Equal(amount, result.Amount);
        Assert.Equal("Boundary School", result.CustomerName);
        Assert.Equal(new DateTime(2026, 10, 21, 1, 2, 3, DateTimeKind.Utc), result.CreatedAt);
        Assert.Equal("Preserve this note", result.DecisionNote);
        var approval = Assert.Single(result.Approvals);
        Assert.Equal("named.user", approval.Username);
        Assert.Equal(role, approval.Role);
        Assert.Equal(result.DecidedAt, approval.ApprovedAt);
        Assert.Equal(DateTimeKind.Utc, approval.ApprovedAt.Kind);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Theory]
    [InlineData(ApprovalRole.Manager, 50000.01, "Manager approval limit is 50,000")]
    [InlineData(ApprovalRole.Manager, 499999.99, "Manager approval limit is 50,000")]
    [InlineData(ApprovalRole.Manager, 500000, "Manager approval limit is 50,000")]
    public void Approve_OverLimit_PreservesEveryFieldWithoutWrite(ApprovalRole role, decimal amount, string reason)
    {
        var invoice = Pending(amount);
        invoice.DecidedBy = "existing metadata";
        invoice.DecidedAt = invoice.CreatedAt;
        var repository = new FakeInvoiceRepository(invoice);
        var snapshot = JsonSerializer.Serialize(invoice);

        var error = Assert.Throws<InvalidOperationException>(() =>
            new ApprovalService(repository).Approve(invoice.Id, "new.user", role));

        Assert.Contains(reason, error.Message);
        Assert.Equal(snapshot, JsonSerializer.Serialize(repository.GetById(invoice.Id)));
        Assert.Equal(0, repository.UpdateCount);
    }

    [Theory]
    [InlineData(ApprovalRole.Unknown)]
    [InlineData((ApprovalRole)99)]
    public void Approve_UnknownRole_PreservesEveryFieldWithoutWrite(ApprovalRole role)
    {
        var invoice = Pending(100m);
        var repository = new FakeInvoiceRepository(invoice);
        var snapshot = JsonSerializer.Serialize(invoice);

        var error = Assert.Throws<ArgumentException>(() =>
            new ApprovalService(repository).Approve(invoice.Id, "admin.or.unknown", role));

        Assert.Contains("Only a Manager or Director", error.Message);
        Assert.Equal(snapshot, JsonSerializer.Serialize(repository.GetById(invoice.Id)));
        Assert.Equal(0, repository.UpdateCount);
    }

    [Theory]
    [InlineData(InvoiceStatus.Draft)]
    [InlineData(InvoiceStatus.Approved)]
    [InlineData(InvoiceStatus.Rejected)]
    public void Approve_NonPending_PreservesEveryFieldWithoutWrite(InvoiceStatus status)
    {
        var invoice = Pending(100m);
        invoice.Status = status;
        invoice.DecidedBy = "previous.user";
        invoice.DecidedAt = invoice.CreatedAt;
        var repository = new FakeInvoiceRepository(invoice);
        var snapshot = JsonSerializer.Serialize(invoice);

        var error = Assert.Throws<InvalidOperationException>(() =>
            new ApprovalService(repository).Approve(invoice.Id, "new.user", ApprovalRole.Director));

        Assert.Contains("only pending invoices", error.Message);
        Assert.Equal(snapshot, JsonSerializer.Serialize(repository.GetById(invoice.Id)));
        Assert.Equal(0, repository.UpdateCount);
    }

    [Fact]
    public void Approve_MissingInvoice_DoesNotChangeOtherInvoicesOrWrite()
    {
        var invoice = Pending(100m);
        var repository = new FakeInvoiceRepository(invoice);
        var snapshot = JsonSerializer.Serialize(repository.GetAll());

        var error = Assert.Throws<KeyNotFoundException>(() =>
            new ApprovalService(repository).Approve("missing", "director", ApprovalRole.Director));

        Assert.Contains("missing was not found", error.Message);
        Assert.Equal(snapshot, JsonSerializer.Serialize(repository.GetAll()));
        Assert.Equal(0, repository.UpdateCount);
    }
}
