using Flowtex.DataAccess.Application.Abstractions;
using Moq;
using NUnit.Framework;
using Samples.Domain.Entities;

namespace Samples.TestingStrategies;

[TestFixture]
public class UnitTestExamples
{
    private Mock<IReadStore> _mockReadStore = null!;
    private Mock<IDataStore> _mockDataStore = null!;
    
    [SetUp]
    public void Setup()
    {
        _mockReadStore = new Mock<IReadStore>();
        _mockDataStore = new Mock<IDataStore>();
    }

    [Test]
    public async Task GetActiveProducts_WithMockedReadStore_ReturnsCorrectData()
    {
        // Arrange
        var products = new List<Product>
        {
            new() { Id = 1, Name = "Active Product 1", IsActive = true, Price = 10.00m },
            new() { Id = 2, Name = "Inactive Product", IsActive = false, Price = 20.00m },
            new() { Id = 3, Name = "Active Product 2", IsActive = true, Price = 30.00m }
        };

        _mockReadStore.Setup(x => x.ListAsync<Product>(
                It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Func<IQueryable<Product>, IQueryable<Product>> shape, CancellationToken ct) =>
            {
                var query = products.AsQueryable();
                if (shape != null)
                    query = shape(query);
                return query.ToList();
            });

        var service = new ProductService(_mockReadStore.Object);

        // Act
        var result = await service.GetActiveProductsAsync();

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.All(p => p.IsActive), Is.True);
        Assert.That(result.Select(p => p.Name), Is.EquivalentTo(new[] { "Active Product 1", "Active Product 2" }));
    }

    [Test]
    public async Task AddProduct_WithMockedDataStore_CallsCorrectMethods()
    {
        // Arrange
        var product = new Product { Name = "New Product", Price = 15.00m };
        var mockSaveHandle = new Mock<ISaveHandle>();
        
        _mockDataStore.Setup(x => x.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockSaveHandle.Object);
            
        mockSaveHandle.Setup(x => x.SaveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service = new ProductService(_mockDataStore.Object);

        // Act
        await service.CreateProductAsync(product);

        // Assert
        _mockDataStore.Verify(x => x.AddAsync(product, It.IsAny<CancellationToken>()), Times.Once);
        mockSaveHandle.Verify(x => x.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

// Example service class that uses the data access patterns
public class ProductService
{
    private readonly IReadStore? _readStore;
    private readonly IDataStore? _dataStore;

    public ProductService(IReadStore readStore)
    {
        _readStore = readStore;
    }

    public ProductService(IDataStore dataStore)
    {
        _dataStore = dataStore;
    }

    public async Task<List<Product>> GetActiveProductsAsync()
    {
        if (_readStore == null) throw new InvalidOperationException("ReadStore not configured");
        
        return await _readStore.ListAsync<Product>(q => 
            q.Where(p => p.IsActive)
             .OrderBy(p => p.Name));
    }

    public async Task CreateProductAsync(Product product)
    {
        if (_dataStore == null) throw new InvalidOperationException("DataStore not configured");
        
        var handle = await _dataStore.AddAsync(product);
        await handle.SaveAsync();
    }
}