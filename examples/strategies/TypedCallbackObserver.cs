using System;
using QuantResearch.Engine;
using QuantResearch.Strategies;

namespace StudioExamples;

public sealed class TypedCallbackObserver : Robot
{
    public long BarsOpened { get; private set; }
    public long BarsClosed { get; private set; }
    public long TicksSeen { get; private set; }
    public long TradesSeen { get; private set; }
    public long QuotesSeen { get; private set; }

    [Parameter("Symbol", Key = "Symbol", Group = "Data", DefaultValue = "DEMO")]
    public string SymbolName { get; set; } = "DEMO";

    // Keep Symbol metadata bound to this configured instrument in every callback.
    protected override string? SymbolNameOverride => SymbolName;

    public override void OnStart(ReplayContext context)
    {
        BarsOpened = BarsClosed = TicksSeen = TradesSeen = QuotesSeen = 0;
    }

    public override void OnBarOpened(ReplayContext context, BarOpen bar)
    {
        if (Matches(bar)) BarsOpened++;
        // bar.Open is known; the final high, low, close and volume are not.
    }

    public override void OnBarClosed(ReplayContext context, BarClose bar)
    {
        if (Matches(bar)) BarsClosed++;
        // bar.Open, High, Low, Close and Volume now describe a completed bar.
    }

    public override void OnTick(ReplayContext context, MarketEvent tick)
    {
        if (!Matches(tick)) return;
        TicksSeen++;
        // Keep the default Trade/Quote dispatch when also overriding OnTick.
        base.OnTick(context, tick);
    }

    public override void OnTrade(ReplayContext context, Trade trade)
    {
        if (Matches(trade)) TradesSeen++;
        // trade.Price is a last-trade observation, not a bid or ask.
    }

    public override void OnQuote(ReplayContext context, Quote quote)
    {
        if (Matches(quote)) QuotesSeen++;
        // quote.Bid and quote.Ask remain distinct; this template sends no orders.
    }

    private bool Matches(MarketEvent observation) =>
        string.Equals(observation.Symbol, SymbolName, StringComparison.Ordinal);
}
