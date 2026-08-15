using FluentAssertions;
using QuantTrading.Infrastructure.Data;

namespace QuantTrading.Test.Infrastructure.Data;

public class LocalCsvParserTests
{
    [Trait("Category", "Business Rule")]
    [Fact]
    public void Given_ValidCsvRows_When_FileIsParsed_Then_MarketDataFieldsMatchExactly()
    {
        // Arrange
        string path = Path.Combine(Path.GetTempPath(), "TEST_AAPL.csv");
        File.WriteAllLines(path, new[]
        {
            "DATE,OPEN,HIGH,LOW,CLOSE,VOLUME",
            "1/3/2024,100.50,105.25,99.75,102.00,1000000",
            "2/3/2024,102.00,110.00,101.50,108.75,1200000",
        });

        try
        {
            var parser = new LocalCsvParser();

            // Act
            var bars = parser.ParseFile(path);

            // Assert
            bars.Should().HaveCount(2);

            bars[0].Symbol.Should().Be("TEST_AAPL");
            bars[0].Timestamp.Should().Be(new DateTime(2024, 3, 1));
            bars[0].Open.Should().Be(100.50m);
            bars[0].High.Should().Be(105.25m);
            bars[0].Low.Should().Be(99.75m);
            bars[0].Close.Should().Be(102.00m);
            bars[0].Volume.Should().Be(1_000_000m);

            bars[1].Timestamp.Should().Be(new DateTime(2024, 3, 2));
            bars[1].Close.Should().Be(108.75m);
        }
        finally
        {
            File.Delete(path);
        }
    }
    [Trait("Category", "Business Rule")]
    [Fact]
    public void Given_FirstLineIsValidLookingData_When_FileIsParsed_Then_ItIsStillSkippedAsHeader()
    {
        // Arrange
        string path = Path.Combine(Path.GetTempPath(), "TEST_HEADER.csv");
        File.WriteAllLines(path, new[]
        {
        "1/1/2024,100,105,99,102,1000000", // valid-looking row, but it's line 1 -> treated as header, discarded
        "2/1/2024,102,110,101,108,1200000", // the only row that should survive
    });

        try
        {
            var parser = new LocalCsvParser();

            // Act
            var bars = parser.ParseFile(path);

            // Assert
            bars.Should().HaveCount(1);
            bars[0].Timestamp.Should().Be(new DateTime(2024, 1, 2));
        }
        finally
        {
            File.Delete(path);
        }
    }
    [Trait("Category", "Financial Invariant")]
    [Fact]
    public void Given_VariousMalformedRows_When_FileIsParsed_Then_EachIsSkippedAndTheValidRowSurvives()
    {
        // Arrange
        string path = Path.Combine(Path.GetTempPath(), "TEST_MALFORMED.csv");
        File.WriteAllLines(path, new[]
        {
        "DATE,OPEN,HIGH,LOW,CLOSE,VOLUME",
        "1/1/2024,100,105",                          // insufficient columns
        "not-a-date,100,105,99,102,1000000",         // invalid date format
        "3/1/2024,abc,105,99,102,1000000",           // non-numeric value
        "4/1/2024,-100,105,99,102,1000000",          // non-positive OHLC
        "5/1/2024,100,50,99,102,1000000",            // impossible structure (High < Open)
        "6/1/2024,100,105,99,102,1000000",           // the only valid row
    });

        try
        {
            var parser = new LocalCsvParser();

            // Act
            var bars = parser.ParseFile(path);

            // Assert
            bars.Should().HaveCount(1);
            bars[0].Timestamp.Should().Be(new DateTime(2024, 1, 6));
        }
        finally
        {
            File.Delete(path);
        }
    }
    [Trait("Category", "Financial Invariant")]
    [Fact]
    public void Given_RowsOutOfChronologicalOrderInTheFile_When_FileIsParsed_Then_OutputIsSortedByTimestamp()
    {
        // Arrange
        string path = Path.Combine(Path.GetTempPath(), "TEST_UNSORTED.csv");
        File.WriteAllLines(path, new[]
        {
        "DATE,OPEN,HIGH,LOW,CLOSE,VOLUME",
        "3/1/2024,100,105,99,102,1000000",
        "1/1/2024,100,105,99,102,1000000",
        "2/1/2024,100,105,99,102,1000000",
    });

        try
        {
            var parser = new LocalCsvParser();

            // Act
            var bars = parser.ParseFile(path);

            // Assert
            bars.Should().HaveCount(3);
            bars[0].Timestamp.Should().Be(new DateTime(2024, 1, 1));
            bars[1].Timestamp.Should().Be(new DateTime(2024, 1, 2));
            bars[2].Timestamp.Should().Be(new DateTime(2024, 1, 3));
        }
        finally
        {
            File.Delete(path);
        }
    }
}