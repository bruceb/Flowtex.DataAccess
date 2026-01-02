using Flowtex.DataAccess.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Samples.Domain.Entities;
using Samples.Infrastructure;

namespace Samples.BasicUsage;

class Program
{
    static async Task Main(string[] args)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        
        var serviceProvider = services.BuildServiceProvider();
        
        using var scope = serviceProvider.CreateScope();
        var dataStore = scope.ServiceProvider.GetRequiredService<IDataStore>();
        var readStore = scope.ServiceProvider.GetRequiredService<IReadStore>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        // Initialize database
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        await context.Database.EnsureCreatedAsync();
        
        logger.LogInformation("=== Flowtex DataAccess Basic Usage Examples ===");
        
        await RunReadExamples(readStore, logger);
        await RunWriteExamples(dataStore, logger);
        await RunBulkOperationExamples(dataStore, logger);
        await RunTransactionExample(dataStore, logger);
        
        logger.LogInformation("=== Examples completed successfully! ===");
    }
    
    static void ConfigureServices(ServiceCollection services)
    {
        services.AddDbContext<SampleDbContext>(options =>
            options.UseInMemoryDatabase("SampleDb"));
            
        services.AddScoped<IDataStore, SampleDataStore>();
        services.AddScoped<IReadStore>(provider => provider.GetService<IDataStore>()!);
        
        services.AddLogging(builder => builder.AddConsole());
    }
    
    static async Task RunReadExamples(IReadStore readStore, ILogger logger)
    {
        logger.LogInformation("\n--- Read Examples ---");
        
        // Simple query
        var activeProducts = await readStore.ListAsync<Product>(q => 
            q.Where(p => p.IsActive)
             .OrderBy(p => p.Name));
             
        logger.LogInformation($"Found {activeProducts.Count} active products:");
        foreach (var product in activeProducts)
        {
            logger.LogInformation($"  - {product.Name}: ${product.Price}");
        }
        
        // Query with projection
        var productSummaries = await readStore.ListAsync<Product, object>(q =>
            q.Where(p => p.IsActive)
             .Select(p => new { p.Name, p.Price, CategoryName = p.Category.Name }));
             
        logger.LogInformation($"\nProduct summaries ({productSummaries.Count} items):");
        foreach (var summary in productSummaries)
        {
            logger.LogInformation($"  - {summary}");
        }
        
        // Direct IQueryable usage
        var expensiveProducts = readStore.Query<Product>()
            .Where(p => p.Price > 500)
            .Count();
            
        logger.LogInformation($"\nExpensive products (>$500): {expensiveProducts}");
    }
    
    static async Task RunWriteExamples(IDataStore dataStore, ILogger logger)
    {
        logger.LogInformation("\n--- Write Examples ---");
        
        // Add single entity
        var newProduct = new Product
        {
            Name = "Wireless Mouse",
            Description = "Ergonomic wireless mouse",
            Price = 29.99m,
            Stock = 75,
            CategoryId = 1
        };
        
        var addHandle = await dataStore.AddAsync(newProduct);
        await addHandle.SaveAsync();
        
        logger.LogInformation($"Added product: {newProduct.Name} (ID: {newProduct.Id})");
        
        // Update entity
        newProduct.Price = 24.99m;
        var updateHandle = dataStore.Update(newProduct);
        await updateHandle.SaveAsync();
        
        logger.LogInformation($"Updated product price to: ${newProduct.Price}");
        
        // Add multiple entities
        var newCategories = new List<Category>
        {
            new() { Name = "Sports", Description = "Sports equipment and gear" },
            new() { Name = "Home & Garden", Description = "Home and garden supplies" }
        };
        
        var addRangeHandle = await dataStore.AddRangeAsync(newCategories);
        await addRangeHandle.SaveAsync();
        
        logger.LogInformation($"Added {newCategories.Count} new categories");
    }
    
    static async Task RunBulkOperationExamples(IDataStore dataStore, ILogger logger)
    {
        logger.LogInformation("\n--- Bulk Operation Examples ---");
        
        // Bulk update
        var updatedCount = await dataStore.ExecuteUpdateAsync<Product>(
            p => p.CategoryId == 1,
            builder => builder.SetConst(p => p.IsActive, true));
            
        logger.LogInformation($"Bulk updated {updatedCount} electronics products");
        
        // Bulk update with expression
        await dataStore.ExecuteUpdateAsync<Product>(
            p => p.Stock < 10,
            builder => builder.Set(p => p.Stock, p => p.Stock + 50));
            
        logger.LogInformation("Restocked low-inventory products");
    }
    
    static async Task RunTransactionExample(IDataStore dataStore, ILogger logger)
    {
        logger.LogInformation("\n--- Transaction Example ---");
        
        var result = await dataStore.InTransactionAsync(async transactionDataStore =>
        {
            // Create order
            var order = new Order
            {
                OrderNumber = $"ORD-{DateTime.Now:yyyyMMddHHmmss}",
                CustomerId = 1,
                TotalAmount = 0
            };
            
            var orderHandle = await transactionDataStore.AddAsync(order);
            await orderHandle.SaveAsync();
            
            // Add order items
            var orderItems = new List<OrderItem>
            {
                new() { OrderId = order.Id, ProductId = 1, Quantity = 1, UnitPrice = 1299.99m },
                new() { OrderId = order.Id, ProductId = 3, Quantity = 2, UnitPrice = 49.99m }
            };
            
            var itemsHandle = await transactionDataStore.AddRangeAsync(orderItems);
            await itemsHandle.SaveAsync();
            
            // Update order total
            order.TotalAmount = orderItems.Sum(i => i.TotalPrice);
            var updateOrderHandle = transactionDataStore.Update(order);
            await updateOrderHandle.SaveAsync();
            
            // Update product stock
            await transactionDataStore.ExecuteUpdateAsync<Product>(
                p => p.Id == 1,
                builder => builder.Set(p => p.Stock, p => p.Stock - 1));
                
            await transactionDataStore.ExecuteUpdateAsync<Product>(
                p => p.Id == 3,
                builder => builder.Set(p => p.Stock, p => p.Stock - 2));
            
            return new { OrderId = order.Id, Total = order.TotalAmount };
        });
        
        logger.LogInformation($"Transaction completed - Order {result.OrderId}, Total: ${result.Total}");
    }
}