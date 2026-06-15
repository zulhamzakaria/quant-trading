namespace QuantTrading.ML.Models;

public record ExperimentResult(
    string Symbol,
    double BaseAuc,
    double RsiAuc,
    double Delta,
    string BaseWinner,
    string RsiWinner);
