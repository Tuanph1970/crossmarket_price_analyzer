namespace Common.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing a monetary amount in a specific currency.
/// </summary>
public sealed record Money
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "USD";

    public Money() { }

    public Money(decimal amount, string currency = "USD")
    {
        if (amount < 0)
            throw new ArgumentException("Money amount cannot be negative.", nameof(amount));

        Amount = Math.Round(amount, 4);
        Currency = currency.ToUpperInvariant();
    }

    /// <summary>Converts this amount to a target currency using the provided exchange rate.</summary>
    public Money ConvertTo(string targetCurrency, decimal exchangeRate)
    {
        if (Currency.Equals(targetCurrency, StringComparison.OrdinalIgnoreCase))
            return this;

        return new Money(Amount * exchangeRate, targetCurrency);
    }

    public static Money operator +(Money left, Money right)
    {
        if (!left.Currency.Equals(right.Currency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Cannot add money in {left.Currency} to money in {right.Currency}.");

        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        if (!left.Currency.Equals(right.Currency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Cannot subtract money in {right.Currency} from money in {left.Currency}.");

        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator *(Money money, decimal multiplier)
        => new(money.Amount * multiplier, money.Currency);

    public static Money Zero(string currency = "USD") => new(0, currency);

    public override string ToString() => $"{Currency} {Amount:N2}";

    public string ToDisplayString() => Currency switch
    {
        "USD" => $"${Amount:N2}",
        "VND" => $"{Amount:N0} ₫",
        _ => $"{Currency} {Amount:N2}"
    };
}
