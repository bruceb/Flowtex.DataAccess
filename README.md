# Flowtex.DataAccess

A modern, streamlined data access library for Entity Framework Core that provides a clean alternative to the traditional Repository and Unit of Work patterns. This library simplifies data operations while maintaining performance and testability.

## What This Library Does

Flowtex.DataAccess provides a unified interface for data access operations through a set of core abstractions:

- **`IReadStore`** - Read-only operations with optimized no-tracking queries, including `FindAsync<T>` for identity-map-cached key lookups
- **`IDataStore`** - Full CRUD operations including reads, writes, and transactions
- **`ISaveHandle`** - Deferred save handle returned by single-mutation methods; calling `SaveAsync()` flushes **all** pending context changes, not only the staged operation — see [ISaveHandle notes](#isavehandle-notes) below
- **`IUpdateBuilder<T>`** - Fluent API for efficient bulk update operations

The library is built around Entity Framework Core but abstracts away the complexity while preserving all the power and performance benefits.

## Why Use This Instead of Repository and Unit of Work Patterns?

### Problems with Traditional Patterns

**Repository Pattern Issues:**
- Creates unnecessary abstraction layers over already-abstracted EF Core
- Often leads to "leaky abstractions" where EF-specific code bleeds through
- Requires extensive mocking for unit testing
- Can hide EF Core's powerful query capabilities behind generic interfaces
- Often results in N+1 query problems due to oversimplified interfaces

**Unit of Work Pattern Issues:**
- EF Core's `DbContext` already implements Unit of Work
- Creates redundant transaction management
- Can lead to confusing nested transaction scenarios
- Adds complexity without meaningful benefits

### Flowtex.DataAccess Advantages

**1. Direct EF Core Integration** *(no abstraction overhead)*
- Leverages EF Core's native capabilities instead of hiding them
- No performance penalties from unnecessary abstractions
- Access to full LINQ query power through `IQueryable<T>`

**2. Explicit Save Control** *(Deferred Execution pattern)*
- `ISaveHandle` provides clear control over when data is persisted
- Enables batching multiple mutations into a single database round-trip
- Eliminates accidental implicit saves

**3. Performance Optimized** *(Fluent Builder for bulk ops)*
- No-tracking queries by default for reads (`IReadStore`)
- Tracked queries available when needed (`QueryTracked<T>`)
- Bulk operations via `ExecuteUpdateAsync`/`ExecuteDeleteAsync` push a single `UPDATE`/`DELETE` statement to the database — no entities loaded into memory

**4. Clean Testing** *(Command-Query Separation)*
- `IReadStore` and `IDataStore` are easily mockable
- Services declare only the interface they need — a read-only service takes `IReadStore`; a write service takes `IDataStore`
- No complex repository hierarchies to maintain

**5. Minimal Boilerplate** *(Template Method pattern)*
- `DataStoreBase` implements all logic; concrete stores override a single `protected abstract DbContext Context { get; }` property
- Built-in transaction support via `InTransactionAsync` uses EF Core's execution strategy for automatic retry and rollback
- Consistent API across all entity types

## Installation

Two packages are published to match clean architecture layering:

| Package | Use in |
|---|---|
| `Flowtex.DataAccess.Abstractions` | Application / Domain layer — **no EF Core dependency** |
| `Flowtex.DataAccess` | Infrastructure layer — includes the `DataStoreBase` EF Core implementation |

```bash
# Application/domain project (IDataStore, IReadStore, ISaveHandle, IUpdateBuilder<T>)
dotnet add package Flowtex.DataAccess.Abstractions

# Infrastructure project (DataStoreBase + EF Core)
dotnet add package Flowtex.DataAccess
```

## Examples

### Basic Setup

```csharp
// Implementation example (in your Infrastructure layer)
// Only Context needs to be overridden — DataStoreBase calls Context.Set<T>() internally.
public class AppDataStore : DataStoreBase
{
    private readonly AppDbContext _context;
    
    public AppDataStore(AppDbContext context)
    {
        _context = context;
    }
    
    protected override DbContext Context => _context;
}

// Dependency injection setup
services.AddScoped<IDataStore, AppDataStore>();
services.AddScoped<IReadStore>(provider => provider.GetRequiredService<IDataStore>());
```

### Reading Data

```csharp
public class ProductQueryService
{
    private readonly IReadStore _readStore;
    
    public ProductQueryService(IReadStore readStore)
    {
        _readStore = readStore;
    }
    
    // Simple query
    public async Task<List<Product>> GetActiveProductsAsync()
    {
        return await _readStore.ListAsync<Product>(q => 
            q.Where(p => p.IsActive)
             .OrderBy(p => p.Name));
    }
    
    // Complex query with projection
    public async Task<List<ProductSummary>> GetProductSummariesAsync()
    {
        return await _readStore.ListAsync<Product, ProductSummary>(q =>
            q.Where(p => p.IsActive)
             .Select(p => new ProductSummary 
             { 
                 Id = p.Id, 
                 Name = p.Name, 
                 Price = p.Price 
             }));
    }
    
    // Direct IQueryable access for complex scenarios
    public IQueryable<Product> GetProductsQuery()
    {
        return _readStore.Query<Product>();
    }
}
```

### Writing Data

```csharp
public class ProductCommandService
{
    private readonly IDataStore _dataStore;
    
    public ProductCommandService(IDataStore dataStore)
    {
        _dataStore = dataStore;
    }
    
    // Adding single entity
    public async Task<Product> CreateProductAsync(Product product)
    {
        var saveHandle = await _dataStore.AddAsync(product);
        await saveHandle.SaveAsync();
        return product;
    }
    
    // Adding multiple entities
    public async Task AddProductsAsync(IEnumerable<Product> products)
    {
        var saveHandle = await _dataStore.AddRangeAsync(products);
        await saveHandle.SaveAsync();
    }
    
    // Updating entity
    public async Task UpdateProductAsync(Product product)
    {
        var saveHandle = _dataStore.Update(product);
        await saveHandle.SaveAsync();
    }
    
    // Bulk update
    public async Task DiscontinueProductsByCategoryAsync(int categoryId)
    {
        await _dataStore.ExecuteUpdateAsync<Product>(
            p => p.CategoryId == categoryId,
            builder => builder.SetConst(p => p.IsDiscontinued, true));
    }
    
    // Bulk delete
    public async Task DeleteExpiredProductsAsync()
    {
        await _dataStore.ExecuteDeleteAsync<Product>(
            p => p.ExpirationDate < DateTime.UtcNow);
    }
}
```

### Transaction Management

`InTransactionAsync` wraps work in a database transaction and commits on success or rolls back on exception. It does **not** call `SaveChangesAsync` implicitly — callers are responsible for flushing pending changes inside the delegate.

```csharp
public async Task<OrderResult> ProcessOrderAsync(Order order, List<OrderItem> items)
{
    return await _dataStore.InTransactionAsync(async dataStore =>
    {
        // Save the order first to obtain its auto-generated Id.
        var orderHandle = await dataStore.AddAsync(order);
        await orderHandle.SaveAsync();
        
        // Set the FK, then stage items + any other changes and flush once.
        foreach (var item in items)
            item.OrderId = order.Id;

        await dataStore.AddRangeAsync(items); // staged, not yet saved
        await dataStore.SaveChangesAsync();   // single round-trip for all staged changes
        
        // ExecuteUpdateAsync runs immediately as its own statement inside the transaction.
        await dataStore.ExecuteUpdateAsync<Product>(
            p => items.Select(i => i.ProductId).Contains(p.Id),
            builder => builder.Set(p => p.Stock, 
                p => p.Stock - items.First(i => i.ProductId == p.Id).Quantity));
        
        return new OrderResult { OrderId = order.Id, Success = true };
    });
}
```

### ISaveHandle Notes

Because EF Core uses a single shared change tracker per `DbContext` instance, calling `SaveAsync()` on any `ISaveHandle` saves **all** pending changes in the context — not only the operation that returned the handle. This is intentional: it preserves EF's unit-of-work semantics.

When you need to stage multiple mutations and persist them in one round-trip, call the mutation methods without awaiting their handles, then call `SaveChangesAsync()` directly:

```csharp
// Stage both mutations
await dataStore.AddAsync(entity1);  // handle ignored
dataStore.Update(entity2);           // handle ignored

// Persist both in a single database round-trip
await dataStore.SaveChangesAsync();
```

### Testing Examples

Separate read and write concerns into distinct service classes so each depends only on the interface it needs — this keeps mocking simple and makes the dependency contract explicit.

```csharp
// Read-only service — depends only on IReadStore
public class ProductQueryService
{
    private readonly IReadStore _readStore;
    public ProductQueryService(IReadStore readStore) => _readStore = readStore;

    public Task<List<Product>> GetActiveProductsAsync() =>
        _readStore.ListAsync<Product>(q => q.Where(p => p.IsActive).OrderBy(p => p.Name));
}

// Write-side service — depends only on IDataStore
public class ProductCommandService
{
    private readonly IDataStore _dataStore;
    public ProductCommandService(IDataStore dataStore) => _dataStore = dataStore;

    public async Task CreateProductAsync(Product product)
    {
        var handle = await _dataStore.AddAsync(product);
        await handle.SaveAsync();
    }
}

// Tests
public class ProductQueryServiceTests
{
    [Test]
    public async Task GetActiveProducts_ReturnsOnlyActiveProducts()
    {
        // Arrange
        var mockReadStore = new Mock<IReadStore>();
        var products = new List<Product>
        {
            new() { Id = 1, Name = "Active Product", IsActive = true },
            new() { Id = 2, Name = "Inactive Product", IsActive = false }
        }.AsQueryable();
        
        mockReadStore.Setup(x => x.ListAsync<Product>(
                It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Func<IQueryable<Product>, IQueryable<Product>> shape, CancellationToken _) =>
                shape(products).ToList());
        
        var service = new ProductQueryService(mockReadStore.Object);
        
        // Act
        var result = await service.GetActiveProductsAsync();
        
        // Assert
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].Name, Is.EqualTo("Active Product"));
    }
}
```

## Future Samples

This section will be expanded with additional examples as the library evolves:

### Planned Examples

- **Advanced Query Patterns**
  - Complex joins and includes
  - Dynamic query building
  - Specification pattern integration

- **Performance Optimization**
  - Query performance analysis
  - Batch operation strategies
  - Memory-efficient data processing

- **Integration Patterns**
  - CQRS implementation
  - Event sourcing scenarios
  - Microservices data patterns

- **Advanced Transaction Scenarios**
  - Distributed transactions
  - Saga pattern implementation
  - Compensation-based transactions

- **Testing Strategies**
  - Integration testing with TestContainers
  - Performance testing approaches
  - Mocking complex scenarios

---

## Summary

Flowtex.DataAccess is a pragmatic, modern alternative to the Repository + Unit of Work pattern stack. It solves the real pain points — testability, batching, bulk operations, transactions — without adding an abstraction layer that fights EF Core. Because `IQueryable<T>` is a first-class citizen, nothing is hidden from you: complex joins, projections, raw queries, and EF-specific features all work exactly as they do natively.

The split into Abstractions vs. Infrastructure packages makes it a natural fit for clean/onion architecture. Domain and application code reference only interfaces with no EF Core coupling; infrastructure wires it all up. The example suite — covering Web API usage, transaction scenarios, and unit testing strategies — demonstrates each pattern clearly and is worth reading alongside this documentation.

The one behaviour to internalise: `ISaveHandle.SaveAsync()` flushes *all* pending changes on the context, not just the one it was created from. This is a natural consequence of EF Core's Unit of Work and is by design — but understanding it upfront prevents surprises.

---

## Contributing

We welcome contributions! Please see our contributing guidelines for details on how to submit pull requests, report issues, and suggest improvements.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.