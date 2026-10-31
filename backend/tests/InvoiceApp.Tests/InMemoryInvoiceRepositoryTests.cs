using System.Text.Json;
using InvoiceApp.Api;
using InvoiceApp.Domain;
using Xunit;

namespace InvoiceApp.Tests;

public class InMemoryInvoiceRepositoryTests
{
    private static Invoice Create(InMemoryInvoiceRepository repository) => repository.Add(new Invoice
    {
        CustomerName = "Concurrency School", Amount = 700000m, Status = InvoiceStatus.PendingApproval
    });

    private static string State(Invoice invoice) => JsonSerializer.Serialize(invoice);

    // All operations are dispatched behind one gate to contend on the same repository.
    private static async Task<Exception?[]> Race(params Action[] actions)
    {
        using var gate = new ManualResetEventSlim();
        using var ready = new CountdownEvent(actions.Length);
        var tasks = actions.Select(action => Task.Factory.StartNew(() =>
        {
            ready.Signal();
            gate.Wait();
            try { action(); return null; }
            catch (Exception error) { return error; }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        var allReady = ready.Wait(TimeSpan.FromSeconds(10));
        gate.Set();
        Assert.True(allReady, "All workers should be ready before the race begins.");
        return await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Transition_ConcurrentDuplicateDirectors_RecordExactlyOneApproval()
    {
        var repository = new InMemoryInvoiceRepository();
        var invoice = Create(repository);
        var outcomes = await Race(Enumerable.Range(0, 16).Select(_ => (Action)(() =>
            new ApprovalService(repository).Approve(invoice.Id, "same.director", ApprovalRole.Director))).ToArray());

        Assert.Single(outcomes, error => error is null);
        Assert.All(outcomes.Where(error => error is not null), error => Assert.IsType<InvalidOperationException>(error));
        var result = repository.GetById(invoice.Id)!;
        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
        Assert.Null(result.DecidedBy);
        Assert.Null(result.DecidedAt);
        Assert.Equal("same.director", Assert.Single(result.Approvals).Username);
    }

    [Fact]
    public async Task Transition_ConcurrentDistinctDirectors_RecordExactlyTwoAndApprove()
    {
        var repository = new InMemoryInvoiceRepository();
        var invoice = Create(repository);
        var outcomes = await Race(Enumerable.Range(0, 16).Select(index => (Action)(() =>
            new ApprovalService(repository).Approve(invoice.Id, $"director.{index}", ApprovalRole.Director))).ToArray());

        Assert.Equal(2, outcomes.Count(error => error is null));
        Assert.All(outcomes.Where(error => error is not null), error => Assert.IsType<InvalidOperationException>(error));
        var result = repository.GetById(invoice.Id)!;
        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal(2, result.Approvals.Count);
        Assert.Equal(2, result.Approvals.Select(entry => entry.Username).Distinct().Count());
        Assert.Equal(result.Approvals[1].Username, result.DecidedBy);
        Assert.Equal(result.Approvals[1].ApprovedAt, result.DecidedAt);
    }

    [Fact]
    public async Task Transition_ApprovalRacingRejection_HasOneConsistentFinalDecision()
    {
        var repository = new InMemoryInvoiceRepository();
        var invoice = Create(repository);
        new ApprovalService(repository).Approve(invoice.Id, "first.manager", ApprovalRole.Manager);
        var outcomes = await Race(
            () => new ApprovalService(repository).Approve(invoice.Id, "second.director", ApprovalRole.Director),
            () => new ApprovalService(repository).Reject(invoice.Id, "admin", "Duplicate"));

        Assert.Single(outcomes, error => error is null);
        Assert.IsType<InvalidOperationException>(Assert.Single(outcomes, error => error is not null));
        var result = repository.GetById(invoice.Id)!;
        if (result.Status == InvoiceStatus.Approved)
        {
            Assert.Equal(2, result.Approvals.Count);
            Assert.Equal("second.director", result.DecidedBy);
            Assert.Equal(result.Approvals[1].ApprovedAt, result.DecidedAt);
            Assert.Null(result.DecisionNote);
        }
        else
        {
            Assert.Equal(InvoiceStatus.Rejected, result.Status);
            Assert.Single(result.Approvals);
            Assert.Equal("admin", result.DecidedBy);
            Assert.Equal("Duplicate", result.DecisionNote);
        }
    }

    [Fact]
    public async Task Transition_ApprovalRacingDelete_CannotResurrectInvoice()
    {
        var repository = new InMemoryInvoiceRepository();
        var invoice = Create(repository);
        var outcomes = await Race(
            () => new ApprovalService(repository).Approve(invoice.Id, "first.director", ApprovalRole.Director),
            () => Assert.True(repository.Remove(invoice.Id)));

        Assert.Null(outcomes[1]);
        if (outcomes[0] is not null) Assert.IsType<KeyNotFoundException>(outcomes[0]);
        Assert.Null(repository.GetById(invoice.Id));
        Assert.DoesNotContain(repository.GetAll(), stored => stored.Id == invoice.Id);
    }

    [Fact]
    public async Task Transition_ReadsDuringApprovals_SeeOnlyCompleteSnapshots()
    {
        var repository = new InMemoryInvoiceRepository();
        var invoice = Create(repository);
        var snapshots = new List<Invoice>();
        var outcomes = await Race(
            () => { for (var index = 0; index < 200; index++) snapshots.Add(repository.GetById(invoice.Id)!); },
            () => new ApprovalService(repository).Approve(invoice.Id, "first.director", ApprovalRole.Director),
            () => new ApprovalService(repository).Approve(invoice.Id, "second.director", ApprovalRole.Director));

        Assert.All(outcomes, error => Assert.Null(error));
        Assert.Equal(200, snapshots.Count);
        foreach (var snapshot in snapshots)
        {
            if (snapshot.Approvals.Count < 2)
            {
                Assert.Equal(InvoiceStatus.PendingApproval, snapshot.Status);
                Assert.Null(snapshot.DecidedBy);
                Assert.Null(snapshot.DecidedAt);
            }
            else
            {
                Assert.Equal(2, snapshot.Approvals.Count);
                Assert.Equal(InvoiceStatus.Approved, snapshot.Status);
                Assert.Equal(snapshot.Approvals[1].Username, snapshot.DecidedBy);
                Assert.Equal(snapshot.Approvals[1].ApprovedAt, snapshot.DecidedAt);
            }
            Assert.NotEmpty(State(snapshot));
        }
        Assert.Equal(InvoiceStatus.Approved, repository.GetById(invoice.Id)!.Status);
    }

    [Fact]
    public void Snapshots_AddReadListAndTransition_DoNotLeakMutableStoredState()
    {
        var repository = new InMemoryInvoiceRepository();
        var supplied = new Invoice { Id = "detached", Amount = 700000m, Status = InvoiceStatus.PendingApproval };
        var added = repository.Add(supplied);
        supplied.Amount = 1;
        added.Amount = 2;
        var first = new ApprovalService(repository).Approve("detached", "first.user", ApprovalRole.Manager);
        var expected = State(first);
        var all = repository.GetAll().Single(invoice => invoice.Id == "detached");
        var byIds = Assert.Single(repository.GetByIds(new[] { "detached" }));
        var one = repository.GetById("detached")!;
        foreach (var detached in new[] { first, all, byIds, one })
        {
            detached.Amount = 3;
            detached.Status = InvoiceStatus.Approved;
            detached.Approvals = Array.Empty<ApprovalEntry>();
        }

        Assert.Equal(expected, State(repository.GetById("detached")!));
        Assert.Equal(700000m, repository.GetById("detached")!.Amount);
    }

    [Fact]
    public void Transition_FailedCallback_DiscardsEvenPartialWorkingCopyChanges()
    {
        var repository = new InMemoryInvoiceRepository();
        var invoice = Create(repository);
        var expected = State(invoice);

        Assert.Throws<InvalidOperationException>(() => repository.Transition(invoice.Id, working =>
        {
            working.Status = InvoiceStatus.Approved;
            working.Approvals = new[] { new ApprovalEntry("user", ApprovalRole.Manager, DateTime.UtcNow) };
            throw new InvalidOperationException("simulated failure");
        }));

        Assert.Equal(expected, State(repository.GetById(invoice.Id)!));
    }

    [Fact]
    public void GetById_LegacyApprovedSeed_KeepsDecisionWithoutInventingHistory()
    {
        var repository = new InMemoryInvoiceRepository();
        var legacy = repository.GetById("INV-1003")!;
        var before = State(legacy);

        Assert.Throws<InvalidOperationException>(() =>
            new ApprovalService(repository).Approve(legacy.Id, "new.director", ApprovalRole.Director));

        Assert.Equal(InvoiceStatus.Approved, legacy.Status);
        Assert.Equal("demo.approver", legacy.DecidedBy);
        Assert.NotNull(legacy.DecidedAt);
        Assert.Empty(legacy.Approvals);
        Assert.Equal(before, State(repository.GetById(legacy.Id)!));
    }
    [Fact]
    public void Snapshots_HistoryArraysAndCollectionCasts_CannotMutateStoredEntries()
    {
        var repository = new InMemoryInvoiceRepository();
        var entry = new ApprovalEntry("first.director", ApprovalRole.Director, DateTime.UtcNow);
        var suppliedHistory = new[] { entry };
        var added = repository.Add(new Invoice
        {
            Id = "history-isolation", Amount = 700000m,
            Status = InvoiceStatus.PendingApproval, Approvals = suppliedHistory
        });
        var expected = State(added);

        suppliedHistory[0] = new ApprovalEntry("forged", ApprovalRole.Manager, DateTime.UtcNow);
        var collection = Assert.IsAssignableFrom<IList<ApprovalEntry>>(added.Approvals);
        Assert.Throws<NotSupportedException>(() => collection[0] = suppliedHistory[0]);
        Assert.Throws<NotSupportedException>(() => collection.Clear());

        Assert.Equal(entry, Assert.Single(repository.GetById(added.Id)!.Approvals));
        Assert.Equal(expected, State(repository.GetById(added.Id)!));
    }

}
