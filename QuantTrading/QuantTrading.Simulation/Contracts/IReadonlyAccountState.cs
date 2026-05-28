namespace QuantTrading.Simulation.Contracts;

public interface IReadonlyAccountState
{
    decimal Cash { get; }
    string Currency { get; }
    IReadOnlyDictionary<string, int> ActivePositions { get; }
    bool HasPositionOpen(string symbol);
    int GetPositionSize(string symbol);
}
