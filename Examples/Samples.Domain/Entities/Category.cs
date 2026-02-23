namespace Samples.Domain.Entities;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    /// <summary>Set by the infrastructure layer at persistence time, not at object construction.</summary>
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public List<Product> Products { get; set; } = new();
}