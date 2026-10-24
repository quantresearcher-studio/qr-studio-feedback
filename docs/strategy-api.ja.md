# ストラテジー API サンプル

[English](strategy-api.md) · [Ubuntu CLI 手順](ubuntu-cli.ja.md) · [API ガイド](https://quant-research.org/ja/docs/strategy-api)

QR Studio のストラテジー API を使う、独立した C# サンプルです。対応する Studio で **1 ファイルずつ**コンパイルしてください。教材用であり、利益や実口座での約定を保証しません。

対象は **M136 / 0.8.0-rc.6** API（2026 年 10 月確認）です。インストーラー候補版は引き続きリリース試験中です。旧版で動く例もありますが、新しい Symbol／セッション API には対応パッケージが必要です。[manifest.json](../examples/strategies/manifest.json) に、ライブラリの 17 サンプルの型名とソースハッシュを記録しています。

## サンプルの選び方

| サンプル | 内容・必要なデータ |
| --- | --- |
| [SymbolMetadataObserver](../examples/api/SymbolMetadataObserver.cs) | `Symbol.TickSize`、`TimeZone`、`ToLocalTime`、`NormalizePrice` を表示。呼値と取引所時間帯を設定した足／Quote データ。注文なし。 |
| [ApiReferenceExamples](../examples/api/ApiReferenceExamples.cs) | 型付きコールバック、パラメーター、decimal/context と簡易注文 API の構文。注文メソッドはコールバックから呼びません。 |
| [TypedCallbackObserver](../examples/strategies/TypedCallbackObserver.cs) | 足開始・終了、Trade、Quote、Tick の通知。注文なし。 |
| [MyStrategy](../examples/strategies/MyStrategy.cs) | 新規作成の雛形。型名は `UserStrategies.MyStrategy`。 |
| [DemoRoundTrip](../examples/strategies/DemoRoundTrip.cs) | 簡単な新規・決済の例。型名は `StudioExamples.RoundTrip`。 |
| [BuyAndHold](../examples/strategies/BuyAndHold.cs) | 成行の買いを 1 回送信。 |
| [EventObserver](../examples/strategies/EventObserver.cs) | 市場イベントを観察。注文なし。 |
| [MovingAverageCrossover](../examples/strategies/MovingAverageCrossover.cs) | 時間足 SMA、準備完了判定、目標ポジション。Ubuntu 手順で使用。 |
| [MeanReversion](../examples/strategies/MeanReversion.cs) | 先行する確定足のバンドと Z スコア。 |
| [ChannelBreakout](../examples/strategies/ChannelBreakout.cs) | 先行する確定足の高値・安値チャネル。 |
| [TradeMomentum](../examples/strategies/TradeMomentum.cs) | 約定価格の時間足モメンタム。Trade データ。 |
| [QuoteMomentum](../examples/strategies/QuoteMomentum.cs) | Bid 時間足、観測スプレッド・数量の条件。Quote データ。 |
| [MultiSymbolTrend](../examples/strategies/MultiSymbolTrend.cs) | 選択した 2 銘柄の独立した指標。 |
| [MultiTimeZoneSessionTrend](../examples/strategies/MultiTimeZoneSessionTrend.cs) | UTC イベントから現地セッションを判断。夏時間を考慮。 |
| [QuoteTradeConfirmation](../examples/strategies/QuoteTradeConfirmation.cs) | 実際の Trade と新鮮な Quote の組合せ。両ストリームが必要。 |
| [PassiveBidAskLadder](../examples/strategies/PassiveBidAskLadder.cs) | Quote ベースの指値、価格正規化、現地時間の複数取引窓。 |
| [NewsFilteredMovingAverage](../examples/strategies/NewsFilteredMovingAverage.cs) | 価格と到着済みの同一銘柄ニュース情報。 |
| [JQuantsStatementForecast](../examples/strategies/JQuantsStatementForecast.cs) | 価格と、対応する開示ラベルを持つ財務諸表情報。 |
| [JQuantsFundamentalForecast](../examples/strategies/JQuantsFundamentalForecast.cs) | 価格と財務サマリーのルール例。学習済み ML ではありません。 |

特記したものを除き型名は `StudioExamples.` + 拡張子を除いたファイル名です。API 参照用の 2 例は `QrExamples.` を使います。コンパイル成功だけでは、すべてのデータ・証券会社・口座での実行を検証したことにはなりません。

## Symbol と保存パラメーター

銘柄名の C# プロパティを `SymbolName` として、継承元の `Symbol` API を使えるようにします。

```csharp
[Parameter("Symbol", Key = "Symbol", Group = "Trading", DefaultValue = "DEMO")]
public string SymbolName { get; set; } = "DEMO";

protected override string? SymbolNameOverride => SymbolName;
```

`Key = "Symbol"` により保存済みのキーを維持します。JSON は `"Symbol":"DEMO"` のままです。override により開始時・時計コールバックでも指定銘柄のメタデータを使えます。データ購読や注文の送信先を自動で変更する設定ではありません。

Quote コールバック内の例：

```csharp
decimal tick = Symbol.TickSize;
DateTimeOffset local = TimeZoneInfo.ConvertTime(quote.Timestamp, Symbol.TimeZone);
decimal bidPrice = Symbol.NormalizePrice(quote.Bid, OrderSide.Buy, levelsAway: 5);
decimal askPrice = Symbol.NormalizePrice(quote.Ask, OrderSide.Sell, levelsAway: 5);
```

買いは切り下げ、売りは切り上げで有効な価格に合わせます。`levelsAway` は外側へ進める有効な価格段数です。Live ではホストが提供する銘柄の価格帯別ルールも使用し、リプレイでは設定した固定呼値を使用します。`TickSize` は基本の刻み幅で、価格帯別の全ルールを表す値ではありません。呼値・取引所時間帯が未設定の場合はエラーとなり、推測値には置き換えません。

複数銘柄で対象が一意でない場合は `Symbols.GetSymbol("DEMO")` で明示します。`Symbol.TimeZone` は取引所時間帯です。PC の表示時間帯や CSV 取り込みの時刻解釈とは別であり、時刻変換で UTC の再生順序は変わりません。

## 指標・コールバック・注文

[移動平均の例](../examples/strategies/MovingAverageCrossover.cs) は `OnStart` で一度だけ指標を登録し、`IsReady` を確認して `OnBarClosed` で確定足を読みます。選択済みの先行する互換データで指標を準備しますが、その間は取引しません。足終了時の判断は後続の適格なイベントで約定処理されます。足開始時点で最終 OHLCV は使えません。

`SetTargetPosition(SymbolName, quantity)` は未約定注文を考慮して目標数量を管理します。低水準の `context.SubmitLimitOrder(...)` を使う場合は注文状態の管理を理解してください。`context.Positions`／`Positions` はネットポジションです。Live では手動や別戦略の保有も含まれ、全数量が自分の戦略の所有であることを意味しません。

## PassiveBidAskLadder の取引時間

[PassiveBidAskLadder.cs](../examples/strategies/PassiveBidAskLadder.cs) の型名は `StudioExamples.PassiveBidAskLadder` です。適切な銘柄ルールと **Quote データ**が必要です。Ubuntu 手順の足データだけでは指値ラダーを出しません。

`exchangeTimeZoneId: Asia/Tokyo` を設定した日本の選択銘柄向けの例：

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

`TickSize=0` は銘柄メタデータを使用します。正の値は旧設定と互換の間隔指定ですが、最終価格は銘柄のルールで正規化します。`TradingWindows` が空でない場合、`TradingStart`／`TradingEnd` より優先します。終了時刻は含みません。

時間外は新しい指値を停止し、この戦略の未約定注文の取消を要求します。確認が取れ、新鮮な時間内 Quote が届くと再開します。保有ポジションはそのままです。休憩中にリプレイのイベントがない場合は、次の関連イベントで前のセッションの注文を取り消します。曜日・時間窓は祝日カレンダーではありません。

符号付き在庫に対応しますが、Live の空売り可否はコネクター・口座・銘柄条件に従います。数量刻みや契約単位も実銘柄に合わせてください。Quote リプレイの流動性・約定モデルは取引所のキュー順位を証明しません。公開サンプルの検証と実際の Paper／Live 約定試験は別です。

## 実行と変更

Studio の Strategies で `.cs` を開き、正しい型を選択して Validate C# を実行し、データ／銘柄とパラメーターを確認してください。Ubuntu は [CLI 手順](ubuntu-cli.ja.md) のコンパイル成果物をリクエストから参照します。

変更はコピーに対して行い、再検証してください。manifest のハッシュ対象は未変更のライブラリ 17 例であり、別フォルダーの API 参照 2 例や利用者の変更版は含みません。質問は [Discussions](https://github.com/quantresearcher-studio/qr-studio-feedback/discussions) へ投稿できます。
