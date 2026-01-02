namespace Samples.Domain.DTOs;

public record ProductSummary
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public int Stock { get; init; }
}