using System;
using QuantResearch.Engine;
using QuantResearch.Strategies;

namespace StudioExamples;

public sealed class RoundTrip : Robot
{
    private bool exitSubmitted;

    [Parameter("Quantity", Group = "Trading", DefaultValue = 1,
        MinValue = 1, MaxValue = 1000000, Step = 1)]
    public int Quantity { get; set; }

    [Parameter("Symbol", Key = "Symbol", Group = "Trading", DefaultValue = "DEMO")]
    public string SymbolName { get; set; } = "DEMO";

    // Keep Symbol metadata bound to this configured instrument in every callback.
    protected override string? SymbolNameOverride => SymbolName;

    public override void OnStart(ReplayContext context)
    {
        SubmitMarketOrder(Buy, SymbolName, Quantity);
    }

    public override void OnBarOpened(ReplayContext context, BarOpen bar) => SubmitExit(bar);

    public override void OnTick(ReplayContext context, MarketEvent tick) => SubmitExit(tick);

    private void SubmitExit(MarketEvent observation)
    {
        if (exitSubmitted || !string.Equals(observation.Symbol, SymbolName, StringComparison.Ordinal))
            return;
        // Submission is an intent; the exit can fill only on a later eligible observation.
        SubmitMarketOrder(Sell, SymbolName, Quantity);
        exitSubmitted = true;
    }
}
