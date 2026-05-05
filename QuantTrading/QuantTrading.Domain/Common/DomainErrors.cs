namespace QuantTrading.Domain.Common;

public static class DomainErrors
{
    public static class Portfolio
    {
        public static readonly Error NotFound
            = new("Portfolio.NotFound", "The specified portfolio was not found.");
        public static readonly Error InsufficientFunds
            = new("Portfolio.InsufficientFunds", "The portfolio does not have sufficient funds for this operation.");
    }
    public static class Trade
    {
        public static readonly Error InvalidSymbol
            = new("Trade.InvalidSymbol", "The symbol provided for the trade is invalid.");
        public static readonly Error InvalidQuantity
            = new("Trade.InvalidQuantity", "The quantity provided for the trade is invalid.");
        public static readonly Error InvalidPrice
            = new("Trade.InvalidPrice", "The price provided for the trade is invalid.");
        public static readonly Error InvalidSide 
            = new("Trade.InvalidSide", "The TradeSide provided for the trade is invalid.");
        public static readonly Error MarketClosed 
            = new("Trade.MarketClosed", "Cannot execute trade because the target market is currently closed.");
    }
}
