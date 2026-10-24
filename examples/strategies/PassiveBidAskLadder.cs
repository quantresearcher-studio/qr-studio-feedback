using System;
using System.Linq;
using System.Globalization;
using QuantResearch.Engine;
using QuantResearch.Strategies;
namespace StudioExamples;
// Independently authored educational example. No profitability or execution guarantee.
// Signals use observations already delivered; the engine fills only on later eligible events.
public sealed class PassiveBidAskLadder : Robot
{
    // Keep the saved key "Symbol" while freeing the instrument API for direct use.
    [Parameter("Symbol", Key = "Symbol", Group = "Trading", DefaultValue = "DEMO")]
    public string SymbolName { get; set; } = "DEMO";

    // Bind metadata during startup and clock callbacks as well as quote callbacks.
    protected override string? SymbolNameOverride => SymbolName;

    [Parameter("Levels", Group = "Signal", DefaultValue = 2, MinValue = 1, MaxValue = 5, Step = 1)]
    public int Levels { get; set; }

    [Parameter("QuantityPerLevel", Group = "Signal", DefaultValue = 1, MinValue = 1, MaxValue = 1000000, Step = 1)]
    public int QuantityPerLevel { get; set; }

    [Parameter("MaximumInventory", Group = "Signal", DefaultValue = 4, MinValue = 1, MaxValue = 1000000, Step = 1)]
    public int MaximumInventory { get; set; }

    [Parameter("RefreshEveryQuotes", Group = "Signal", DefaultValue = 1, MinValue = 1, MaxValue = 10000, Step = 1)]
    public int RefreshEveryQuotes { get; set; }

    [Parameter("TickSize", Group = "Signal", DefaultValue = "0", MinValue = "0", MaxValue = "1000", Step = "0.00000001")]
    public decimal TickSize { get; set; }

    [Parameter("Use local trading hours", Group = "Trading hours", DefaultValue = false)]
    public bool UseTradingHours { get; set; }

    [Parameter("Start local (HH:mm)", Group = "Trading hours", DefaultValue = "09:00")]
    public string TradingStart { get; set; } = "09:00";

    [Parameter("End local (HH:mm, exclusive)", Group = "Trading hours", DefaultValue = "15:30")]
    public string TradingEnd { get; set; } = "15:30";

    [Parameter("Session start days", Group = "Trading hours", DefaultValue = "Monday,Tuesday,Wednesday,Thursday,Friday")]
    public string TradingDays { get; set; } = "Monday,Tuesday,Wednesday,Thursday,Friday";

    [Parameter("Local windows (HH:mm-HH:mm; blank uses start/end)", Group = "Trading hours", DefaultValue = "")]
    public string TradingWindows { get; set; } = "";

    private (TimeSpan Start, TimeSpan End)[] windows = Array.Empty<(TimeSpan, TimeSpan)>();
    private DayOfWeek[] sessionDays = Array.Empty<DayOfWeek>();
    public override bool ManagedOrderScheduleEnabled => UseTradingHours;

    protected override bool IsLimitOrderTimeAllowed(string symbol, DateTimeOffset timestamp)
    {
        // Extra selected instruments have no ladder here; OnQuote ignores them.
        if (symbol != SymbolName) return true;
        return ActiveWindow(timestamp) >= 0;
    }

    private int ActiveWindow(DateTimeOffset timestamp)
    {
        var local = TimeZoneInfo.ConvertTime(timestamp, Symbol.TimeZone);
        for (var i = 0; i < windows.Length; i++)
        {
            var (start, end) = windows[i];
            var time = local.TimeOfDay;
            var day = start >= end && time < end ? local.AddDays(-1).DayOfWeek : local.DayOfWeek;
            var inside = start == end || (start > end ? time >= start || time < end : time >= start && time < end);
            if (inside && sessionDays.Contains(day)) return i;
        }
        return -1;
    }

    protected override string? LimitOrderSessionKey(string symbol, DateTimeOffset timestamp)
    {
        if (symbol != SymbolName) return null;
        var index = ActiveWindow(timestamp);
        if (index < 0) return null;
        var (start, end) = windows[index];
        var zone = Symbol.TimeZone;
        var local = TimeZoneInfo.ConvertTime(timestamp, zone);
        var day = start >= end && local.TimeOfDay < end ? local.Date.AddDays(-1) : local.Date;
        var endDay = start >= end ? day.AddDays(1) : day;
        // Distinct windows also cancel old orders when replay has no event during a break.
        var repeatedBoundary = zone.IsAmbiguousTime(day.Add(start)) || zone.IsAmbiguousTime(endDay.Add(end));
        return day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "/" + index
            + (repeatedBoundary ? "/" + local.Offset.ToString() : "");
    }

    public override bool LimitOrderRefreshOnlyOnChange => true;

    // Optional cadence setting. Robot manages quote limit orders automatically, even without this override.
    public override int LimitOrderRefreshQuotes => RefreshEveryQuotes;

