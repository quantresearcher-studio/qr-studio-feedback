using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using QuantResearch.Engine;
using QuantResearch.Strategies;
namespace StudioExamples;

// Original educational rule, not a trained sentiment model: NewsAPI.ai sentiment gates a completed-bar SMA signal.
// Add a price dataset AND a same-symbol NewsAPIai Information dataset to the backtest.
// Default snapshot timing stays at fetch time. PublicationApproximation requires explicit parameter opt-in.
public sealed class NewsFilteredMovingAverage : Robot
{
    private DateTimeOffset? newsPublished;
    private bool newsAllowed;
    public string LastInputStatus { get; private set; } = "Waiting for news and completed price bars";

    [Parameter("Symbol", Key = "Symbol", Group = "Trading", DefaultValue = "DEMO")]
    public string SymbolName { get; set; } = "DEMO";

    // Keep Symbol metadata bound to this configured instrument in every callback.
    protected override string? SymbolNameOverride => SymbolName;

    [Parameter("Quantity", Group = "Trading", DefaultValue = 1,
        MinValue = 1, MaxValue = 1000000, Step = 1)]
    public int Quantity { get; set; } = 1;

    [Parameter("FastPeriod", Group = "Moving averages", DefaultValue = 20,
        MinValue = 2, MaxValue = 500, Step = 1)]
    public int FastPeriod { get; set; } = 20;

    [Parameter("SlowPeriod", Group = "Moving averages", DefaultValue = 50,
        MinValue = 3, MaxValue = 1000, Step = 1)]
    public int SlowPeriod { get; set; } = 50;

    [Parameter("MinimumSentiment", Group = "News filter", DefaultValue = "0.1",
        MinValue = "-1", MaxValue = "1", Step = "0.01")]
    public decimal MinimumSentiment { get; set; } = 0.1m;

    [Parameter("MaximumNewsAgeHoursAtSignal", Group = "News filter", DefaultValue = 72,
        MinValue = 1, MaxValue = 8760, Step = 1)]
    public int MaximumNewsAgeHoursAtSignal { get; set; } = 72;

    [Parameter("AllowPublicationApproximation", Group = "Information timing", DefaultValue = false)]
    public bool AllowPublicationApproximation { get; set; } = false;

    // Minutes per completed candle: 1, 5, 15, 30, 60, or another whole-minute divisor of 1440.

    [Parameter("Timeframe minutes", Group = "Timeframe", DefaultValue = 1, MinValue = 1, MaxValue = 1440, Step = 1)]
    public int TimeframeMinutes { get; set; } = 1;

    public override TimeSpan BarTimeframe => TimeSpan.FromMinutes(TimeframeMinutes);

    public override void OnStart(ReplayContext context)
    {
        Initialize();
        fast = Indicators.SimpleMovingAverage(BarTimeframe, FastPeriod, SymbolName);
        slow = Indicators.SimpleMovingAverage(BarTimeframe, SlowPeriod, SymbolName);
        newsPublished = null; newsAllowed = false;
        LastInputStatus = "Waiting for news and completed price bars";
        if (FastPeriod >= SlowPeriod)
            throw new ArgumentException("FastPeriod must be less than SlowPeriod.");
    }
    public override void OnInformation(ReplayContext context, InformationEvent info)
    {
        if (info.Symbol != symbol || info.Provider != "NewsAPIai" || info.Category != InformationCategory.News) return;
        // Older publication arriving later cannot replace the already observed newer article.
        if (newsPublished is { } prior && info.PublishedUtc < prior) return;
        newsPublished = info.PublishedUtc;
        newsAllowed = AcceptedTiming(info) && info.Metrics.TryGetValue("sentiment", out var sentiment)
            && sentiment >= -1m && sentiment <= 1m && sentiment >= MinimumSentiment;
        LastInputStatus = newsAllowed ? "News available; waiting for a price decision" : "News missing, excluded, or below threshold";
        if (!newsAllowed) CancelPendingEntries(context);
    }

    public override void OnBarClosed(ReplayContext context, BarClose bar)
    {
        if (bar.Symbol != symbol) return;
        // History warms prices only. Eligible news must still arrive through OnInformation.
        bool available = newsAllowed && IsRecent(bar.Timestamp, newsPublished, MaximumNewsAgeHoursAtSignal);
        if (!fast.IsReady || !slow.IsReady || !available)
        {
            LastInputStatus = !available ? "No eligible recent news at this decision" : "Warming up completed bars";
            Target(context, 0m); return;
        }
        decimal target = 0m;
        try
        {
            LastInputStatus = "News gate available; completed-bar SMA evaluated";
            target = fast.Close > slow.Close ? quantity : 0m;
        }
        catch (OverflowException) { LastInputStatus = "Price arithmetic unavailable"; }
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
    private BarIndicator<SimpleMovingAverage> fast = null!, slow = null!;

    private void Initialize()
    {
        symbol = SymbolName;
        if (string.IsNullOrWhiteSpace(symbol) || symbol != symbol.Trim())
            throw new ArgumentException("Choose the exact common price/information dataset symbol.");
        quantity = Quantity;
        allowPublicationApproximation = AllowPublicationApproximation;
        ownedOrders.Clear(); OrderState = "No owned orders";
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

    private static bool IsRecent(DateTimeOffset now, DateTimeOffset? published, int maximumHours) =>
        published is { } date && now >= date && (now - date).TotalHours <= maximumHours;
}
