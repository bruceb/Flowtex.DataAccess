namespace Samples.Domain.DTOs;

public record OrderResult
{
    public int OrderId { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}