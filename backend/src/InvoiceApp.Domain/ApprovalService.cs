namespace InvoiceApp.Domain;

/// <summary>Validates invoice lifecycle and approval authority inside atomic repository transitions.</summary>
public class ApprovalService
{
    private readonly IInvoiceRepository _repository;

    public ApprovalService(IInvoiceRepository repository) => _repository = repository;

    public Invoice Submit(string invoiceId) => _repository.Transition(invoiceId, invoice =>
    {
        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Invoice {invoiceId} is {invoice.Status}; only drafts can be submitted.");
        }
        invoice.Status = InvoiceStatus.PendingApproval;
    });

    public Invoice Approve(string invoiceId, string approver, ApprovalRole role) =>
        _repository.Transition(invoiceId, invoice =>
        {
            EnsurePending(invoice);
            if (string.IsNullOrWhiteSpace(approver))
            {
                throw new ArgumentException("An authenticated approver identity is required.", nameof(approver));
            }
            if (role is not (ApprovalRole.Manager or ApprovalRole.Director))
            {
                throw new ArgumentException("Only a Manager or Director may approve an invoice.", nameof(role));
            }
            if (invoice.Approvals.Any(entry => string.Equals(entry.Username, approver, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("A different approver is required; the same user cannot approve twice.");
            }

            var requiresTwo = invoice.Amount > 500_000m;
            if (requiresTwo && invoice.Approvals.Count >= 2)
            {
                throw new InvalidOperationException("This invoice already has two approvals.");
            }
            if (requiresTwo && invoice.Approvals.Count == 1 && role != ApprovalRole.Director)
            {
                throw new InvalidOperationException("The second approver must be a different Director.");
            }
            if (!requiresTwo && role == ApprovalRole.Manager && invoice.Amount > 50_000m)
            {
                throw new InvalidOperationException("Manager approval limit is 50,000; a Director is required.");
            }

            var approvedAt = DateTime.UtcNow;
            invoice.Approvals = Array.AsReadOnly(invoice.Approvals
                .Append(new ApprovalEntry(approver, role, approvedAt)).ToArray());
            if (!requiresTwo || invoice.Approvals.Count == 2)
            {
                invoice.Status = InvoiceStatus.Approved;
                invoice.DecidedBy = approver;
                invoice.DecidedAt = approvedAt;
            }
        });

    public Invoice Reject(string invoiceId, string approver, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to reject an invoice.", nameof(reason));
        }
        return _repository.Transition(invoiceId, invoice =>
        {
            EnsurePending(invoice);
            invoice.Status = InvoiceStatus.Rejected;
            invoice.DecidedBy = approver;
            invoice.DecidedAt = DateTime.UtcNow;
            invoice.DecisionNote = reason;
        });
    }

    private static void EnsurePending(Invoice invoice)
    {
        if (invoice.Status != InvoiceStatus.PendingApproval)
        {
            throw new InvalidOperationException(
                $"Invoice {invoice.Id} is {invoice.Status}; only pending invoices can be decided.");
        }
    }
}
