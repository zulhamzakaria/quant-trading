using QuantTrading.Shared.Contracts;
using QuantTrading.Shared.Execution;
using QuantTrading.Shared.Features;
using QuantTrading.Shared.Models;

namespace QuantTrading.Test.Common.Fakes;

/// <summary>
/// Test double for IStrategy. Returns a pre-scripted sequence of orders, one
/// per call to OnData, in order. Returns null once the script is exhausted.
/// </summary>
public sealed class ScriptedStrategy : IStrategy
{
    private readonly Queue<OrderRequest?> _script;
    public string Name { get; }
    public int CallCount { get; private set; }
    public ScriptedStrategy(
        IEnumerable<OrderRequest?> script,
        string name = "scripted-strategy")
    {
        _script = new Queue<OrderRequest?>
            (script ?? throw new ArgumentNullException(nameof(script)));
        Name = name;
    }
    public OrderRequest? OnData(
        MarketData data,
        MarketFeatures features,
        IReadonlyAccountState accountState)
    {
        CallCount++;
        return _script.Count > 0 ? _script.Dequeue() : null;
    }
}
