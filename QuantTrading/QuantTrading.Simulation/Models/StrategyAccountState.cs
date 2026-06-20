using QuantTrading.Shared.Contracts;
using System.Collections.ObjectModel;

namespace QuantTrading.Simulation.Models;

public sealed class StrategyAccountState : IReadonlyAccountState
{
    private readonly Dictionary<string, int> _positions
        = new(StringComparer.OrdinalIgnoreCase);

    public decimal Cash { get; private set; }
    public string Currency { get; }

    public IReadOnlyDictionary<string, int> ActivePositions { get; }

    public StrategyAccountState
        (decimal startingCash, string currency)
    {
        Cash = startingCash;
        Currency = currency;
        ActivePositions =
            new ReadOnlyDictionary<string, int>(_positions);
    }

    public int GetPositionSize(string symbol)
        => _positions.TryGetValue(symbol, out int size)
        ? size : 0;

    public bool HasPositionOpen(string symbol)
        => _positions.TryGetValue(symbol, out int size)
        && size > 0;

    internal void DebitCash(decimal amount) 
        => Cash -= amount;

    internal void CreditCash(decimal amount) 
        => Cash += amount;

    internal void UpdatePosition
        (string symbol, int shares, bool isExit)
    {
        _positions.TryGetValue(symbol, out int currenctShares);

        if (isExit)
        {
            int remaining = currenctShares - shares;
            if(remaining <= 0)
                _positions.Remove(symbol);
            else
                _positions[symbol] = remaining;
        }
        else
        {
            _positions[symbol] = currenctShares + shares;
        }
    }

}
