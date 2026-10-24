using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using QuantResearch.Engine;
using QuantResearch.Strategies;
namespace StudioExamples;

// Original fundamentals + price example, using the actually normalized EPS/BPS summary keys.
// EPS/BPS is a disclosed-period earnings-to-book feature, not annualized ROE or a price/earnings ratio.
// Requires same-symbol completed bars and JQuantsV2 Fundamentals information; absent metrics never become zero.
public sealed class JQuantsFundamentalForecast : Robot
{

    [Parameter("Symbol", Key = "Symbol", Group = "Trading", DefaultValue = "83160")]
    public string SymbolName { get; set; } = "83160";

    // Keep Symbol metadata bound to this configured instrument in every callback.
    protected override string? SymbolNameOverride => SymbolName;

    [Parameter("Quantity", Group = "Trading", DefaultValue = 1,
        MinValue = 1, MaxValue = 1000000, Step = 1)]
    public int Quantity { get; set; } = 1;

    [Parameter("TrendLookback", Group = "Forecast", DefaultValue = 20,
        MinValue = 2, MaxValue = 500, Step = 1)]
    public int TrendLookback { get; set; } = 20;

    [Parameter("InformationWeight", Group = "Forecast", DefaultValue = "0.1",
        MinValue = "0", MaxValue = "1", Step = "0.01")]
    public decimal InformationWeight { get; set; } = 0.1m;

    [Parameter("MinimumForecastReturn", Group = "Forecast", DefaultValue = "0.005",
        MinValue = "0", MaxValue = "0.5", Step = "0.001")]
    public decimal MinimumForecastReturn { get; set; } = 0.005m;

    [Parameter("MaximumAbsoluteForecastReturn", Group = "Forecast", DefaultValue = "0.05",
        MinValue = "0.001", MaxValue = "0.5", Step = "0.001")]
    public decimal MaximumAbsoluteForecastReturn { get; set; } = 0.05m;

    [Parameter("MaximumDisclosureAgeDaysAtSignal", Group = "Information timing", DefaultValue = 400,
        MinValue = 1, MaxValue = 3650, Step = 1)]
    public int MaximumDisclosureAgeDaysAtSignal { get; set; } = 400;

    [Parameter("AllowPublicationApproximation", Group = "Information timing", DefaultValue = false)]
    public bool AllowPublicationApproximation { get; set; } = false;

    [Parameter("MinimumEarningsToBook", Group = "Disclosure fields", DefaultValue = "0",
        MinValue = "0", MaxValue = "1", Step = "0.01")]
    public decimal MinimumEarningsToBook { get; set; } = 0m;

    public override void OnStart(ReplayContext context) { Initialize(); ResetInformation(); }
    public override void OnInformation(ReplayContext context, InformationEvent info)
    {
        if (info.Symbol == symbol) Information(context, info, "Fundamentals");
    }

    public override void OnBarClosed(ReplayContext context, BarClose bar)
    {
        if (bar.Symbol == symbol) Forecast(context, bar);
    }
    private bool RequiredMetric(string key) => key is "EPS" or "BPS";
    private bool HasRequiredValues() => disclosureMetrics.TryGetValue("EPS", out _)
        && disclosureMetrics.TryGetValue("BPS", out var book) && book > 0m;
    private decimal Feature() => disclosureMetrics["EPS"] / disclosureMetrics["BPS"];
    private bool FeatureAllowed(decimal value) => value > MinimumEarningsToBook;

    private DateTimeOffset? disclosurePublished;
    private string disclosureIdentity = "";
    private DateTimeOffset disclosureAvailable;
    private bool disclosureEligible;
    private readonly Dictionary<string, decimal> disclosureMetrics = new(StringComparer.Ordinal);
    public decimal? LastForecastReturn { get; private set; }
    public decimal? LastForecastPrice { get; private set; }
    public string LastInputStatus { get; private set; } = "Waiting for a complete disclosure and price bars";

    private void ResetInformation()
    {
        disclosurePublished = null; disclosureIdentity = ""; disclosureAvailable = default;
        disclosureEligible = false; disclosureMetrics.Clear();
        LastForecastReturn = null; LastForecastPrice = null;
        LastInputStatus = "Waiting for a complete disclosure and price bars";
    }

