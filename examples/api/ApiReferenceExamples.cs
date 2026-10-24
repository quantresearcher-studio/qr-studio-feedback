using System;
using System.Linq;
using QuantResearch.Engine;
using QuantResearch.Strategies;

namespace QrExamples;

// Compile-only syntax reference: callbacks intentionally place no orders.
public sealed class ApiReferenceExamples : Robot
{
    [Parameter("Enabled", Group = "Core", DefaultValue = true)]
    public bool Enabled { get; set; }

    [Parameter("Threshold", Group = "Signal", DefaultValue = 1.5,
        MinValue = 0.5, MaxValue = 10.0, Step = 0.5)]
    public double Threshold { get; set; }

    [Parameter("Limit price", Group = "Trading", DefaultValue = "158.90",
        MinValue = "0.01", MaxValue = "1000000.00", Step = "0.01")]
    public decimal LimitPrice { get; set; }

    [Parameter("Label", Group = "Trading", DefaultValue = "Reference")]
    public string Label { get; set; } = "Reference";

    public override void OnStart(ReplayContext context) { }
    public override void OnBarOpened(ReplayContext context, BarOpen bar) { }
    public override void OnBarClosed(ReplayContext context, BarClose bar) { }
    public override void OnTrade(ReplayContext context, Trade trade) { }
    public override void OnQuote(ReplayContext context, Quote quote) { }
    public override void OnInformation(ReplayContext context, InformationEvent information) { }
    public override void OnOrderSubmitted(ReplayContext context, StrategyOrderUpdate update) { }
    public override void OnFill(ReplayContext context, StrategyFill fill) { }
    public override void OnPositionOpened(ReplayContext context, StrategyPositionUpdate update) { }
    protected override void OnStop() { }

    private void DecimalOrders(ReplayContext context, DateTimeOffset validUntil)
    {
        long marketId = context.SubmitMarketOrder(Sell, "USDJPY", 2000m,
            label: Label, stopLossPips: 10m, takeProfitPips: 20m, comment: "Entry signal");
        long limitId = context.SubmitLimitOrder(Buy, "CHFJPY", 1000m, targetPrice: 158.90m,
            label: Label, stopLossPips: 10m, takeProfitPips: 20m,
            expiresAt: validUntil, comment: "Limit entry");
    }

    private void DoubleOrders(DateTimeOffset validUntil)
    {
        long marketId = ExecuteMarketOrder(Sell, "USDJPY", 2000,
            Label, 10, 20, "Entry signal");
        long limitId = PlaceLimitOrder(Buy, "CHFJPY", 1000, 158.90,
            Label, 10, 20, expiration: validUntil, comment: "Limit entry");
    }

    private void ProtectionExamples()
    {
        foreach (var position in Positions.Where(p => p.Symbol == "CHFJPY" && p.Label == Label))
            position.ModifyTakeProfitPips(20);
        foreach (var order in PendingOrders.Where(p => p.Symbol == "CHFJPY" && p.Label == Label))
            order.ModifyTakeProfitPips(20);
    }
}
