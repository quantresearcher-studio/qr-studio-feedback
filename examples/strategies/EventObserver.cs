using System;
using QuantResearch.Engine;
using QuantResearch.Strategies;
namespace StudioExamples;
public sealed class EventObserver : Robot
{
    private long observedEvents;

    [Parameter("Symbol", Key = "Symbol", Group = "Data", DefaultValue = "DEMO")]
    public string SymbolName { get; set; } = "DEMO";

    // Keep Symbol metadata bound to this configured instrument in every callback.
    protected override string? SymbolNameOverride => SymbolName;

    public override void OnStart(ReplayContext context) { observedEvents = 0; }

    // OnEvent is intentional here: count every matching observation, including
    // information, FX and corporate actions. Use the typed observer for named hooks.
    public override void OnEvent(ReplayContext context, MarketEvent observation)
    {
        if (string.Equals(observation.Symbol, SymbolName, StringComparison.Ordinal))
            observedEvents++;
        // Add research logic here. This template deliberately submits no orders.
    }
}
