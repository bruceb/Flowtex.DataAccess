namespace Samples.Domain.Entities;

/// <summary>
/// Represents the lifecycle state of a product. Using a single enum prevents the
/// invalid state that arises from two separate boolean flags (IsActive + IsDiscontinued).
/// </summary>
public enum ProductStatus
{
    Active = 0,
    Inactive = 1,
    Discontinued = 2
}
