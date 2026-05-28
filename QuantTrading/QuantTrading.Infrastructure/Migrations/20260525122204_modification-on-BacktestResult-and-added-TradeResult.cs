using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantTrading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class modificationonBacktestResultandaddedTradeResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BacktestResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InitialCapitalAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    InitialCapitalCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    FinalCapitalAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    FinalCapitalCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    GrossProfitAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    GrossProfitCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    GrossLossAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    GrossLossCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BacktestResults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TradeRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BacktestResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    EntryPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    EnteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExitPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    ExitedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RealizedPnL = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeRecords_BacktestResults_BacktestResultId",
                        column: x => x.BacktestResultId,
                        principalTable: "BacktestResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TradeRecords_BacktestResultId",
                table: "TradeRecords",
                column: "BacktestResultId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TradeRecords");

            migrationBuilder.DropTable(
                name: "BacktestResults");
        }
    }
}
