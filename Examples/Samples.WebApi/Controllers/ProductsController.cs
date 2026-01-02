using Flowtex.DataAccess.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Samples.Domain.DTOs;
using Samples.Domain.Entities;

namespace Samples.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IDataStore _dataStore;
    private readonly IReadStore _readStore;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        IDataStore dataStore, 
        IReadStore readStore,
        ILogger<ProductsController> logger)
    {
        _dataStore = dataStore;
        _readStore = readStore;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<ProductSummary>>> GetProducts(
        [FromQuery] bool? activeOnly = null,
        [FromQuery] int? categoryId = null)
    {
        var products = await _readStore.ListAsync<Product, ProductSummary>(q =>
        {
            var query = q.AsQueryable();
            
            if (activeOnly == true)
                query = query.Where(p => p.IsActive);
                
            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId.Value);
            
            return query.Select(p => new ProductSummary
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price,
                CategoryName = p.Category.Name,
                Stock = p.Stock
            });
        });

        return Ok(products);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Product>> GetProduct(int id)
    {
        var product = await _readStore.Query<Product>()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
            return NotFound();

        return Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<Product>> CreateProduct(Product product)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var handle = await _dataStore.AddAsync(product);
        await handle.SaveAsync();

        _logger.LogInformation("Created product {ProductId}: {ProductName}", product.Id, product.Name);
        
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProduct(int id, Product product)
    {
        if (id != product.Id)
            return BadRequest();

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existingProduct = await _dataStore.QueryTracked<Product>()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (existingProduct == null)
            return NotFound();

        // Update properties
        existingProduct.Name = product.Name;
        existingProduct.Description = product.Description;
        existingProduct.Price = product.Price;
        existingProduct.Stock = product.Stock;
        existingProduct.CategoryId = product.CategoryId;
        existingProduct.IsActive = product.IsActive;
        existingProduct.UpdatedAt = DateTime.UtcNow;

        var handle = _dataStore.Update(existingProduct);
        await handle.SaveAsync();

        _logger.LogInformation("Updated product {ProductId}: {ProductName}", id, product.Name);

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var product = await _dataStore.QueryTracked<Product>()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
            return NotFound();

        var handle = _dataStore.Remove(product);
        await handle.SaveAsync();

        _logger.LogInformation("Deleted product {ProductId}", id);

        return NoContent();
    }

    [HttpPost("{id}/discontinue")]
    public async Task<IActionResult> DiscontinueProduct(int id)
    {
        var affectedRows = await _dataStore.ExecuteUpdateAsync<Product>(
            p => p.Id == id && !p.IsDiscontinued,
            builder => builder.SetConst(p => p.IsDiscontinued, true));

        if (affectedRows == 0)
            return NotFound();

        _logger.LogInformation("Discontinued product {ProductId}", id);

        return NoContent();
    }

    [HttpPost("bulk-update-stock")]
    public async Task<IActionResult> BulkUpdateStock([FromBody] Dictionary<int, int> stockUpdates)
    {
        var productIds = stockUpdates.Keys.ToList();
        
        foreach (var update in stockUpdates)
        {
            await _dataStore.ExecuteUpdateAsync<Product>(
                p => p.Id == update.Key,
                builder => builder.SetConst(p => p.Stock, update.Value));
        }

        _logger.LogInformation("Bulk updated stock for {Count} products", stockUpdates.Count);

        return NoContent();
    }
}