    private void Information(ReplayContext context, InformationEvent info, string kind)
    {
        if (info.Provider != "JQuantsV2" || info.Category != InformationCategory.Financial) return;
        string prefix = kind + ":" + symbol + ":";
        if (!info.EventId.StartsWith(prefix, StringComparison.Ordinal)) return;
        int colon = info.EventId.LastIndexOf(':');
        string batch = info.EventId[(colon + 1)..];
        if (colon <= prefix.Length || batch.Length != 4 || batch.Any(c => c < '0' || c > '9'))
        {
            disclosureEligible = false; disclosureMetrics.Clear(); CancelPendingEntries(context); return;
        }
        if (disclosurePublished is { } previous && info.PublishedUtc < previous) return;
        string identity = info.EventId[..colon];
        // Batches may share publication time. Never stitch metrics from different disclosures or capture times.
        if (identity != disclosureIdentity || info.AvailableUtc != disclosureAvailable || info.PublishedUtc != disclosurePublished)
        {
            disclosureIdentity = identity; disclosureAvailable = info.AvailableUtc;
            disclosurePublished = info.PublishedUtc; disclosureMetrics.Clear();
            disclosureEligible = AcceptedTiming(info);
            LastForecastReturn = null; LastForecastPrice = null;
            CancelPendingEntries(context);
        }
        if (!AcceptedTiming(info)) disclosureEligible = false;
        foreach (var metric in info.Metrics)
        {
            if (!RequiredMetric(metric.Key)) continue;
            if (disclosureMetrics.TryGetValue(metric.Key, out var existing) && existing != metric.Value)
            { disclosureEligible = false; continue; } // Conflicting duplicate batch is not a usable disclosure.
            disclosureMetrics[metric.Key] = metric.Value;
        }
        LastInputStatus = "Disclosure observed; required fields checked at the next completed bar";
        if (!disclosureEligible || !HasRequiredValues()) CancelPendingEntries(context);
    }

    private bool InformationRecent(DateTimeOffset now) => disclosureEligible
        && IsRecent(now, disclosurePublished, checked(MaximumDisclosureAgeDaysAtSignal * 24));

    private void Forecast(ReplayContext context, BarClose bar)
    {
        int lookback = TrendLookback;
        AddClose(bar.Close, lookback);
        LastForecastReturn = null; LastForecastPrice = null;
        if (prices.Count < lookback || !InformationRecent(bar.Timestamp) || !HasRequiredValues())
        {
            LastInputStatus = "Missing, excluded, or stale required disclosure; or price warm-up incomplete";
            Target(context, 0m); return;
        }
        decimal target = 0m;
        try
        {
            // Explicit one-next-bar heuristic: linearized price trend plus the current disclosure feature.
            // Not fitted ML, not an analyst target, and not a forecast of guaranteed profit.
            decimal trend = (bar.Close / prices.Peek() - 1m) / (lookback - 1);
            decimal feature = Feature();
            decimal maximum = MaximumAbsoluteForecastReturn;
            decimal forecast = Math.Clamp(checked(trend + InformationWeight * feature), -maximum, maximum);
            LastForecastReturn = forecast;
            LastForecastPrice = checked(bar.Close * (1m + forecast));
            LastInputStatus = "One-next-bar rule forecast available from observed inputs";
            target = FeatureAllowed(feature) && forecast > MinimumForecastReturn ? quantity : 0m;
        }
        catch (OverflowException)
        {
            LastForecastReturn = null; LastForecastPrice = null;
            LastInputStatus = "Forecast arithmetic unavailable";
        }
        Target(context, target);
    }

    // Only this strategy's order IDs are cancelled. Pending remaining quantities prevent duplicate entries.
    // Orders use the engine's later-observation fill model; information is not an executable price.
    private sealed class OrderReservation(bool seenPending)
    {
        public bool SeenPending { get; set; } = seenPending;
        public bool CancelSent { get; set; }
    }
    private readonly Dictionary<long, OrderReservation> ownedOrders = new();
    public string OrderState { get; private set; } = "No owned orders";
    private string symbol = "";
    private decimal quantity;
    private bool allowPublicationApproximation;
    private readonly Queue<decimal> prices = new();

