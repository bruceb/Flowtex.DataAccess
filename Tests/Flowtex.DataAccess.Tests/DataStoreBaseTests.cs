using Microsoft.EntityFrameworkCore;

namespace Flowtex.DataAccess.Tests;

public sealed class DataStoreBaseTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private static Widget NewWidget(string name, int stock = 0) => new() { Name = name, Stock = stock };

    // ---- Target framework sanity ------------------------------------------------------------

    [Fact]
    public void EfCoreMajorVersion_MatchesTargetFramework()
    {
        var efMajor = typeof(DbContext).Assembly.GetName().Version!.Major;

        Assert.Equal(Environment.Version.Major, efMajor);
    }

    // ---- Reads ------------------------------------------------------------------------------

    [Fact]
    public async Task Query_ReturnsUntrackedEntities()
    {
        _db.Seed(NewWidget("a"));
        var context = _db.CreateContext();
        var store = new TestDataStore(context);

        var widgets = await store.Query<Widget>().ToListAsync();

        Assert.Single(widgets);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Query_WithSelector_ProjectsColumns()
    {
        _db.Seed(NewWidget("a"), NewWidget("b"));

        var names = await _db.CreateStore().Query<Widget, string>(w => w.Name).OrderBy(n => n).ToListAsync();

        Assert.Equal(["a", "b"], names);
    }

    [Fact]
    public async Task ListAsync_WithoutShape_ReturnsAllRows()
    {
        _db.Seed(NewWidget("a"), NewWidget("b"), NewWidget("c"));

        var widgets = await _db.CreateStore().ListAsync<Widget>();

        Assert.Equal(3, widgets.Count);
    }

    [Fact]
    public async Task ListAsync_WithShape_AppliesFilterAndOrder()
    {
        _db.Seed(NewWidget("a", 1), NewWidget("b", 5), NewWidget("c", 9));

        var widgets = await _db.CreateStore().ListAsync<Widget>(q => q.Where(w => w.Stock > 2).OrderByDescending(w => w.Stock));

        Assert.Equal(["c", "b"], widgets.Select(w => w.Name));
    }

    [Fact]
    public async Task ListAsync_WithProjection_ReturnsProjectedResults()
    {
        _db.Seed(NewWidget("a", 1), NewWidget("b", 5));

        var stocks = await _db.CreateStore().ListAsync<Widget, int>(q => q.OrderBy(w => w.Stock).Select(w => w.Stock));

        Assert.Equal([1, 5], stocks);
    }

    [Fact]
    public async Task FindAsync_ExistingKey_ReturnsEntity()
    {
        _db.Seed(NewWidget("a"));
        var id = _db.ReadAll().Single().Id;

        var widget = await _db.CreateStore().FindAsync<Widget>([id]);

        Assert.NotNull(widget);
        Assert.Equal("a", widget.Name);
    }

    [Fact]
    public async Task FindAsync_MissingKey_ReturnsNull()
    {
        var widget = await _db.CreateStore().FindAsync<Widget>([12345]);

        Assert.Null(widget);
    }

    [Fact]
    public async Task QueryTracked_ModifiedEntity_IsPersistedBySaveChanges()
    {
        _db.Seed(NewWidget("a", 1));
        var store = _db.CreateStore();

        var widget = await store.QueryTracked<Widget>().SingleAsync();
        widget.Stock = 42;
        await store.SaveChangesAsync();

        Assert.Equal(42, _db.ReadAll().Single().Stock);
    }

    // ---- Instance-based writes --------------------------------------------------------------

    [Fact]
    public async Task AddAsync_IsNotWrittenUntilSaved()
    {
        var store = _db.CreateStore();

        var handle = await store.AddAsync(NewWidget("a"));
        Assert.Empty(_db.ReadAll());

        var written = await handle.SaveAsync();

        Assert.Equal(1, written);
        Assert.Single(_db.ReadAll());
    }

    [Fact]
    public async Task AddRangeAsync_SavesAllEntities()
    {
        var handle = await _db.CreateStore().AddRangeAsync([NewWidget("a"), NewWidget("b")]);

        await handle.SaveAsync();

        Assert.Equal(2, _db.ReadAll().Count);
    }

    [Fact]
    public async Task SaveHandle_SavesAllPendingChangesOnTheStore()
    {
        var store = _db.CreateStore();

        await store.AddAsync(NewWidget("staged first"));
        var handle = await store.AddAsync(NewWidget("staged second"));
        await handle.SaveAsync();

        Assert.Equal(2, _db.ReadAll().Count);
    }

    [Fact]
    public async Task Update_DetachedEntity_IsPersisted()
    {
        _db.Seed(NewWidget("a", 1));
        var detached = _db.ReadAll().Single();
        detached.Name = "renamed";

        await _db.CreateStore().Update(detached).SaveAsync();

        Assert.Equal("renamed", _db.ReadAll().Single().Name);
    }

    [Fact]
    public async Task Remove_DeletesEntity()
    {
        _db.Seed(NewWidget("a"));
        var store = _db.CreateStore();
        var widget = await store.QueryTracked<Widget>().SingleAsync();

        await store.Remove(widget).SaveAsync();

        Assert.Empty(_db.ReadAll());
    }

    // ---- Set-based server operations -------------------------------------------------------

    [Fact]
    public async Task ExecuteDeleteAsync_DeletesMatchingRows()
    {
        _db.Seed(NewWidget("a", 0), NewWidget("b", 0), NewWidget("c", 3));

        var deleted = await _db.CreateStore().ExecuteDeleteAsync<Widget>(w => w.Stock == 0);

        Assert.Equal(2, deleted);
        Assert.Equal("c", _db.ReadAll().Single().Name);
    }

    [Fact]
    public async Task ExecuteDeleteAsync_ThenSavingModifiedTrackedCopy_ThrowsConcurrencyException()
    {
        // Documents the remark on IDataStore.ExecuteDeleteAsync: the tracker still holds the row.
        _db.Seed(NewWidget("a", 1));
        var store = _db.CreateStore();
        var widget = await store.QueryTracked<Widget>().SingleAsync();

        await store.ExecuteDeleteAsync<Widget>(w => true);
        widget.Stock = 2;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => store.SaveChangesAsync());
    }

    [Fact]
    public async Task ExecuteUpdateAsync_SetConst_WritesConstant()
    {
        _db.Seed(NewWidget("a", 1), NewWidget("b", 2));

        var updated = await _db.CreateStore().ExecuteUpdateAsync<Widget>(w => w.Name == "a", b => b.SetConst(w => w.Stock, 99));

        Assert.Equal(1, updated);
        Assert.Equal([99, 2], _db.ReadAll().Select(w => w.Stock));
    }

    [Fact]
    public async Task ExecuteUpdateAsync_Set_ComputesFromRowAndChainsSetters()
    {
        _db.Seed(NewWidget("a", 10));

        await _db.CreateStore().ExecuteUpdateAsync<Widget>(
            w => true,
            b => b.Set(w => w.Stock, w => w.Stock - 3).SetConst(w => w.Name, "sold"));

        var widget = _db.ReadAll().Single();
        Assert.Equal(7, widget.Stock);
        Assert.Equal("sold", widget.Name);
    }

    [Fact]
    public async Task ExecuteUpdateAsync_WithoutSetters_Throws()
    {
        var store = _db.CreateStore();

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ExecuteUpdateAsync<Widget>(w => true, _ => { }));
    }

    // ---- Transactions -----------------------------------------------------------------------

    [Fact]
    public async Task InTransactionAsync_Success_CommitsAndReturnsResult()
    {
        var store = _db.CreateStore();

        var name = await store.InTransactionAsync(async s =>
        {
            await (await s.AddAsync(NewWidget("a"))).SaveAsync();
            await s.ExecuteUpdateAsync<Widget>(w => true, b => b.SetConst(w => w.Name, "committed"));
            return "done";
        });

        Assert.Equal("done", name);
        Assert.Equal("committed", _db.ReadAll().Single().Name);
    }

    [Fact]
    public async Task InTransactionAsync_Exception_RollsBackSavedChanges()
    {
        var store = _db.CreateStore();

        await Assert.ThrowsAsync<ApplicationException>(() => store.InTransactionAsync<int>(async s =>
        {
            await (await s.AddAsync(NewWidget("a"))).SaveAsync();
            throw new ApplicationException("fail after save");
        }));

        Assert.Empty(_db.ReadAll());
    }

    [Fact]
    public async Task InTransactionAsync_Exception_ClearsChangeTracker()
    {
        // The rollback undoes the database write; the tracker must not keep describing the
        // rolled-back row as saved, or the store cannot be reused.
        var context = _db.CreateContext();
        var store = new TestDataStore(context);

        await Assert.ThrowsAsync<ApplicationException>(() => store.InTransactionAsync<int>(async s =>
        {
            await (await s.AddAsync(NewWidget("a"))).SaveAsync();
            throw new ApplicationException("fail after save");
        }));

        Assert.Empty(_db.ReadAll());
        Assert.Empty(context.ChangeTracker.Entries());

        await (await store.AddAsync(NewWidget("b"))).SaveAsync();
        Assert.Equal("b", _db.ReadAll().Single().Name);
    }

    [Fact]
    public async Task InTransactionAsync_RetriedAttempt_DoesNotKeepEntitiesFromFailedAttempt()
    {
        var store = _db.CreateStore(retryOnFailure: true);
        var attempts = 0;

        await store.InTransactionAsync(async s =>
        {
            attempts++;
            await (await s.AddAsync(NewWidget($"attempt {attempts}"))).SaveAsync();
            if (attempts == 1) throw new TransientTestException();
            return attempts;
        });

        Assert.Equal(2, attempts);
        Assert.Equal("attempt 2", _db.ReadAll().Single().Name);
    }

    [Fact]
    public async Task InTransactionAsync_RetriedAttempt_ReloadsTrackedEntitiesFromDatabase()
    {
        _db.Seed(NewWidget("a", 10));
        var store = _db.CreateStore(retryOnFailure: true);
        var attempts = 0;

        await store.InTransactionAsync(async s =>
        {
            attempts++;
            var widget = await s.QueryTracked<Widget>().SingleAsync();
            widget.Stock -= 1;
            await s.SaveChangesAsync();
            if (attempts == 1) throw new TransientTestException();
            return attempts;
        });

        Assert.Equal(2, attempts);
        Assert.Equal(9, _db.ReadAll().Single().Stock);
    }

    [Fact]
    public async Task InTransactionAsync_RetryingStrategy_FirstAttemptStartsWithEmptyTracker()
    {
        // Every attempt must see the same tracker state; otherwise the first attempt could save
        // pre-call entities that a retry (which starts empty) silently would not.
        _db.Seed(NewWidget("a"));
        var context = _db.CreateContext(retryOnFailure: true);
        var store = new TestDataStore(context);
        await store.QueryTracked<Widget>().SingleAsync();
        var trackedPerAttempt = new List<int>();

        await store.InTransactionAsync(s =>
        {
            trackedPerAttempt.Add(context.ChangeTracker.Entries().Count());
            if (trackedPerAttempt.Count == 1) throw new TransientTestException();
            return Task.FromResult(0);
        });

        Assert.Equal([0, 0], trackedPerAttempt);
    }

    [Fact]
    public async Task InTransactionAsync_RetryingStrategyWithPendingChanges_ThrowsWithoutRunningWork()
    {
        _db.Seed(NewWidget("a", 1));
        var context = _db.CreateContext(retryOnFailure: true);
        var store = new TestDataStore(context);
        var widget = await store.QueryTracked<Widget>().SingleAsync();
        widget.Stock = 2;
        var ran = false;

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.InTransactionAsync(
            _ => { ran = true; return Task.FromResult(0); }));

        Assert.False(ran);
        Assert.Single(context.ChangeTracker.Entries<Widget>(), e => e.State == EntityState.Modified);
    }

    [Fact]
    public async Task InTransactionAsync_ExceptionBeforeAnySave_StillDiscardsPendingChanges()
    {
        // A failed unit of work discards all tracked state, so a later save cannot persist half of it.
        _db.Seed(NewWidget("a", 1));
        var context = _db.CreateContext();
        var store = new TestDataStore(context);
        var widget = await store.QueryTracked<Widget>().SingleAsync();
        widget.Stock = 2;

        await Assert.ThrowsAsync<ApplicationException>(() => store.InTransactionAsync<int>(
            _ => throw new ApplicationException("validation failed")));

        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Equal(0, await store.SaveChangesAsync());
        Assert.Equal(1, _db.ReadAll().Single().Stock);
    }

    [Fact]
    public async Task InTransactionAsync_CancelledToken_DoesNotRunWork()
    {
        var store = _db.CreateStore(retryOnFailure: true);
        var ran = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.InTransactionAsync(
            _ => { ran = true; return Task.FromResult(0); },
            new CancellationToken(canceled: true)));

        Assert.False(ran);
    }

    [Fact]
    public async Task AddAsync_CancelledToken_DoesNotStageEntity()
    {
        var context = _db.CreateContext();
        var store = new TestDataStore(context);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await store.AddAsync(NewWidget("a"), new CancellationToken(canceled: true)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await store.AddRangeAsync([NewWidget("b")], new CancellationToken(canceled: true)));

        Assert.Empty(context.ChangeTracker.Entries());
    }
}
