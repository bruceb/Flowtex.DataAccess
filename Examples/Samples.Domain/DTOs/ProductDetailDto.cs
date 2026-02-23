using Samples.Domain.Entities;

namespace Samples.Domain.DTOs;

/// <summary>
/// A richer projection DTO for single-product endpoints.
/// Demonstrates server-side projection via IReadStore.Query&lt;T&gt;().Select(...):
/// only the columns listed here are fetched from the database.
/// </summary>
public record ProductDetailDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public int Stock { get; init; }
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public ProductStatus Status { get; init; }
    public DateTime? ExpirationDate { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
