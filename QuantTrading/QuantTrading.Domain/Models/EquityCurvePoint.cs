using QuantTrading.Domain.ValueObjects;

namespace QuantTrading.Domain.Models;

public sealed record EquityCurvePoint(DateTime Timestamp, Money Value);