using QuantTrading.Shared.Contracts;
using QuantTrading.Shared.Models;
using QuantTrading.Simulation.Engine;
using QuantTrading.Simulation.Models;
using QuantTrading.Simulation.Reporting;
using QuantTrading.Simulation.Strategies;

namespace QuantTrading.Simulation.Tournament;

public sealed class TournamentRunner
{
    private readonly decimal _startingCapital;
    private readonly string _currency;

    public TournamentRunner(
        decimal startingCapital = 10_000m,
        string currency = "USD")
    {
        if (startingCapital <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startingCapital),
                "Starting capital must be greater than zero.");
        }
        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException(
                "Currency cannot be null or whitespace.",
                nameof(currency));
        }
        _startingCapital = startingCapital;
        _currency = currency;
    }

    public IReadOnlyList<StrategyResult> Run(
        IReadOnlyList<IStrategy> strategies,
        IReadOnlyList<MarketData> historicalData)
    {
        if (strategies is null || strategies.Count is 0)
        {
            throw new ArgumentException(
                "At least one strategy must be provided.",
                nameof(strategies));
        }
        if (historicalData is null || historicalData.Count is 0)
        {
            throw new ArgumentException(
                "Historical data cannot be null or empty.",
                nameof(historicalData));
        }

        BacktestEngine engine = new();

        foreach (var strategy in strategies)
        {
            engine.RegisterStrategy(
                strategy,
                _startingCapital,
                _currency);
        }

        engine.RunSimulation(historicalData);

        DateTime firstBar = historicalData[0].Timestamp;
        DateTime lastBar = historicalData[^1].Timestamp;

        List<StrategyResult> results = new();

        foreach (var strategy in strategies)
        {
            var equityCurve = engine.GetEquityCurve(strategy);
            decimal endingValue = equityCurve[^1].Equity;

            var trades = engine.GetCompletedTrades(strategy);
            string symbol = historicalData[0].Symbol;
            var openEntry = engine.GetOpenPositionEntryTimestamp
                (strategy, symbol);
            decimal exposureRatio = ExposureReporter.CalculateExposureRatio(
                trades,
                firstBar,
                lastBar,
                openEntry);


            results.Add(new StrategyResult
                (StrategyName: strategy.Name,
                Trades: engine.GetCompletedTrades(strategy),
                StartingCapital: _startingCapital,
                EndingPortfolioValue: endingValue,
                FirstBarTimestamp: firstBar,
                LastBarTimestamp: lastBar,
                EquityCurve: equityCurve,
                ExposureRatio: exposureRatio));

            // TEMP: diagnostic hook for Experiment 3.
            // Remove after diagnostic summary is promoted to the reporting pipeline.
            if (strategy is MlStrategy strat)
            {
                var finalState = engine.GetAccountState(strategy);
                strat.PrintDiagnosticSummary(finalState);
            }

        }

        return results;
    }

}
