# Flowtex.DataAccess

A modern, streamlined data access library for Entity Framework Core that provides a clean alternative to the traditional Repository and Unit of Work patterns. This library simplifies data operations while maintaining performance and testability.

## What This Library Does

Flowtex.DataAccess provides a unified interface for data access operations through a set of core abstractions:

- **`IReadStore`** - Read-only operations with optimized no-tracking queries
- **`IDataStore`** - Full CRUD operations including reads, writes, and transactions
- **`ISaveHandle`** - Explicit save control for better transaction management
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

**1. Direct EF Core Integration**
- Leverages EF Core's native capabilities instead of hiding them
- No performance penalties from unnecessary abstractions
- Access to full LINQ query power through `IQueryable<T>`

**2. Explicit Save Control**
- `ISaveHandle` provides clear control over when data is persisted
- Enables batch operations and transaction optimization
- Reduces accidental database round-trips

**3. Performance Optimized**
- No-tracking queries by default for reads (`IReadStore`)
- Tracked queries available when needed (`QueryTracked<T>`)
- Efficient bulk operations with `ExecuteUpdateAsync` and `ExecuteDeleteAsync`

**4. Clean Testing**
- Interfaces are easily mockable
- No complex repository hierarchies to maintain
- Direct integration with EF Core's in-memory testing capabilities

**5. Simplified Architecture**
- Single interface for data operations instead of multiple repositories
- Built-in transaction support without separate Unit of Work classes
- Consistent API across all entity types

## Examples

### Basic Setup

```csharp
// Implementation example (in your Infrastructure layer)
public class AppDataStore : DataStoreBase
{
    private readonly AppDbContext _context;
    
    public AppDataStore(AppDbContext context)
    {
        _context = context;
    }
    
    protected override DbContext Context => _context;
    
    protected override DbSet<T> GetDbSet<T>() => _context.Set<T>();
}

// Dependency injection setup
services.AddScoped<IDataStore, AppDataStore>();
services.AddScoped<IReadStore>(provider => provider.GetService<IDataStore>());
```

### Reading Data

```csharp
public class ProductService
{
    private readonly IReadStore _readStore;
    
    public ProductService(IReadStore readStore)
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
public class ProductService
{
    private readonly IDataStore _dataStore;
    
    public ProductService(IDataStore dataStore)
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

```csharp
public async Task<OrderResult> ProcessOrderAsync(Order order, List<OrderItem> items)
{
    return await _dataStore.InTransactionAsync(async dataStore =>
    {
        // Add order
        var orderHandle = await dataStore.AddAsync(order);
        await orderHandle.SaveAsync();
        
        // Add order items
        foreach (var item in items)
        {
            item.OrderId = order.Id;
        }
        var itemsHandle = await dataStore.AddRangeAsync(items);
        await itemsHandle.SaveAsync();
        
        // Update inventory
        await dataStore.ExecuteUpdateAsync<Product>(
            p => items.Select(i => i.ProductId).Contains(p.Id),
            builder => builder.Set(p => p.Stock, 
                p => p.Stock - items.First(i => i.ProductId == p.Id).Quantity));
        
        return new OrderResult { OrderId = order.Id, Success = true };
    });
}
```

### Testing Examples

```csharp
public class ProductServiceTests
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
        
        mockReadStore.Setup(x => x.Query<Product>())
                   .Returns(products);
        
        var service = new ProductService(mockReadStore.Object);
        
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

## Contributing

We welcome contributions! Please see our contributing guidelines for details on how to submit pull requests, report issues, and suggest improvements.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.