    private void Initialize()
    {
        symbol = SymbolName;
        if (string.IsNullOrWhiteSpace(symbol) || symbol != symbol.Trim())
            throw new ArgumentException("Choose the exact common price/information dataset symbol.");
        quantity = Quantity;
        allowPublicationApproximation = AllowPublicationApproximation;
        prices.Clear(); ownedOrders.Clear(); OrderState = "No owned orders";
    }

    private bool AcceptedTiming(InformationEvent info) =>
        info.TimingBasis == InformationTimingBasis.SnapshotFetched || allowPublicationApproximation;

    private void CancelPendingEntries(ReplayContext context)
    {
        foreach (var order in context.PendingOrders.Where(order => ownedOrders.ContainsKey(order.Id)
            && order.Symbol == symbol && order.Side == OrderSide.Buy).ToArray())
        {
            ownedOrders[order.Id].SeenPending = true;
            CancelOwned(context, order.Id);
        }
    }

    private void CancelOwned(ReplayContext context, long id)
    {
        var reservation = ownedOrders[id];
        if (!reservation.CancelSent && context.CancelOrder(id)) reservation.CancelSent = true;
        if (reservation.CancelSent) OrderState = "Waiting for owned cancellation to leave the broker snapshot";
    }

    private void Target(ReplayContext context, decimal target)
    {
        var pending = context.PendingOrders.Where(order => ownedOrders.ContainsKey(order.Id)
            && order.Symbol == symbol).ToArray();
        var pendingIds = pending.Select(order => order.Id).ToHashSet();
        foreach (var order in pending) ownedOrders[order.Id].SeenPending = true;
        foreach (var tracked in ownedOrders.Where(pair => pair.Value.SeenPending && !pendingIds.Contains(pair.Key)).ToArray())
            ownedOrders.Remove(tracked.Key);
        // Broker snapshots may lag a newly submitted ID. Preserve its reservation instead of duplicating exposure.
        // If an order completes before ever appearing in PendingOrders, pause conservatively until strategy restart.
        if (ownedOrders.Values.Any(reservation => !reservation.SeenPending))
        { OrderState = "Waiting for submitted order to appear in the broker snapshot"; return; }
        if (ownedOrders.Values.Any(reservation => reservation.CancelSent))
        { OrderState = "Waiting for owned cancellation to leave the broker snapshot"; return; }
        decimal actual = context.TradingPositions.Where(position => position.Symbol == symbol)
            .Sum(position => position.Quantity);
        decimal projected = actual + pending.Sum(order => order.Side == OrderSide.Buy
            ? order.RemainingQuantity : -order.RemainingQuantity);
        if (projected == target) { OrderState = pending.Length == 0 ? "Target held" : "Owned pending order covers target"; return; }
        foreach (var order in pending) CancelOwned(context, order.Id);
        // Live cancellation is asynchronous: never submit an offsetting duplicate while cancellation is pending.
        if (context.PendingOrders.Any(order => ownedOrders.ContainsKey(order.Id) && order.Symbol == symbol)) return;
        actual = context.TradingPositions.Where(position => position.Symbol == symbol).Sum(position => position.Quantity);
        if (actual != target)
        {
            long id = SubmitMarketOrder(target > actual ? Buy : Sell, symbol, Math.Abs(target - actual));
            ownedOrders[id] = new OrderReservation(context.PendingOrders.Any(order => order.Id == id));
            OrderState = ownedOrders[id].SeenPending ? "Owned pending order covers target" : "Waiting for submitted order to appear in the broker snapshot";
        }
    }

    private void AddClose(decimal close, int capacity)
    {
        prices.Enqueue(close);
        while (prices.Count > capacity) prices.Dequeue();
    }

    private static bool IsRecent(DateTimeOffset now, DateTimeOffset? published, int maximumHours) =>
        published is { } date && now >= date && (now - date).TotalHours <= maximumHours;
}
