using System;
using QuantResearch.Engine;
using QuantResearch.Strategies;
namespace StudioExamples;
// Independently authored educational example. No profitability or execution guarantee.
// Register once in OnStart: completed candles update both SMAs automatically.
// Prior selected replay data and compatible live history warm indicators automatically.
// IsReady protects against insufficient history; OnBarClosed runs once per candle.
// Choose separate timeframes in minutes. No Update call is needed.
// SetTargetPosition handles pending orders; bar-based orders fill on a later eligible observation.
public sealed class MovingAverageCrossover : Robot
{
    private BarIndicator<SimpleMovingAverage> sma_fast = null!;
    private BarIndicator<SimpleMovingAverage> sma_slow = null!;

    [Parameter("Symbol", Key = "Symbol", Group = "Trading", DefaultValue = "DEMO")]
    public string SymbolName { get; set; } = "DEMO";

    // Keep Symbol metadata bound to this configured instrument in every callback.
    protected override string? SymbolNameOverride => SymbolName;

    [Parameter("Quantity", Group = "Trading", DefaultValue = 1, MinValue = 1, MaxValue = 1000000, Step = 1)]
    public int Quantity { get; set; } = 1;

    [Parameter("Fast", Group = "Signal", DefaultValue = 10, MinValue = 1, MaxValue = 1000, Step = 1)]
    public int Fast { get; set; }

    [Parameter("Slow", Group = "Signal", DefaultValue = 30, MinValue = 1, MaxValue = 1000, Step = 1)]
    public int Slow { get; set; }

    [Parameter("Fast timeframe minutes", Group = "Timeframe", DefaultValue = 1, MinValue = 1, MaxValue = 1440, Step = 1)]
    public int FastTimeframeMinutes { get; set; } = 1;

    [Parameter("Slow timeframe minutes", Group = "Timeframe", DefaultValue = 1, MinValue = 1, MaxValue = 1440, Step = 1)]
    public int SlowTimeframeMinutes { get; set; } = 1;

    public override TimeSpan BarTimeframe => TimeSpan.FromMinutes(FastTimeframeMinutes);

    public override void OnStart(ReplayContext context)
    {
        sma_fast = Indicators.SimpleMovingAverage(TimeSpan.FromMinutes(FastTimeframeMinutes), Fast, SymbolName);
        sma_slow = Indicators.SimpleMovingAverage(TimeSpan.FromMinutes(SlowTimeframeMinutes), Slow, SymbolName);

        if (string.IsNullOrWhiteSpace(SymbolName) || SymbolName != SymbolName.Trim())
            throw new ArgumentException("Set Symbol to the selected data symbol.");
        // Compare the configured averages as entered; equal settings stay flat.
    }
    public override void OnBarClosed(ReplayContext context, BarClose bar)
    {
        if (bar.Symbol != SymbolName) return;
        if (!sma_fast.IsReady || !sma_slow.IsReady) return;

        if (sma_fast.Close > sma_slow.Close)
        {
            SetTargetPosition(SymbolName, Quantity); // Buy up to the desired position.
        }
        else
        {
            SetTargetPosition(SymbolName, 0m); // Sell the long position and stay flat.
        }
    }

}
