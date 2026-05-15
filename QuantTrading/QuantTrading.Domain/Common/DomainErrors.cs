namespace QuantTrading.Domain.Common;

public static class DomainErrors
{
    public static class PortfolioError
    {
        public static readonly Error NotFound
            = new("Portfolio.NotFound", "The specified portfolio was not found.");
        public static readonly Error InsufficientFunds
            = new("Portfolio.InsufficientFunds", "The portfolio does not have sufficient funds for this operation.");
    }
    public static class TradeError
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

    public static class MoneyError
    {
        // Static Method
        //public static Error Required()
        //    => new("Money.Required", "Amount/Currency are required.");
        //Static Field
         public static Error Required
            => new("Money.Required", "Amount/Currency are required.");
        public static readonly Error CurrencyMismatch
            = new("Money.CurrencyMismatched", "Currency must match.");
    }

    public static class CurrencyError
    {
        public static readonly Error InvalidCode
            = new("Currency.InvalidCode", "Invalid Currency Code.");
    }
}
