using System.Linq;
using QuantResearch.Engine;
using QuantResearch.Strategies;

namespace UserStrategies;

public sealed class MyStrategy : Robot
{
    [Parameter("Enabled", Group = "Core", DefaultValue = true)]
    public bool Enabled { get; set; }

    public override void OnStart(ReplayContext context)
    {
        // Symbols contains the selected backtest or assigned live instruments.
        // With several instruments, read Symbols.GetSymbol(name) outside callbacks.
    }

    public override void OnBarOpened(ReplayContext context, BarOpen bar)
    {
        // Only the opening price is known here: bar.Open.
        // Use bar.Symbol to trade this instrument; no Symbol parameter is needed.
    }

    public override void OnBarClosed(ReplayContext context, BarClose bar)
    {
        if (!Enabled) return;
        // Completed OHLCV is available here. Add your bar-based logic.
        // Positions and PendingOrders support LINQ filtering by Symbol and Label.
    }

    public override void OnQuote(ReplayContext context, Quote quote)
    {
        if (!Enabled) return;
        // Symbol.Name and Symbol.TickSize describe this callback instrument.
        // Example: var tick = Symbol.TickSize;
        // With an explicit exchange zone: var local = Symbol.ToLocalTime(quote.Timestamp);
        // Symbol.TimeZone is a TimeZoneInfo; UTC event timestamps remain unchanged.
        // Use Symbol.NormalizePrice(price, Buy) for valid limit prices.
        // Example: SubmitMarketOrder(Buy, quote.Symbol, 1m);
    }

    public override void OnTick(ReplayContext context, MarketEvent tick)
    {
        // A Trade or Quote observation arrives here; bars are separate callbacks.
        // Add your tick-based logic. This starter places no orders.
        // Keep default routing if you later add OnTrade/OnQuote overrides.
        base.OnTick(context, tick);
    }

    protected override void OnStop()
    {
        // Read final state through Context here. Stop also runs after cancellation
        // or an error following successful startup. Order changes are not allowed.
    }
}
