# Flowtex.DataAccess Examples

This folder contains comprehensive examples demonstrating how to use the Flowtex.DataAccess library in various scenarios. Each example project showcases different aspects of the data access patterns and best practices.

## 📁 Project Structure

### Shared Components

- **[Samples.Domain](Samples.Domain/)** - Common domain entities, DTOs, and value objects used across all examples
- **[Samples.Infrastructure](Samples.Infrastructure/)** - Shared infrastructure including DbContext, DataStore implementation, and database configurations

### Example Projects

- **[Samples.BasicUsage](Samples.BasicUsage/)** - Console application demonstrating fundamental CRUD operations and basic patterns
- **[Samples.WebApi](Samples.WebApi/)** - ASP.NET Core Web API showing real-world usage in a web application
- **[Samples.PerformanceDemo](Samples.PerformanceDemo/)** - Performance benchmarking and comparison examples
- **[Samples.TransactionScenarios](Samples.TransactionScenarios/)** - Complex transaction patterns and error handling scenarios  
- **[Samples.TestingStrategies](Samples.TestingStrategies/)** - Unit and integration testing approaches with mocking examples

## 🚀 Getting Started

### Prerequisites

- .NET 10.0 or later
- Visual Studio 2025 or VS Code (recommended)

### Running the Examples

Each example project is self-contained and can be run independently:

```bash
# Basic Usage Console App
cd Samples.BasicUsage
dotnet run

# Web API (runs on https://localhost:7001)
cd Samples.WebApi  
dotnet run

# Performance Benchmarks
cd Samples.PerformanceDemo
dotnet run

# Transaction Scenarios
cd Samples.TransactionScenarios
dotnet run

# Unit Tests
cd Samples.TestingStrategies
dotnet test
```

## 📖 Example Descriptions

### Samples.BasicUsage
**What it demonstrates:**
- Basic CRUD operations (Create, Read, Update, Delete)
- Simple and complex querying patterns
- Bulk operations (`ExecuteUpdateAsync`, `ExecuteDeleteAsync`)
- Transaction management with `InTransactionAsync`
- Proper dependency injection setup

**Key files:**
- `Program.cs` - Main entry point with all examples
- Demonstrates the core patterns from the main README

### Samples.WebApi  
**What it demonstrates:**
- ASP.NET Core integration and dependency injection
- RESTful API endpoints using IDataStore/IReadStore
- Model validation and error handling
- Bulk operations through API endpoints
- Swagger/OpenAPI documentation

**Key files:**
- `Program.cs` - Application startup and DI configuration
- `Controllers/ProductsController.cs` - Complete CRUD API controller

**Endpoints:**
- `GET /api/products` - List products with filtering
- `GET /api/products/{id}` - Get single product
- `POST /api/products` - Create new product
- `PUT /api/products/{id}` - Update product
- `DELETE /api/products/{id}` - Delete product
- `POST /api/products/{id}/discontinue` - Bulk discontinue
- `POST /api/products/bulk-update-stock` - Bulk stock update

### Samples.PerformanceDemo
**What it demonstrates:**
- Performance benchmarking using BenchmarkDotNet
- Comparison between different data access patterns
- Memory usage analysis
- Query optimization techniques

**Key features:**
- Benchmarked operations for common scenarios
- Performance metrics and analysis
- Best practices for high-performance data access

### Samples.TransactionScenarios
**What it demonstrates:**
- Complex multi-step transactions
- Error handling and rollback scenarios
- Compensating transaction patterns
- Nested transaction contexts
- Real-world business logic implementation

**Key scenarios:**
- Complex order processing with inventory management
- Payment processing with automatic rollback
- Nested transaction examples
- Exception handling within transactions

### Samples.TestingStrategies
**What it demonstrates:**
- Unit testing with mocked interfaces
- Integration testing approaches
- Test data setup and cleanup
- Assertion patterns for data access operations

**Key features:**
- NUnit test framework examples
- Moq mocking library usage
- In-memory database testing
- Service layer testing patterns

## 🏗️ Shared Infrastructure

### Samples.Domain
Contains the common domain model used across all examples:

- **Entities:** `Product`, `Category`, `Customer`, `Order`, `OrderItem`
- **DTOs:** `ProductSummary`, `OrderResult`
- **Enums:** `OrderStatus`

### Samples.Infrastructure
Provides shared infrastructure components:

- **`SampleDbContext`** - Entity Framework DbContext with full configuration
- **`SampleDataStore`** - Implementation of DataStoreBase for the sample domain
- **Seed Data** - Pre-populated test data for examples
- **Database Configurations** - Entity mappings and relationships

## 💡 Key Concepts Demonstrated

### 1. Separation of Read and Write Operations
```csharp
// Read operations (no-tracking, optimized for queries)
var products = await readStore.ListAsync<Product>(q => 
    q.Where(p => p.IsActive));

// Write operations (with change tracking)
var saveHandle = await dataStore.AddAsync(product);
await saveHandle.SaveAsync();
```

### 2. Explicit Save Control
```csharp
// Fine-grained control over when data is persisted
var handle1 = await dataStore.AddAsync(entity1);
var handle2 = await dataStore.AddAsync(entity2);

// Save both together
await handle1.SaveAsync();
await handle2.SaveAsync();
```

### 3. Powerful Bulk Operations
```csharp
// Server-side bulk updates
await dataStore.ExecuteUpdateAsync<Product>(
    p => p.CategoryId == categoryId,
    builder => builder.SetConst(p => p.IsDiscontinued, true));
```

### 4. Transaction Management
```csharp
// Atomic transactions with automatic rollback
var result = await dataStore.InTransactionAsync(async tx =>
{
    // Multiple operations in single transaction
    var handle1 = await tx.AddAsync(order);
    await handle1.SaveAsync();
    
    await tx.ExecuteUpdateAsync<Product>(/* update inventory */);
    
    return orderResult;
});
```

## 🔧 Configuration Notes

All examples use:
- **In-Memory Database** for simplicity and portability
- **Entity Framework Core 10.0**
- **Dependency Injection** with Microsoft.Extensions.DependencyInjection
- **Logging** with console output for visibility

For production usage, replace the in-memory database with your preferred provider (SQL Server, PostgreSQL, etc.).

## 📚 Next Steps

1. **Start with BasicUsage** - Run the console app to see core concepts
2. **Explore WebApi** - See real-world API integration
3. **Review TestingStrategies** - Learn testing patterns
4. **Run PerformanceDemo** - Understand performance characteristics
5. **Study TransactionScenarios** - Master complex business logic patterns

## 🤝 Contributing

These examples are continuously improved. If you have suggestions for additional scenarios or improvements to existing examples, please contribute back to the main repository.

## 📄 License

These examples are part of the Flowtex.DataAccess library and follow the same MIT license terms.