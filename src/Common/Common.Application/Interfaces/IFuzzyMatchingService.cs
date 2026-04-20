namespace Common.Application.Interfaces;

/// <summary>
/// Computes a fuzzy match score between a US product and a Vietnam product name.
/// Score range: 0–100.
/// </summary>
public interface IFuzzyMatchingService
{
    decimal ComputeMatchScore(
        string usProductName,
        string vnProductName,
        string? usBrand = null,
        string? vnBrand = null);
}
