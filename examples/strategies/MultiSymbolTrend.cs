using System;
using System.Collections.Generic;
using QuantResearch.Engine;
using QuantResearch.Strategies;
namespace StudioExamples;
// Independently authored educational example. No profitability or execution guarantee.
// Register timeframe history once in OnStart. Completed candles update automatically.
// Earlier selected data and compatible live history warm prices without trading.
// OnBarClosed runs once per completed timeframe candle; no timestamp guard is needed.
public sealed class MultiSymbolTrend : Robot
{

    [Parameter("Symbol", Key = "Symbol", Group = "Trading", DefaultValue = "DEMO")]
    public string SymbolName { get; set; } = "DEMO";

    // Symbol follows the callback instrument; use Symbols.GetSymbol(name) outside callbacks.

    [Parameter("Quantity", Group = "Trading", DefaultValue = 1, MinValue = 1, MaxValue = 1000000, Step = 1)]
    public int Quantity { get; set; } = 1;

    private readonly Dictionary<string, BarIndicator<SimpleMovingAverage>> averages = new(StringComparer.Ordinal);

    [Parameter("OtherSymbol", Group = "Trading", DefaultValue = "DEMO2")]
    public string OtherSymbol { get; set; } = "DEMO2";

    [Parameter("Period", Group = "Signal", DefaultValue = 20, MinValue = 2, MaxValue = 1000, Step = 1)]
    public int Period { get; set; }

    // Minutes per completed candle: 1, 5, 15, 30, 60, or another whole-minute divisor of 1440.

    [Parameter("Timeframe minutes", Group = "Timeframe", DefaultValue = 1, MinValue = 1, MaxValue = 1440, Step = 1)]
    public int TimeframeMinutes { get; set; } = 1;

    public override TimeSpan BarTimeframe => TimeSpan.FromMinutes(TimeframeMinutes);

    public override void OnStart(ReplayContext context)
    {
        if (string.IsNullOrWhiteSpace(SymbolName) || SymbolName != SymbolName.Trim())
            throw new ArgumentException("Set Symbol to the selected data symbol.");
        averages.Clear();
        if (string.IsNullOrWhiteSpace(OtherSymbol) || OtherSymbol == SymbolName || OtherSymbol != OtherSymbol.Trim())
            throw new ArgumentException("OtherSymbol must identify a distinct configured dataset symbol.");
        averages[SymbolName] = Indicators.SimpleMovingAverage(BarTimeframe, Period, SymbolName);
        averages[OtherSymbol] = Indicators.SimpleMovingAverage(BarTimeframe, Period, OtherSymbol);
    }
    public override void OnBarClosed(ReplayContext context, BarClose bar)
    {
        if (!averages.TryGetValue(bar.Symbol, out var average)) return;
        if (average.IsReady) SetTargetPosition(bar.Symbol, bar.Close > average.Close ? Quantity : 0m);
    }

}
