using QuantResearch.Engine;
using QuantResearch.Strategies;
namespace StudioExamples;
public sealed class BuyAndHold : Robot
{
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
    // The ending portfolio retains the position; stopping does not liquidate it.
}
