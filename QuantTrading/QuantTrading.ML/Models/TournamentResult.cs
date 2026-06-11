namespace QuantTrading.ML.Models;

public sealed record TournamentResult(
    string Name,
    double Accuracy,
    double AUC,
    double F1Score);
