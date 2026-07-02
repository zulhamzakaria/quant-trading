using QuantTrading.Shared.Models;
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

        string symbol =
            Path.GetFileNameWithoutExtension(filePath)
            .ToUpperInvariant();

        int lineNumber = 1;
        int skippedRows = 0;

        while (reader.ReadLine() is { } line)
        {
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] tokens = line.Split(',');
            if (tokens.Length < 6)
            {
                LogSkipped(lineNumber, line, "insufficient columns");
                skippedRows++;
                continue;
            }
            ;

            if (!DateTime.TryParseExact(tokens[0], "d/M/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                LogSkipped(lineNumber, line, "invalid date format");
                skippedRows++;
                continue;
            }
            ;

            if (!decimal.TryParse(tokens[1], CultureInfo.InvariantCulture, out var open) ||
                !decimal.TryParse(tokens[2], CultureInfo.InvariantCulture, out var high) ||
                !decimal.TryParse(tokens[3], CultureInfo.InvariantCulture, out var low) ||
                !decimal.TryParse(tokens[4], CultureInfo.InvariantCulture, out var close) ||
                !decimal.TryParse(tokens[5], CultureInfo.InvariantCulture, out var volume))
            {
                LogSkipped(lineNumber, line, "non-numeric OHLCV value");
                skippedRows++;
                continue;
            }


            if (open <= 0 || high <= 0 || low <= 0 || close <= 0 || volume < 0)
            {
                LogSkipped(lineNumber, line,
                    $"invalid OHLCV values (O={open} H={high} L={low} C={close} V={volume})");
                skippedRows++;
                continue;
            }

            if (high < low ||
                high < open ||
                high < close ||
                low > open ||
                low > close)
            {
                LogSkipped(lineNumber, line,
                    $"impossible OHLC structure (O={open} H={high} L={low} C={close})");
                skippedRows++;
                continue;
            }

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

        if (skippedRows > 0)
            Console.WriteLine(
                $"[PARSER WARNING] {skippedRows} row(s) skipped in '{filePath}'. " +
                $"Review dataset integrity.");


        return bars.OrderBy(b => b.Timestamp).ToList();
    }

    private static void LogSkipped
        (int lineNumber, string line, string reason)
    {
        // Truncate long lines in the log to keep output readable.
        string preview = line.Length > 60 ? line[..60] + "..." : line;
        Console.WriteLine(
            $"[PARSER SKIP] Line {lineNumber}: {reason} — \"{preview}\"");
    }

}

