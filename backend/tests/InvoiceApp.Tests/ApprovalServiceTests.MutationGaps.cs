using System.Text.Json;
using InvoiceApp.Domain;
using Xunit;

namespace InvoiceApp.Tests;

public partial class ApprovalServiceTests
{
    [Theory]
    [InlineData(InvoiceStatus.PendingApproval)]
    [InlineData(InvoiceStatus.Approved)]
    [InlineData(InvoiceStatus.Rejected)]
    public void Submit_NonDraft_ExplainsFailureAndPreservesEveryField(InvoiceStatus status)
    {
        var invoice = InvoiceWith("submit-state", status);
        var repository = new FakeInvoiceRepository(invoice);
        var before = JsonSerializer.Serialize(repository.GetById(invoice.Id));

        var error = Assert.Throws<InvalidOperationException>(() =>
            new ApprovalService(repository).Submit(invoice.Id));

        Assert.Contains(invoice.Id, error.Message);
        Assert.Contains("only drafts can be submitted", error.Message);
        Assert.Equal(before, JsonSerializer.Serialize(repository.GetById(invoice.Id)));
        Assert.Equal(0, repository.UpdateCount);
    }

    [Theory]
    [InlineData(InvoiceStatus.Draft)]
    [InlineData(InvoiceStatus.Approved)]
    [InlineData(InvoiceStatus.Rejected)]
    public void Reject_NonPending_DoesNotOverwriteLifecycleDecision(InvoiceStatus status)
    {
        var invoice = InvoiceWith("reject-state", InvoiceStatus.Draft);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);
        if (status != InvoiceStatus.Draft)
        {
            service.Submit(invoice.Id);
            if (status == InvoiceStatus.Approved)
                service.Approve(invoice.Id, "original.director", ApprovalRole.Director);
            else
                service.Reject(invoice.Id, "original.admin", "Original rejection");
        }
        var before = JsonSerializer.Serialize(repository.GetById(invoice.Id));
        var writes = repository.UpdateCount;

        var error = Assert.Throws<InvalidOperationException>(() =>
            service.Reject(invoice.Id, "another.admin", "Replacement decision"));

        Assert.Contains("only pending invoices", error.Message);
        Assert.Equal(before, JsonSerializer.Serialize(repository.GetById(invoice.Id)));
        Assert.Equal(writes, repository.UpdateCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Reject_MissingReason_ExplainsFailureAndPreservesApprovalHistory(string? reason)
    {
        var invoice = Pending(700000m);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);
        service.Approve(invoice.Id, "first.manager", ApprovalRole.Manager);
        var before = JsonSerializer.Serialize(repository.GetById(invoice.Id));
        var writes = repository.UpdateCount;

        var error = Assert.Throws<ArgumentException>(() =>
            service.Reject(invoice.Id, "another.admin", reason!));

        Assert.Contains("A reason is required to reject an invoice", error.Message);
        Assert.Equal(before, JsonSerializer.Serialize(repository.GetById(invoice.Id)));
        Assert.Equal(writes, repository.UpdateCount);
    }
}
