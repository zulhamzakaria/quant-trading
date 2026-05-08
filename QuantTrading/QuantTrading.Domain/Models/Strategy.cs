namespace QuantTrading.Domain.Models;

public sealed record Strategy
{
    public string Name { get; }
    public string Description { get; }
    public IReadOnlyDictionary<string, string> Parameters { get; }

    public Strategy(
        string name,
        string? description,
        Dictionary<string, string>? parameters = null)
    {
        name = name?.Trim();

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Strategy name is required.",
                nameof(name));

        Name = name;
        Description = description?.Trim() ?? string.Empty;
        Parameters = parameters is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(parameters);
    }
}