    public override void OnStart(ReplayContext context)
    {
        if (string.IsNullOrWhiteSpace(SymbolName)) throw new ArgumentException("Supply the configured quote symbol.");
        _ = Symbol.Name; // Require the configured instrument to be selected.
        if (!UseTradingHours) return;
        _ = Symbol.TimeZone; // Require an explicit exchange zone before any order can be submitted.
        TimeSpan ParseTime(string value)
        {
            if (!TimeSpan.TryParseExact(value.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var time))
                throw new ArgumentException("Trading hours must use 24-hour HH:mm (00:00 through 23:59).");
            return time;
        }
        if (string.IsNullOrWhiteSpace(TradingWindows))
            windows = new[] { (ParseTime(TradingStart), ParseTime(TradingEnd)) };
        else
        {
            var entries = TradingWindows.Split(';', StringSplitOptions.TrimEntries);
            if (entries.Length > 8) throw new ArgumentException("Supply at most eight local trading windows.");
            windows = entries.Select(entry =>
            {
                var bounds = entry.Split('-', StringSplitOptions.TrimEntries);
                if (bounds.Length != 2) throw new ArgumentException("TradingWindows must use HH:mm-HH:mm separated by semicolons.");
                return (ParseTime(bounds[0]), ParseTime(bounds[1]));
            }).OrderBy(w => w.Item1).ToArray();
        }
        // Compare the daily clock segments, including both sides of midnight. Adjacent bounds are allowed.
        var segments = windows.SelectMany(w => w.Start == w.End ? new[] { (TimeSpan.Zero, TimeSpan.FromDays(1)) }
            : w.Start < w.End ? new[] { (w.Start, w.End) }
            : new[] { (w.Start, TimeSpan.FromDays(1)), (TimeSpan.Zero, w.End) })
            .Where(w => w.Item1 < w.Item2).OrderBy(w => w.Item1).ToArray();
        if (segments.Zip(segments.Skip(1), (a, b) => a.Item2 > b.Item1).Any(overlap => overlap))
            throw new ArgumentException("Trading windows must not overlap; a 24-hour window must stand alone.");
        var names = TradingDays.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (names.Length == 0 || names.Any(n => !Enum.GetNames<DayOfWeek>().Contains(n, StringComparer.OrdinalIgnoreCase)))
            throw new ArgumentException("TradingDays must list full English weekday names separated by commas.");
        sessionDays = names.Select(n => Enum.Parse<DayOfWeek>(n, true)).Distinct().ToArray();
    }
    public override void OnQuote(ReplayContext context, Quote quote)
    {
        if (quote.Symbol != SymbolName) return;
        var position = context.Positions.Where(p => p.Symbol == quote.Symbol).Sum(p => p.Quantity);
        var maximum = MaximumInventory;
        // Zero reads Symbol.TickSize; retain a positive legacy TickSize as an explicit spacing override.
        // Automatic levels walk the instrument price bands through NormalizePrice.
        var tick = TickSize == 0m ? Symbol.TickSize : TickSize;
        if (tick < 0m) throw new InvalidOperationException("The price increment cannot be negative.");
        foreach (var side in new[] { OrderSide.Buy, OrderSide.Sell })
        {
            // Existing net inventory occupies inner levels on its own side, including partial levels.
            // Account inventory includes manual exposure; only this strategy's pending orders are refreshed.
            var occupied = Math.Max(0m, side == OrderSide.Buy ? position : -position);
            var budget = Math.Max(0m, side == OrderSide.Buy ? maximum - position : maximum + position);
            // A long-only broker reserves other strategies' and manual sell orders first.
            if (side == OrderSide.Sell && context.SellLimitCapacity(SymbolName) is { } available) budget = Math.Min(budget, available);
            decimal? previousPrice = null;
            for (var level = 0; level < Levels; level++)
            {
                var capacity = Math.Max(0m, QuantityPerLevel - Math.Clamp(occupied - level * QuantityPerLevel, 0m, QuantityPerLevel));
                var amount = Math.Min(budget, capacity);
                var reference = side == OrderSide.Buy ? quote.Bid : quote.Ask;
                var requested = TickSize > 0m ? PriceGrid.Round(reference, tick, side, level) : reference;
                if (requested <= 0m) continue;
                var price = Symbol.NormalizePrice(requested, side, TickSize > 0m ? 0 : level);
                if (price <= 0m) break;
                // A custom spacing smaller than the broker increment must not collapse two levels to one price.
                if (previousPrice is { } previous && (side == OrderSide.Buy ? price >= previous : price <= previous))
                    price = Symbol.NormalizePrice(previous, side, 1);
                previousPrice = price;
                if (amount <= 0m || price <= 0m) continue;
                budget -= amount;
                context.SubmitLimitOrder(side, SymbolName, amount, price);
            }
        }
    }
}
