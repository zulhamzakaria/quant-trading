using QuantTrading.Shared.Contracts;

namespace QuantTrading.Simulation.Models;

public sealed record AccountStateSnapshot(
    decimal Cash,
    string Currency,
    IReadOnlyDictionary<string, int> ActivePositions
    ) : IReadonlyAccountState
{

    public int GetPositionSize(string symbol)
        => ActivePositions.TryGetValue(symbol, out int qty) ? qty : 0;

    public bool HasPositionOpen(string symbol)
        => ActivePositions.TryGetValue(symbol, out int qty) && qty > 0;
}
