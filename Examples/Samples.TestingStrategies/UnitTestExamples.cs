using Flowtex.DataAccess.Abstractions;
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
            new() { Id = 1, Name = "Active Product 1", Status = ProductStatus.Active, Price = 10.00m },
            new() { Id = 2, Name = "Inactive Product", Status = ProductStatus.Inactive, Price = 20.00m },
            new() { Id = 3, Name = "Active Product 2", Status = ProductStatus.Active, Price = 30.00m }
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

        var service = new ProductQueryService(_mockReadStore.Object);

        // Act
        var result = await service.GetActiveProductsAsync();

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.All(p => p.Status == ProductStatus.Active), Is.True);
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

        var service = new ProductCommandService(_mockDataStore.Object);

        // Act
        await service.CreateProductAsync(product);

        // Assert
        _mockDataStore.Verify(x => x.AddAsync(product, It.IsAny<CancellationToken>()), Times.Once);
        mockSaveHandle.Verify(x => x.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

// Example service classes demonstrating proper separation of read/write concerns.
// Each class has a single constructor with a non-nullable dependency — no null guards needed.

/// <summary>Read-only product query service. Depends only on <see cref="IReadStore"/>.</summary>
public class ProductQueryService
{
    private readonly IReadStore _readStore;

    public ProductQueryService(IReadStore readStore) =>
        _readStore = readStore;

    public Task<List<Product>> GetActiveProductsAsync() =>
        _readStore.ListAsync<Product>(q =>
            q.Where(p => p.Status == ProductStatus.Active)
             .OrderBy(p => p.Name));
}

/// <summary>Write-side product command service. Depends on <see cref="IDataStore"/>.</summary>
public class ProductCommandService
{
    private readonly IDataStore _dataStore;

    public ProductCommandService(IDataStore dataStore) =>
        _dataStore = dataStore;

    public async Task CreateProductAsync(Product product)
    {
        var handle = await _dataStore.AddAsync(product);
        await handle.SaveAsync();
    }
}