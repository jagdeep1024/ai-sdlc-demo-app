namespace InvoiceApp.Domain;

public interface IInvoiceRepository
{
    IReadOnlyList<Invoice> GetAll();
    Invoice? GetById(string id);
    IReadOnlyList<Invoice> GetByIds(IEnumerable<string> ids);
    Invoice Add(Invoice invoice);
    /// <summary>Apply a complete transition to an isolated copy under the repository lock.
    /// Commit only if the callback succeeds; return a detached snapshot. Reads and Remove
    /// share the same synchronization. A missing ID throws KeyNotFoundException.</summary>
    Invoice Transition(string id, Action<Invoice> change);
    bool Remove(string id);
}
