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
}