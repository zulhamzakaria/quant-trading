using QuantTrading.Domain.Common;

namespace QuantTrading.Domain.ValueObjects;

public sealed record Currency
{
    public string Code { get; }

    private Currency(string code) => Code = code;

    public static Result<Currency> Create(string code)
    {
        code = code?.Trim().ToUpperInvariant() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(code) 
            || code.Length != 3 
            || !code.All(char.IsLetter))
            return Result.Failure<Currency>(DomainErrors.Currency.InvalidCode);

        return new Currency(code);
    }

    public static readonly Currency USD = new("USD");
    public static readonly Currency EUR = new("EUR");
    public static readonly Currency GBP = new("GBP");
    public static readonly Currency JPY = new("JPY");
    public static readonly Currency MYR = new("MYR");
    public static readonly Currency BTC = new("BTC");
    public static readonly Currency ETH = new("ETH");

    public static explicit operator string(Currency currency) => currency.Code;

    public override string ToString() => Code;
}
