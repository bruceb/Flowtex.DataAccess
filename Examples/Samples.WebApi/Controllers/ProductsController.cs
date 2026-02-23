using Flowtex.DataAccess.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
                query = query.Where(p => p.Status == ProductStatus.Active);

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

    // Intentionally returns the full Product entity with its Category navigation property
    // included. This is appropriate here because the full domain model is the intended
    // response shape. For a DTO-projected alternative see GetProductDetail below.
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

    /// <summary>
    /// Returns a <see cref="ProductDetailDto"/> projection for the given product.
    /// Demonstrates server-side column projection: only the fields declared on the DTO
    /// are fetched from the database — no over-fetching, no circular-reference risk.
    /// Compare with <see cref="GetProduct"/> which returns the full entity graph.
    /// </summary>
    [HttpGet("{id}/detail")]
    public async Task<ActionResult<ProductDetailDto>> GetProductDetail(int id)
    {
        var dto = await _readStore.Query<Product>()
            .Where(p => p.Id == id)
            .Select(p => new ProductDetailDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                Stock = p.Stock,
                CategoryId = p.CategoryId,
                CategoryName = p.Category.Name,
                Status = p.Status,
                ExpirationDate = p.ExpirationDate,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            })
            .FirstOrDefaultAsync();

        if (dto is null)
            return NotFound();

        return Ok(dto);
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

        // Mutate the tracked entity directly. Because it was loaded via QueryTracked,
        // the change tracker already holds a snapshot; SaveChangesAsync detects the diff
        // and emits a minimal UPDATE. Calling dataStore.Update() is not needed here and
        // would unnecessarily mark all columns as modified.
        existingProduct.Name = product.Name;
        existingProduct.Description = product.Description;
        existingProduct.Price = product.Price;
        existingProduct.Stock = product.Stock;
        existingProduct.CategoryId = product.CategoryId;
        existingProduct.Status = product.Status;
        // UpdatedAt is set automatically by SampleDbContext.StampTimestamps on save.

        await _dataStore.SaveChangesAsync();

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
            p => p.Id == id && p.Status != ProductStatus.Discontinued,
            builder => builder.SetConst(p => p.Status, ProductStatus.Discontinued));

        if (affectedRows == 0)
            return NotFound();

        _logger.LogInformation("Discontinued product {ProductId}", id);

        return NoContent();
    }

    [HttpPost("bulk-update-stock")]
    public async Task<IActionResult> BulkUpdateStock([FromBody] Dictionary<int, int> stockUpdates)
    {
        // Wrap all updates in a single transaction so a partial failure
        // does not leave stock in an inconsistent state.
        await _dataStore.InTransactionAsync(async store =>
        {
            foreach (var update in stockUpdates)
            {
                await store.ExecuteUpdateAsync<Product>(
                    p => p.Id == update.Key,
                    builder => builder.SetConst(p => p.Stock, update.Value));
            }
            return true;
        });

        _logger.LogInformation("Bulk updated stock for {Count} products", stockUpdates.Count);

        return NoContent();
    }
}
