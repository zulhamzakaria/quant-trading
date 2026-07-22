namespace QuantTrading.Shared.Execution;

public abstract record SizingInstruction
{
    public sealed record FixedQuantity(int Shares)
        : SizingInstruction;
    public sealed record EquityFraction(decimal Fraction)
        : SizingInstruction;
}
