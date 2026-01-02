using Flowtex.DataAccess.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Samples.Domain.DTOs;
using Samples.Domain.Entities;
using Samples.Infrastructure;

namespace Samples.TransactionScenarios;

class Program
{
    static async Task Main(string[] args)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        
        var serviceProvider = services.BuildServiceProvider();
        
        using var scope = serviceProvider.CreateScope();
        var dataStore = scope.ServiceProvider.GetRequiredService<IDataStore>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        // Initialize database
        var context = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        await context.Database.EnsureCreatedAsync();
        
        logger.LogInformation("=== Transaction Scenario Examples ===");
        
        await ComplexOrderProcessingExample(dataStore, logger);
        await CompensatingTransactionExample(dataStore, logger);
        await NestedTransactionExample(dataStore, logger);
        
        logger.LogInformation("=== Transaction examples completed! ===");
    }
    
    static void ConfigureServices(ServiceCollection services)
    {
        services.AddDbContext<SampleDbContext>(options =>
            options.UseInMemoryDatabase("TransactionDb"));
            
        services.AddScoped<IDataStore, SampleDataStore>();
        services.AddLogging(builder => builder.AddConsole());
    }
    
    static async Task ComplexOrderProcessingExample(IDataStore dataStore, ILogger logger)
    {
        logger.LogInformation("\n--- Complex Order Processing Transaction ---");
        
        try
        {
            var result = await dataStore.InTransactionAsync(async transactionDataStore =>
            {
                // Step 1: Create customer if needed
                var customer = new Customer
                {
                    FirstName = "Transaction",
                    LastName = "Customer",
                    Email = "transaction@example.com"
                };
                
                var customerHandle = await transactionDataStore.AddAsync(customer);
                await customerHandle.SaveAsync();
                
                // Step 2: Create order
                var order = new Order
                {
                    OrderNumber = $"TXN-{DateTime.Now:yyyyMMddHHmmss}",
                    CustomerId = customer.Id,
                    Status = OrderStatus.Processing
                };
                
                var orderHandle = await transactionDataStore.AddAsync(order);
                await orderHandle.SaveAsync();
                
                // Step 3: Process multiple order items with stock validation
                var requestedItems = new[]
                {
                    new { ProductId = 1, Quantity = 2 },
                    new { ProductId = 2, Quantity = 1 },
                    new { ProductId = 3, Quantity = 3 }
                };
                
                var orderItems = new List<OrderItem>();
                decimal totalAmount = 0;
                
                foreach (var item in requestedItems)
                {
                    // Check stock availability
                    var product = await transactionDataStore.QueryTracked<Product>()
                        .FirstOrDefaultAsync(p => p.Id == item.ProductId);
                    
                    if (product == null)
                        throw new InvalidOperationException($"Product {item.ProductId} not found");
                        
                    if (product.Stock < item.Quantity)
                        throw new InvalidOperationException($"Insufficient stock for product {product.Name}");
                    
                    // Create order item
                    var orderItem = new OrderItem
                    {
                        OrderId = order.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = product.Price
                    };
                    
                    orderItems.Add(orderItem);
                    totalAmount += orderItem.TotalPrice;
                    
                    // Update product stock
                    product.Stock -= item.Quantity;
                    var productUpdateHandle = transactionDataStore.Update(product);
                    await productUpdateHandle.SaveAsync();
                }
                
                // Step 4: Save order items
                var itemsHandle = await transactionDataStore.AddRangeAsync(orderItems);
                await itemsHandle.SaveAsync();
                
                // Step 5: Update order total
                order.TotalAmount = totalAmount;
                var orderUpdateHandle = transactionDataStore.Update(order);
                await orderUpdateHandle.SaveAsync();
                
                // Step 6: Mark order as shipped (simulate processing time)
                order.Status = OrderStatus.Shipped;
                var finalOrderHandle = transactionDataStore.Update(order);
                await finalOrderHandle.SaveAsync();
                
                return new OrderResult 
                { 
                    OrderId = order.Id, 
                    Success = true 
                };
            });
            
            logger.LogInformation($"Complex order processed successfully: Order {result.OrderId}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Complex order processing failed");
        }
    }
    
    static async Task CompensatingTransactionExample(IDataStore dataStore, ILogger logger)
    {
        logger.LogInformation("\n--- Compensating Transaction Example ---");
        
        // First, let's create a scenario where we need to rollback
        try
        {
            await dataStore.InTransactionAsync(async transactionDataStore =>
            {
                // Step 1: Reserve inventory
                await transactionDataStore.ExecuteUpdateAsync<Product>(
                    p => p.Id == 1,
                    builder => builder.Set(p => p.Stock, p => p.Stock - 10));
                
                logger.LogInformation("Reserved 10 units of product 1");
                
                // Step 2: Create a payment record (simulated)
                var order = new Order
                {
                    OrderNumber = "COMP-" + DateTime.Now.Ticks,
                    CustomerId = 1,
                    TotalAmount = 100.00m,
                    Status = OrderStatus.Processing
                };
                
                var orderHandle = await transactionDataStore.AddAsync(order);
                await orderHandle.SaveAsync();
                
                logger.LogInformation($"Created order {order.OrderNumber}");
                
                // Step 3: Simulate payment processing failure
                await Task.Delay(100); // Simulate processing time
                throw new InvalidOperationException("Payment processing failed");
                
                // This code won't be reached, transaction will rollback
                return true;
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning("Transaction rolled back due to: {Error}", ex.Message);
            logger.LogInformation("Inventory reservation and order creation were automatically rolled back");
            
            // Verify rollback worked
            var product = await dataStore.Query<Product>()
                .FirstOrDefaultAsync(p => p.Id == 1);
            logger.LogInformation($"Product 1 stock after rollback: {product?.Stock}");
        }
    }
    
    static async Task NestedTransactionExample(IDataStore dataStore, ILogger logger)
    {
        logger.LogInformation("\n--- Nested Transaction Example ---");
        
        var result = await dataStore.InTransactionAsync(async outerDataStore =>
        {
            // Outer transaction: Update customer information
            var customer = await outerDataStore.QueryTracked<Customer>()
                .FirstOrDefaultAsync(c => c.Id == 1);
            
            if (customer != null)
            {
                customer.Phone = "+1-555-0123";
                var customerHandle = outerDataStore.Update(customer);
                await customerHandle.SaveAsync();
                
                logger.LogInformation("Updated customer phone in outer transaction");
            }
            
            // Inner business logic (uses same transaction context)
            var orderResult = await ProcessOrderInSameTransaction(outerDataStore, logger);
            
            // Both customer update and order processing share the same transaction
            return new { CustomerUpdated = true, OrderResult = orderResult };
        });
        
        logger.LogInformation("Nested transaction completed: Customer updated = {CustomerUpdated}, Order success = {OrderSuccess}", 
            result.CustomerUpdated, result.OrderResult.Success);
    }
    
    private static async Task<OrderResult> ProcessOrderInSameTransaction(IDataStore dataStore, ILogger logger)
    {
        // This method participates in the existing transaction
        var order = new Order
        {
            OrderNumber = $"NESTED-{DateTime.Now:HHmmss}",
            CustomerId = 1,
            TotalAmount = 50.00m,
            Status = OrderStatus.Pending
        };
        
        var orderHandle = await dataStore.AddAsync(order);
        await orderHandle.SaveAsync();
        
        logger.LogInformation("Created order in nested transaction context");
        
        return new OrderResult { OrderId = order.Id, Success = true };
    }
}