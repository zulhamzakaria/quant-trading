using QuantTrading.Domain.Models;
using System.Globalization;

namespace QuantTrading.Infrastructure.Data;

public sealed class LocalCsvParser
{

    public IReadOnlyList<MarketData> ParseFile(string filePath)
    {
        var bars = new List<MarketData>();

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Historical data file not found at: {filePath}");
        }

        using var reader = new StreamReader(filePath);

        // Skip the header row: DATE;OPEN;HIGH;LOW;CLOSE;VOLUME
        reader.ReadLine();

        string symbol = Path.GetFileNameWithoutExtension(filePath).ToUpperInvariant();

        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            // stooq uses semicolons as delimiters, so split on ';'
            string[] tokens = line.Split(';');
            if (tokens.Length < 6) continue;

            if (!DateTime.TryParseExact(tokens[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) continue;
            if (!decimal.TryParse(tokens[1], CultureInfo.InvariantCulture, out var open)) continue;
            if (!decimal.TryParse(tokens[2], CultureInfo.InvariantCulture, out var high)) continue;
            if (!decimal.TryParse(tokens[3], CultureInfo.InvariantCulture, out var low)) continue;
            if (!decimal.TryParse(tokens[4], CultureInfo.InvariantCulture, out var close)) continue;
            if (!long.TryParse(tokens[5], CultureInfo.InvariantCulture, out var volume)) continue;

            bars.Add(new MarketData(
                Symbol: symbol,
                Timestamp: date,
                Open: open,
                High: high,
                Low: low,
                Close: close,
                Volume: volume
            ));
        }

        return bars.OrderBy(b => b.Timestamp).ToList();
    }
}

