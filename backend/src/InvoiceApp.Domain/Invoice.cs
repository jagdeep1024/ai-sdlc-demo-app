namespace InvoiceApp.Domain;

public class Invoice
{
    public string Id { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }
    public IReadOnlyList<ApprovalEntry> Approvals { get; set; } = Array.Empty<ApprovalEntry>();

    // Repository boundaries copy the mutable envelope; history entries are immutable records.
    public Invoice Snapshot() => new()
    {
        Id = Id, CustomerName = CustomerName, Amount = Amount, Status = Status,
        CreatedAt = CreatedAt, DecidedBy = DecidedBy, DecidedAt = DecidedAt,
        DecisionNote = DecisionNote, Approvals = Array.AsReadOnly(Approvals.ToArray())
    };
}
