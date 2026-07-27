using Microsoft.ML;

public sealed record TrainedModelResult(
    double Auc,
    string ModelName,
    ITransformer Model,      // non-nullable — guaranteed non-null by the throw above
    DataViewSchema Schema,   // non-nullable — same guarantee
    string Symbol,           // new, per GPT's second suggestion
    string FeatureSetName);  // new, per GPT's second suggestion