using QuantTrading.Domain.Common;

namespace QuantTrading.Domain.ValueObjects;

public sealed record Money(decimal Amount, Currency Currency)
{
    public static Result<Money> Create(decimal amount, Currency currency)
    {
        if (currency is null)
            return Result.Failure<Money>(DomainErrors.Money.Required);

        return new Money(amount, currency);
    }

    // Only allow addition if currencies match
    public static Money operator +(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new Exception(DomainErrors.Money.CurrencyMismatch.ToString());

        return new Money(a.Amount + b.Amount, a.Currency);
    }
}
