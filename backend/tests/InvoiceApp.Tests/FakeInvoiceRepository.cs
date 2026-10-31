using InvoiceApp.Domain;

namespace InvoiceApp.Tests;

internal class FakeInvoiceRepository : IInvoiceRepository
{
    private readonly List<Invoice> _invoices;
    private readonly object _sync = new();
    public FakeInvoiceRepository(params Invoice[] seed) => _invoices = seed.Select(invoice => invoice.Snapshot()).ToList();
    public int UpdateCount { get; private set; }
    public IReadOnlyList<Invoice> GetAll() { lock (_sync) return _invoices.Select(invoice => invoice.Snapshot()).ToList(); }
    public Invoice? GetById(string id) { lock (_sync) return _invoices.FirstOrDefault(invoice => invoice.Id == id)?.Snapshot(); }
    public IReadOnlyList<Invoice> GetByIds(IEnumerable<string> ids)
    {
        var wanted = ids.ToHashSet();
        lock (_sync) return _invoices.Where(invoice => wanted.Contains(invoice.Id)).Select(invoice => invoice.Snapshot()).ToList();
    }
    public Invoice Add(Invoice invoice) { lock (_sync) { _invoices.Add(invoice.Snapshot()); return invoice.Snapshot(); } }
    public Invoice Transition(string id, Action<Invoice> change)
    {
        lock (_sync)
        {
            var index = _invoices.FindIndex(invoice => invoice.Id == id);
            if (index < 0) throw new KeyNotFoundException($"Invoice {id} was not found.");
            var working = _invoices[index].Snapshot();
            change(working);
            _invoices[index] = working.Snapshot();
            UpdateCount++;
            return working.Snapshot();
        }
    }
    public bool Remove(string id) { lock (_sync) return _invoices.RemoveAll(invoice => invoice.Id == id) > 0; }
}
