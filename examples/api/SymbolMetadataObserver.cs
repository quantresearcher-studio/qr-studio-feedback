using System;
using QuantResearch.Engine;
using QuantResearch.Strategies;

namespace QrExamples;

// Read-only API example. Requires a selected instrument with tick size and exchange zone.
// These callbacks print observations; they do not submit orders.
public sealed class SymbolMetadataObserver : Robot
{
    [Parameter("Symbol", Key = "Symbol", Group = "Data", DefaultValue = "DEMO")]
    public string SymbolName { get; set; } = "DEMO";

    protected override string? SymbolNameOverride => SymbolName;

    public override void OnStart(ReplayContext context)
    {
        Print($"Symbol={Symbol.Name}, Tick={Symbol.TickSize}, ExchangeZone={Symbol.TimeZone.Id}");
    }

    public override void OnBarClosed(ReplayContext context, BarClose bar)
    {
        if (bar.Symbol != SymbolName) return;
        var local = TimeZoneInfo.ConvertTime(bar.Timestamp, Symbol.TimeZone);
        var buyPrice = Symbol.NormalizePrice(bar.Close, OrderSide.Buy);
        var sellPrice = Symbol.NormalizePrice(bar.Close, OrderSide.Sell);
        Print($"Local={local:O}, Close={bar.Close}, BuyGrid={buyPrice}, SellGrid={sellPrice}");
    }

    public override void OnQuote(ReplayContext context, Quote quote)
    {
        if (quote.Symbol != SymbolName || quote.Bid <= 0m || quote.Ask <= 0m) return;
        // Move five valid price levels outward; normalization uses the host's current rules.
        var bid = Symbol.NormalizePrice(quote.Bid, OrderSide.Buy, levelsAway: 5);
        var ask = Symbol.NormalizePrice(quote.Ask, OrderSide.Sell, levelsAway: 5);
        Print($"Local={Symbol.ToLocalTime(quote.Timestamp):O}, BidLevel={bid}, AskLevel={ask}");
    }
}
