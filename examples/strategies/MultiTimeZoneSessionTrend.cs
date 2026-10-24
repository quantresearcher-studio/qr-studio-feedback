using System;
using QuantResearch.Engine;
using QuantResearch.Strategies;
namespace StudioExamples;
// Independently authored educational example. No profitability or execution guarantee.
// Register timeframe history once in OnStart. Completed candles update automatically.
// Earlier selected data and compatible live history warm prices without trading.
// OnBarClosed runs once per completed timeframe candle; no timestamp guard is needed.
public sealed class MultiTimeZoneSessionTrend : Robot
{

    [Parameter("Symbol", Key = "Symbol", Group = "Trading", DefaultValue = "DEMO")]
    public string SymbolName { get; set; } = "DEMO";

    // Keep Symbol metadata bound to this configured instrument in every callback.
    protected override string? SymbolNameOverride => SymbolName;

    [Parameter("Quantity", Group = "Trading", DefaultValue = 1, MinValue = 1, MaxValue = 1000000, Step = 1)]
    public int Quantity { get; set; } = 1;

    private BarIndicator<SimpleMovingAverage> average = null!;
    private TimeZoneInfo tokyo = TimeZoneInfo.Utc, newYork = TimeZoneInfo.Utc;

    [Parameter("Period", Group = "Signal", DefaultValue = 20, MinValue = 2, MaxValue = 1000, Step = 1)]
    public int Period { get; set; }

    [Parameter("AsiaTimeZone", Group = "Session", DefaultValue = "Asia/Tokyo")]
    public string AsiaTimeZone { get; set; } = "Asia/Tokyo";

    [Parameter("AmericaTimeZone", Group = "Session", DefaultValue = "America/New_York")]
    public string AmericaTimeZone { get; set; } = "America/New_York";

    // Minutes per completed candle: 1, 5, 15, 30, 60, or another whole-minute divisor of 1440.

    [Parameter("Timeframe minutes", Group = "Timeframe", DefaultValue = 1, MinValue = 1, MaxValue = 1440, Step = 1)]
    public int TimeframeMinutes { get; set; } = 1;

    public override TimeSpan BarTimeframe => TimeSpan.FromMinutes(TimeframeMinutes);
    public override BarPriceSource BarSource => BarPriceSource.Trade;

    public override void OnStart(ReplayContext context)
    {
        if (string.IsNullOrWhiteSpace(SymbolName) || SymbolName != SymbolName.Trim())
            throw new ArgumentException("Set Symbol to the selected data symbol.");
        average = Indicators.SimpleMovingAverage(BarTimeframe, Period, SymbolName, BarPriceSource.Trade);
        tokyo = TimeZoneInfo.FindSystemTimeZoneById(AsiaTimeZone);
        newYork = TimeZoneInfo.FindSystemTimeZoneById(AmericaTimeZone);
    }
    public override void OnBarClosed(ReplayContext context, BarClose bar)
    {
        if (bar.Symbol != SymbolName) return;
        if (!InSession(bar.Timestamp, tokyo, 540, 930) && !InSession(bar.Timestamp, newYork, 570, 960))
        { SetTargetPosition(SymbolName, 0m); return; }
        // Prices warm across sessions; the named-zone schedule controls trading, not indicator history.
        if (average.IsReady) SetTargetPosition(SymbolName, bar.Close > average.Close ? Quantity : 0m);
    }
    private static bool InSession(DateTimeOffset utc, TimeZoneInfo zone, int startMinute, int endMinute)
    {
        var local = TimeZoneInfo.ConvertTime(utc, zone);
        var minute = local.Hour * 60 + local.Minute;
        return local.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && minute >= startMinute && minute < endMinute;
    }
}
