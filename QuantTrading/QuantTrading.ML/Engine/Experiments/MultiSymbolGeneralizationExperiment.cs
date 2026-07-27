using QuantTrading.Infrastructure.Data;
using QuantTrading.Shared.Contracts;
using QuantTrading.Shared.Features;
using QuantTrading.Shared.Models;
using QuantTrading.Simulation.Analytics;
using QuantTrading.Simulation.Engine;
using QuantTrading.Simulation.Strategies;
using QuantTrading.Simulation.Tournament;

namespace QuantTrading.ML.Engine.Experiments;

// Disposable, single-checkpoint study — not permanent infrastructure like
// ModelTrainer. Answers one pre-registered question: does
// BaseObvPriceZScoreFeatures beat BaseObvFeatures across a fixed 6-symbol
// large-cap-tech basket, on both AUC and full tournament metrics.
// See handoff doc Position Sizing Checkpoint 3 -> Phase 4 pivot for the
// full pre-registration (symbol list, pass rule, tech-only scope caveat).
public sealed class MultiSymbolGeneralizationExperiment
{
    //private static readonly string[] Symbols =
    //    {"aapl", "amzn", "googl","meta","msft","nvda"};
    // TEMP-AUDIT
    private static readonly string[] Symbols =
        {"amzn", "meta", "nvda"};

    private const int MinTradesForConfidence = 10;
    private const int RequiredPasses = 4; // pre-registered pass bar, see handoff doc
    private const decimal StartingCapital = 10_000m;
    private sealed record SymbolResult(
    string Symbol,
    double BaseObvAuc,
    double CandidateAuc,
    double BaseObvCagr,
    double CandidateCagr,
    decimal BaseObvProfitFactor,
    decimal CandidateProfitFactor,
    decimal BaseObvMaxDrawdown,
    decimal CandidateMaxDrawdown,
    int BaseObvTrades,
    int CandidateTrades)
    {
        public bool LowConfidence =>
            BaseObvTrades < MinTradesForConfidence || CandidateTrades < MinTradesForConfidence;

        // Per-symbol pass rule, pre-registered: ALL four conditions must
        // hold simultaneously. "Outperform" is deliberately not vague here.
        public bool Passes =>
            !double.IsNaN(CandidateCagr) && !double.IsNaN(BaseObvCagr) &&
            CandidateAuc > BaseObvAuc &&
            CandidateCagr > BaseObvCagr &&
            CandidateProfitFactor >= BaseObvProfitFactor &&
            CandidateMaxDrawdown <= BaseObvMaxDrawdown;
    }

    public void Run()
    {
        LocalCsvParser parser = new();
        FeatureGenerator featureGenerator = new();
        ModelTrainer modelTrainer = new();

        List<SymbolResult> results = new();

        Console.WriteLine("====================================================================");
        Console.WriteLine(" MULTI-SYMBOL GENERALIZATION STUDY — BaseObv vs BaseObvPriceZScore");
        Console.WriteLine("====================================================================");

        foreach (var symbol in Symbols)
        {
            string csvPath = Path.Combine
                (AppContext.BaseDirectory, "Data", $"{symbol}.csv");
            if (!File.Exists(csvPath))
            {
                Console.WriteLine($"[SKIP] {symbol.ToUpper()}: CSV not found at {csvPath}");
                continue;
            }

            Console.WriteLine($"\n--- {symbol.ToUpper()} ---");

            var marketData = parser.ParseFile(csvPath);
            var trainingData = featureGenerator
                .ComputeTrainingRows(marketData);
            Console.WriteLine($"{trainingData.Count} training rows computed.");

            var baseObvResult = modelTrainer.TrainTournament(
                symbol.ToUpper(),
                trainingData,
                FeatureSets.BaseObvFeatures,
                FeatureSetType.BaseObv.ToString(),
                saveModel: false);

            var candidateResult = modelTrainer.TrainTournament(
                symbol.ToUpper(),
                trainingData,
                FeatureSets.BaseObvPriceZScoreFeatures,
                FeatureSetType.BaseObvPriceZScore.ToString(),
                saveModel: false);

            Console.WriteLine($"BaseObv        — AUC: {baseObvResult.Auc:F4} ({baseObvResult.ModelName})");
            Console.WriteLine($"BaseObvPriceZ  — AUC: {candidateResult.Auc:F4} ({candidateResult.ModelName})");

            var baseObvMetrics =
                RunBacktest(baseObvResult, marketData);
            var candidateMetrics =
                RunBacktest(candidateResult, marketData);

            //TEMP-AUDIT
            var strategy = new MlStrategy(
                candidateResult.Model,
                name: symbol,
                allocationPerTrade: 2000m,
                confidenceMinPct: 0.10m,
                confidenceMaxPct: 0.30m,
                diagnosticMode: true);
            var engine = new BacktestEngine();
            engine.RegisterStrategy(strategy, 10_000m);
            engine.RunSimulation(marketData);
            strategy.PrintDiagnosticSummary(engine.GetAccountState(strategy));
            //---

            results.Add(new SymbolResult(
                Symbol: symbol.ToUpper(),
                BaseObvAuc: baseObvResult.Auc,
                CandidateAuc: candidateResult.Auc,
                BaseObvCagr: baseObvMetrics.Cagr,
                CandidateCagr: candidateMetrics.Cagr,
                BaseObvProfitFactor: baseObvMetrics.ProfitFactor ?? 0m,
                CandidateProfitFactor: candidateMetrics.ProfitFactor ?? 0m,
                BaseObvMaxDrawdown: baseObvMetrics.MaxDrawdownPercent ?? 0m,
                CandidateMaxDrawdown: candidateMetrics.MaxDrawdownPercent ?? 0m,
                BaseObvTrades: baseObvMetrics.TradeCount,
                CandidateTrades: candidateMetrics.TradeCount));

        }
        PrintFinalReport(results);
    }


