namespace Samples.Domain.Entities;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public int CategoryId { get; set; }
    /// <summary>
    /// Replaces the previous dual IsActive/IsDiscontinued boolean flags.
    /// A single enum prevents the invalid state where both flags could be set simultaneously.
    /// </summary>
    public ProductStatus Status { get; set; } = ProductStatus.Active;
    public DateTime? ExpirationDate { get; set; }
    /// <summary>Set by the infrastructure layer at persistence time, not at object construction.</summary>
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public Category Category { get; set; } = null!;
    public List<OrderItem> OrderItems { get; set; } = new();
}