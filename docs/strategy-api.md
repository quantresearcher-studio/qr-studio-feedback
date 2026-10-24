# Strategy API samples

[日本語](strategy-api.ja.md) · [Ubuntu CLI walkthrough](ubuntu-cli.md) · [Full API guide](https://quant-research.org/docs/strategy-api)

These standalone C# examples use QR Studio's strategy API. Compile **one file at a time** in the matching Studio package. Examples are educational and do not establish profitability or live execution behaviour.

Compatibility: **M136 / 0.8.0-rc.6** API, reviewed October 2026. The installer candidate remains in release testing. Some examples may compile on earlier builds, but newer symbol/session APIs require a compatible package. [Bundled sample manifest](../examples/strategies/manifest.json) records the source hashes and exact strategy type names for the 17 library examples.

## Choose an example

| Example | What it demonstrates / required input |
| --- | --- |
| [SymbolMetadataObserver](../examples/api/SymbolMetadataObserver.cs) | Read-only `Symbol.TickSize`, `Symbol.TimeZone`, `Symbol.ToLocalTime` and `Symbol.NormalizePrice`; bars or quotes, explicit instrument metadata. |
| [ApiReferenceExamples](../examples/api/ApiReferenceExamples.cs) | Typed callbacks, parameters, decimal/context and convenience order APIs; order snippets are private methods that callbacks do not invoke. |
| [TypedCallbackObserver](../examples/strategies/TypedCallbackObserver.cs) | Bar-open/closed, trade, quote and tick dispatch without orders. |
| [MyStrategy](../examples/strategies/MyStrategy.cs) | Starter scaffold, type `UserStrategies.MyStrategy`. |
| [DemoRoundTrip](../examples/strategies/DemoRoundTrip.cs) | A small entry/exit demonstration; type `StudioExamples.RoundTrip`. |
| [BuyAndHold](../examples/strategies/BuyAndHold.cs) | One market entry. |
| [EventObserver](../examples/strategies/EventObserver.cs) | Observe market events without orders. |
| [MovingAverageCrossover](../examples/strategies/MovingAverageCrossover.cs) | Registered timeframe SMAs, readiness and target positions; used in the Ubuntu tutorial. |
| [MeanReversion](../examples/strategies/MeanReversion.cs) | Prior-window bands and z-score rules. |
| [ChannelBreakout](../examples/strategies/ChannelBreakout.cs) | Prior completed candle high/low channels. |
| [TradeMomentum](../examples/strategies/TradeMomentum.cs) | Trade-price timeframe momentum; trade input. |
| [QuoteMomentum](../examples/strategies/QuoteMomentum.cs) | Bid-price timeframe momentum and observed spread/size filters; quote input. |
| [MultiSymbolTrend](../examples/strategies/MultiSymbolTrend.cs) | Independent indicators for two selected symbols. |
| [MultiTimeZoneSessionTrend](../examples/strategies/MultiTimeZoneSessionTrend.cs) | Local session decisions with UTC events and daylight-saving conversion. |
| [QuoteTradeConfirmation](../examples/strategies/QuoteTradeConfirmation.cs) | Actual trade signal plus fresh observed quote confirmation; both streams required. |
| [PassiveBidAskLadder](../examples/strategies/PassiveBidAskLadder.cs) | Quote-based limit ladder, normalized prices and optional local trading windows. |
| [NewsFilteredMovingAverage](../examples/strategies/NewsFilteredMovingAverage.cs) | Prices plus arrived, matching-symbol news information. |
| [JQuantsStatementForecast](../examples/strategies/JQuantsStatementForecast.cs) | Prices plus statement information with matching disclosure labels. |
| [JQuantsFundamentalForecast](../examples/strategies/JQuantsFundamentalForecast.cs) | Prices plus financial-summary information; a rule example, not trained ML. |

Unless specified otherwise, bundled types are `StudioExamples.` followed by the filename without `.cs`. The two API reference types use `QrExamples.`. Compiling a sample does not verify its results with every feed, broker or account.

## Symbol metadata and parameter compatibility

Use a different C# property name for the symbol selection parameter so the inherited `Symbol` API remains available:

```csharp
[Parameter("Symbol", Key = "Symbol", Group = "Trading", DefaultValue = "DEMO")]
public string SymbolName { get; set; } = "DEMO";

protected override string? SymbolNameOverride => SymbolName;
```

`Key = "Symbol"` preserves the saved parameter key. JSON still uses `"Symbol":"DEMO"`. The override binds metadata to that selected instrument in startup and clock callbacks too; it does not subscribe to data or route orders automatically.

Inside a quote callback:

```csharp
decimal tick = Symbol.TickSize;
DateTimeOffset local = TimeZoneInfo.ConvertTime(quote.Timestamp, Symbol.TimeZone);
decimal bidPrice = Symbol.NormalizePrice(quote.Bid, OrderSide.Buy, levelsAway: 5);
decimal askPrice = Symbol.NormalizePrice(quote.Ask, OrderSide.Sell, levelsAway: 5);
```

Buy prices round down; sell prices round up. `levelsAway` moves outward by valid price levels. Live normalization follows the instrument rules supplied by its host, including price bands when available; replay uses the configured fixed tick. `TickSize` is the base increment and is not a complete price-band schedule. Missing tick or exchange-zone metadata causes an error rather than inventing a value. Always configure the imported instrument accurately.

For multiple instruments, use `Symbols.GetSymbol("DEMO")` explicitly where there is no single current callback symbol. `Symbol.TimeZone` is the exchange zone, not the computer's display zone or the CSV parsing zone. Converting a timestamp preserves its instant; it does not shift replay event ordering.

## Indicators, callbacks and order decisions

The [moving-average source](../examples/strategies/MovingAverageCrossover.cs) registers indicators once in `OnStart`, checks `IsReady`, and reads completed candles in `OnBarClosed`. Earlier selected compatible data warms indicators without trading. Bar-based decisions execute on later eligible observations; final OHLCV is not available at bar open.

`SetTargetPosition(SymbolName, quantity)` manages the target including pending orders. The lower-level `context.SubmitLimitOrder(...)` submits an order request and requires the author to understand the strategy's order lifecycle. `context.Positions`/`Positions` expose net positions; live account positions can include manual and other-strategy exposure. A position is not proof that this strategy owns all its quantity.

## Passive ladder and local trading windows

Compile [PassiveBidAskLadder.cs](../examples/strategies/PassiveBidAskLadder.cs) as `StudioExamples.PassiveBidAskLadder`. Select **quote** data with compatible instrument rules. It will not quote from the bar-only Ubuntu tutorial dataset.

Example parameters for a selected Japanese instrument with `exchangeTimeZoneId: Asia/Tokyo`:

```json
{
  "Symbol": "YOUR_SELECTED_SYMBOL",
  "Levels": "2",
  "QuantityPerLevel": "1",
  "MaximumInventory": "2",
  "RefreshEveryQuotes": "10",
  "TickSize": "0",
  "UseTradingHours": "true",
  "TradingWindows": "09:00-11:30;12:30-15:30",
  "TradingDays": "Monday,Tuesday,Wednesday,Thursday,Friday"
}
```

`TickSize=0` uses instrument metadata; a positive legacy value sets explicit spacing, and prices are still normalized. Nonblank `TradingWindows` overrides `TradingStart`/`TradingEnd`. Windows use local exchange time and exclude the end. Outside a window, the managed schedule stops quoting and requests cancellation of this strategy's pending orders; quoting resumes on fresh in-window quotes after confirmation. Positions remain open. A replay without observations during a break cancels the old session's orders at the next relevant event. Weekday windows are not a holiday calendar; configure the actual market/session rules you need.

The ladder supports signed inventory, but live short sales depend on connector, account and instrument eligibility. Quantity steps and contract units must suit the selected instrument. Quote replay uses its configured fill/liquidity model and does not establish exchange queue position. Public sample validation is distinct from a real broker/Paper fill test.

## Run or adapt a sample

In Studio, open the `.cs` file in Strategies, select the exact type, validate, choose data/instruments and review parameters before starting a workflow. On Ubuntu, follow the [complete CLI tutorial](ubuntu-cli.md); compilation produces an artifact descriptor that the workflow request references.

Work on a copy when adapting samples. Revalidate after edits. The manifest fingerprints the unmodified bundled copies only; it does not include the two separate API reference examples or your custom files. Report API questions in [Discussions](https://github.com/quantresearcher-studio/qr-studio-feedback/discussions).