    // No temp files, no file I/O — MlStrategy's in-memory constructor
    // consumes TrainedModelResult.Model directly.
    private StrategyMetrics RunBacktest(
        TrainedModelResult trained,
        IReadOnlyList<MarketData> marketData)
    {
        MlStrategy strategy = new(
            trained.Model,
            name: $"{trained.Symbol}-{trained.FeatureSetName}",
            allocationPerTrade: 2000m);

        TournamentRunner runner = new(StartingCapital);
        var runResults = runner.Run(
            new List<IStrategy> { strategy },
            marketData);
        var result = runResults[0];

        return MetricsCalculator.Calculate(
            result.Trades,
            result.StartingCapital,
            result.EndingPortfolioValue,
            result.FirstBarTimestamp,
            result.LastBarTimestamp,
            result.EquityCurve);
    }
    private void PrintFinalReport(List<SymbolResult> results)
    {
        Console.WriteLine("\n====================================================================");
        Console.WriteLine(" PER-SYMBOL RESULTS (unfiltered — every result reported)");
        Console.WriteLine("====================================================================\n");

        Console.WriteLine($"{"Symbol",-8}{"AUC(Base)",-11}{"AUC(Cand)",-11}{"CAGR(Base)",-12}{"CAGR(Cand)",-12}" +
            $"{"PF(Base)",-10}{"PF(Cand)",-10}{"DD(Base)",-10}{"DD(Cand)",-10}{"Trades",-16}{"Pass?"}");

        int passCount = 0;
        int lowConfidenceCount = 0;

        foreach (var r in results)
        {
            string tradesCol = $"{r.BaseObvTrades}/{r.CandidateTrades}";
            string passCol = r.LowConfidence ? "LOW-CONF" : (r.Passes ? "PASS" : "FAIL");

            if (r.LowConfidence)
                lowConfidenceCount++;
            else if (r.Passes)
                passCount++;

            Console.WriteLine(
                $"{r.Symbol,-8}{r.BaseObvAuc,-11:F4}{r.CandidateAuc,-11:F4}" +
                $"{r.BaseObvCagr,-12:F2}{r.CandidateCagr,-12:F2}" +
                $"{r.BaseObvProfitFactor,-10:F2}{r.CandidateProfitFactor,-10:F2}" +
                $"{r.BaseObvMaxDrawdown,-10:F2}{r.CandidateMaxDrawdown,-10:F2}" +
                $"{tradesCol,-16}{passCol}");
        }

        int eligibleCount = results.Count - lowConfidenceCount;

        Console.WriteLine("\n====================================================================");
        Console.WriteLine($" STUDY TALLY: {passCount} passed / {eligibleCount} eligible " +
            $"({lowConfidenceCount} low-confidence, excluded from tally, reported above)");
        Console.WriteLine("====================================================================");
        Console.WriteLine();
        Console.WriteLine(passCount >= RequiredPasses
            ? $"FEATURE CHAMPION: BaseObvPriceZScoreFeatures passes the pre-registered bar (>={RequiredPasses}/6)."
            : $"STUDY RESULT: BaseObvPriceZScoreFeatures does NOT pass the pre-registered bar (>={RequiredPasses}/6).");
        Console.WriteLine("No model champion changes are implied by this result — see handoff doc.");
        Console.WriteLine("Scope caveat: all 6 symbols are large-cap tech; this result does not");
        Console.WriteLine("establish generalization beyond that sector (see Future Improvement).");

    }
}
