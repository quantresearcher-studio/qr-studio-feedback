using System;
using QuantResearch.Engine;
using QuantResearch.Strategies;
namespace StudioExamples;
// Independently authored educational example. No profitability or execution guarantee.
// Register timeframe history once in OnStart. Completed candles update automatically.
// Earlier selected data and compatible live history warm prices without trading.
// OnBarClosed runs once per completed timeframe candle; no timestamp guard is needed.
public sealed class TradeMomentum : Robot
{

    [Parameter("Symbol", Key = "Symbol", Group = "Trading", DefaultValue = "DEMO")]
    public string SymbolName { get; set; } = "DEMO";

    // Keep Symbol metadata bound to this configured instrument in every callback.
    protected override string? SymbolNameOverride => SymbolName;

    [Parameter("Quantity", Group = "Trading", DefaultValue = 1, MinValue = 1, MaxValue = 1000000, Step = 1)]
    public int Quantity { get; set; } = 1;

    private StrategyBarSeries bars = null!;
    private bool longSignal;

    [Parameter("Lookback", Group = "Signal", DefaultValue = 20, MinValue = 2, MaxValue = 1000, Step = 1)]
    public int Lookback { get; set; }

    [Parameter("ThresholdBps", Group = "Signal", DefaultValue = "5", MinValue = "0.01", MaxValue = "10000", Step = "0.01")]
    public decimal ThresholdBps { get; set; }

    // Minutes per completed candle: 1, 5, 15, 30, 60, or another whole-minute divisor of 1440.

    [Parameter("Timeframe minutes", Group = "Timeframe", DefaultValue = 1, MinValue = 1, MaxValue = 1440, Step = 1)]
    public int TimeframeMinutes { get; set; } = 1;

    public override TimeSpan BarTimeframe => TimeSpan.FromMinutes(TimeframeMinutes);
    public override BarPriceSource BarSource => BarPriceSource.Trade;

    public override void OnStart(ReplayContext context)
    {
        if (string.IsNullOrWhiteSpace(SymbolName) || SymbolName != SymbolName.Trim())
            throw new ArgumentException("Set Symbol to the selected data symbol.");
        bars = Indicators.Bars(BarTimeframe, Lookback + 1, SymbolName, BarPriceSource.Trade);
        longSignal = false;
    }
    public override void OnBarClosed(ReplayContext context, BarClose bar)
    {
        if (bar.Symbol != SymbolName) return;
        if (bars.Closed.Count == Lookback + 1 && bars.Closed[0].Close != 0m)
        {
            var changeBps = (bar.Close / bars.Closed[0].Close - 1m) * 10000m;
            if (changeBps > ThresholdBps) longSignal = true;
            else if (changeBps < -ThresholdBps) longSignal = false;
            SetTargetPosition(SymbolName, longSignal ? Quantity : 0m);
        }
    }

}
