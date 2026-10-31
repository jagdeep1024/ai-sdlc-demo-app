namespace InvoiceApp.Domain;

public sealed record ApprovalEntry(string Username, ApprovalRole Role, DateTime ApprovedAt);
