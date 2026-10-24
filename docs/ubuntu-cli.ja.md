# Ubuntu CLI：データ取り込み・バックテスト・最適化・Research

[English](ubuntu-cli.md) · [ストラテジー API サンプル](strategy-api.ja.md) · [トップページ](../README.ja.md)

この手順は **M136 / 0.8.0-rc.6** の CLI API を対象とします。インストーラー候補版は署名元の確認とリリース試験中であり、このページは公開リリースの告知ではありません。配布元の対応する Ubuntu パッケージをご利用ください。Ubuntu 版は CLI／ヘッドレス版です。認証を有効にした配布版では、ヘッドレス利用を含むライセンスが必要です。

以下は**合成データ**だけを使う操作例です。証券会社への接続、注文、データ提供元の API キーは不要です。損益は操作確認用であり、投資成績の検証ではありません。実データにも同じリクエスト形式を使用します。

| 操作 | コマンド |
| --- | --- |
| データ取り込み | `import --workspace ABS --request ABS` |
| データセット一覧 | `list --workspace ABS` |
| ストラテジーのコンパイル | `compile --workspace ABS --source ABS --name NAME.cs --output ABS` |
| Backtest / Optimize / Research | `run --workspace ABS --request ABS` |
| 保存済み実行一覧 | `runs --workspace ABS` |
| 保存済み実行の読み取り | `show --workspace ABS --id RUN_ID` |

`ABS` は絶対パスです。`backtest`、`optimize`、`research` という独立したサブコマンドはありません。`run` に渡す JSON の `operation` で処理を選択します。JSON のプロパティ名・列挙値は大文字小文字を区別します。

## 1. サンプルと CLI の準備

Ubuntu x64、Bash、Python 3、リポジトリ取得用の Git、対応する QR Studio CLI が必要です。補助スクリプトは Python 標準ライブラリのみを使用します。CSV の取り込み処理自体には Python は不要です。M136 パッケージ版は .NET ランタイム、C# コンパイラー、参照アセンブリを同梱します。パッケージの構成を保ってください。

```bash
git clone https://github.com/quantresearcher-studio/qr-studio-feedback.git
cd qr-studio-feedback
export QR_EXAMPLES="$(pwd -P)"
export QR_CLI="$(command -v qr-studio)"
test -x "$QR_CLI"
```

PATH に登録されていない場合は、例えば `export QR_CLI="$HOME/.local/bin/qr-studio"` のように実行可能なランチャーの絶対パスを設定します。対応する AppImage を直接指定する場合は `export APPIMAGE_EXTRACT_AND_RUN=1` も設定できます。インストールと認証は、その配布版に同梱された手順に従ってください。Windows 用 EXE は使用しません。旧ポータブル版でツールの指定が必要な場合は末尾の説明を参照してください。

すべての処理をまとめて実行するには：

```bash
bash examples/ubuntu/run-all.sh
```

新しい一時ディレクトリを作成し、入力・リクエスト・結果を残して保存先を表示します。既存のワークスペースは削除しません。同時候補数は最大 2、実行時間にも上限を設定しています。以下は個別に実行する手順です。

## 2. データ取り込み

実行ごとに新しいディレクトリを使用します。既存のコンパイル成果物は上書きしません。

```bash
export QR_DEMO="$(mktemp -d "$HOME/qr-cli-demo-XXXXXXXX")"
python3 "$QR_EXAMPLES/examples/ubuntu/prepare.py" data --work "$QR_DEMO"
"$QR_CLI" import --workspace "$QR_DEMO/workspace" \
  --request "$QR_DEMO/import.json" > "$QR_DEMO/import-result.json"
"$QR_CLI" list --workspace "$QR_DEMO/workspace" > "$QR_DEMO/catalogue.json"
python3 -m json.tool "$QR_DEMO/import-result.json"
```

[prepare.py](../examples/ubuntu/prepare.py) は架空銘柄 `DEMO` の 1 分足 OHLCV を 360 本生成します。開始は `2026-01-05T00:00:00+00:00`、価格はローカルで決定的に生成する合成値です。`import.json` はそのまま編集できる完全な取り込みリクエストです。全項目の JSON は [English 版](ubuntu-cli.md#2-data-import) にも掲載しています。

| 主な項目 | 意味・この例の値 |
| --- | --- |
| `sourcePath` | CSV の絶対パス |
| `format` / `metadata.kind` | `csv` / `bar` |
| `metadata.streamId` | ストリームの識別名 `demo-bars` |
| `metadata.instrument.symbol` | ストラテジーで選ぶ銘柄名 `DEMO` |
| `currency` / `priceTickSize` / `quantityStep` | `USD` / `0.01` / `1` |
| `executionFeedMode` | `barOpen`：後続の適格な足始値イベントで約定処理 |
| `exchangeTimeZoneId` | `Asia/Tokyo`：ストラテジーが参照する取引所の時間帯 |
| `priceBasis` | `raw`：この例は未調整価格 |
| `layout.columns` | 左が QR の項目名、右が CSV の列名 |
| `layout.barStamp` | `open`：CSV 時刻が足の開始時刻 |
| `layout.barIntervalTicks` | `600000000`：100ns 単位で 1 分 |

1 ファイルにつき 1 銘柄を取り込みます。行は時刻順、足は非重複である必要があります。足の高値・安値・終値・出来高は足終了時に利用可能になります。

実データではファイル、列対応、銘柄、通貨、呼値、数量刻み、価格調整方式、時刻規則を正しく設定してください。USD と東京の時間帯の組み合わせは説明用であり、実在銘柄の仕様ではありません。価格の小数桁数から呼値を推測しないでください。

| 元データの時刻 | `layout.time` |
| --- | --- |
| `2026-01-05T09:00:00+09:00` のようにオフセット付き | `{"mode":"explicitOffset"}` |
| UTC と確認済みのオフセットなし時刻 | `{"mode":"utc"}` |
| 取引所現地時刻 | `{"mode":"timeZone","zoneId":"Asia/Tokyo"}` |
| 固定 UTC+09:00 | `{"mode":"fixedOffset","offsetMinutes":540}` |

取り込み時刻の解釈と `exchangeTimeZoneId` は別です。元データは UTC に変換して保存され、複数ストリームのイベントは UTC の順序で再生されます。`Symbol.TimeZone` は現地時間での判断に使用するメタデータです。データごとの元の時間帯は正しく指定する必要があります。Ubuntu では `America/New_York` などの IANA 名を使うと夏時間を考慮できます。曖昧な現地時刻は、明示したオフセットで区別してください。

取り込み結果の `datasetId` を、実行リクエストの `datasetIds` に設定します。CSV ファイル名を ID として使いません。複数データの場合はそれぞれを取り込み、実際の ID と対応する `configuration.instruments` を設定します。重複するデータを二重選択しないでください。Quote／Trade 戦略には対応するデータと約定モードが必要です。

## 3. コンパイルとリクエスト作成

```bash
"$QR_CLI" compile --workspace "$QR_DEMO/workspace" \
  --source "$QR_EXAMPLES/examples/strategies/MovingAverageCrossover.cs" \
  --name MovingAverageCrossover.cs \
  --output "$QR_DEMO/strategy-artifact.json" > "$QR_DEMO/compile-result.json"
python3 "$QR_EXAMPLES/examples/ubuntu/prepare.py" requests --work "$QR_DEMO"
```

`--output` にはコンパイル成果物の記述子を保存します。標準出力は別のコンパイル診断レポートです。補助スクリプトは実際の成果物と取り込み ID を読み、`backtest.json`、`optimize.json`、`genetic.json`、`research.json` を生成します。ID やハッシュを手入力で作りません。記述子はローカルのアセンブリを参照するため、別 PC や別ビルドへ移動した場合は再コンパイルします。

主なフィールドは `operation`、`artifact`、`strategyType`、`parameters`、`datasetIds`、`configuration`、`domains`、`options`、`folds` です。パラメーター値と探索候補値は `"Fast":"3"` のように文字列で指定します。資金などの設定値は JSON の数値です。C# のプロパティ名が `SymbolName` でも、保存用キーは `"Symbol"` です。

## 4. Backtest

```bash
"$QR_CLI" run --workspace "$QR_DEMO/workspace" \
  --request "$QR_DEMO/backtest.json" > "$QR_DEMO/backtest-result.json"
```

`operation: backtest`、型名 `StudioExamples.MovingAverageCrossover`、Fast=3、Slow=10、両方 1 分足、数量 1 を使用します。初期資金は USD 10,000、約定ごとの手数料は USD 0.01、エクイティと取引履歴の記録を有効にしています。Backtest の `domains` と `folds` は空配列です。足終了時の判断は、その後の適格なイベントで約定処理されます。

この例の `researchLedger` は証券会社の資金・借株・担保制約を再現しません。`unbounded` は市場の利用可能数量を制約にしません。実際の分析では適切な会計・流動性モデルを設定してください。正の損益は将来の利益を示しません。

## 5. Optimization：Grid と Genetic

```bash
"$QR_CLI" run --workspace "$QR_DEMO/workspace" \
  --request "$QR_DEMO/optimize.json" > "$QR_DEMO/optimize-result.json"
"$QR_CLI" run --workspace "$QR_DEMO/workspace" \
  --request "$QR_DEMO/genetic.json" > "$QR_DEMO/genetic-result.json"
```

どちらも `operation: optimize` です。Grid では以下の 4 組を探索します。

```json
"domains": [
  { "name": "Fast", "values": ["3", "5"] },
  { "name": "Slow", "values": ["10", "15"] }
]
```

探索するキーは固定 `parameters` から外してください。`options.method: grid` の全組合せ数は `maximumEvaluations` 以下である必要があります。Genetic の例は `method: genetic`、seed=1、populationSize=4、generations=2、mutationPercent=20、評価上限 4 です。小さな操作例のため、世代数より先に評価予算を使い切る場合があります。実研究の推奨設定ではありません。

評価値は「終了時のポートフォリオ資産 − 初期資金」で、大きい候補を優先します。`search.trials` と `search.best` を確認してください。Sharpe や収益率そのものではありません。不正なパラメーター候補は `skipped_invalid` として記録され、勝者になりません。他の実行エラーは成功扱いになりません。

## 6. Research：学習・検証・アウトオブサンプル

```bash
"$QR_CLI" run --workspace "$QR_DEMO/workspace" \
  --request "$QR_DEMO/research.json" > "$QR_DEMO/research-result.json"
```

`operation: research` と同じ探索範囲を使用します。以下はすべて 2026 年 1 月 5 日の UTC 時刻です。開始を含み、終了を含まない区間です。

| Fold | 学習（Train） | 検証（Validation） | OOS |
| --- | --- | --- | --- |
| fold_1 | 00:00–02:00 | 02:00–03:00 | 03:00–04:00 |
| fold_2 | 01:00–03:00 | 03:00–04:00 | 04:00–05:00 |

学習区間で探索し、上位 2 候補を検証区間で比較します（`validationShortlist: 2`）。選択した 1 候補を固定して OOS を 1 回実行します。その Fold の候補選択に OOS の成績は使用しません。各区間より前の選択済み価格で指標を準備できますが、準備中は取引せず、そのイベントは区間の成績に入りません。Research の結果を見てさらに調整する場合は、別の最終ホールドアウトも確保してください。

`research.folds` に各段階、`research.independentOutOfSampleNetProfit` に独立 OOS の合計を記録します。データや成功候補のない Fold は、完了した OOS 試験とみなしません。Research では `folds` を使い、トップレベルの `window` は省略／null にします。Fold の ID は英字または `_` で始め、英数字と `_` のみで記述します。

## 7. 結果確認とトラブルシューティング

```bash
"$QR_CLI" runs --workspace "$QR_DEMO/workspace" > "$QR_DEMO/runs.json"
RUN_ID=$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["runId"])' "$QR_DEMO/backtest-result.json")
"$QR_CLI" show --workspace "$QR_DEMO/workspace" --id "$RUN_ID" > "$QR_DEMO/saved-backtest.json"
python3 "$QR_EXAMPLES/examples/ubuntu/check-results.py" "$QR_DEMO"
```

再確認にはワークスペースとコンパイル済みアセンブリを残してください。結果には使用パラメーター、入力・ソースの識別情報、個別結果を保存します。終了コード 0 には `completed_with_failures` も含まれるため、各候補・Fold の状態も確認してください。この例の確認スクリプトは、すべての候補の成功を要求します。

| 症状 | 確認する項目 |
| --- | --- |
| コマンド不明／ツール指定不足 | 対応ビルドとランチャー、下記の明示的ツール設定 |
| ファイルパスのエラー | 絶対パス、空白を含むパスの引用符 |
| 出力が既に存在 | 新しい作業ディレクトリ・ファイル名 |
| 不明なパラメーター | 保存キー、大文字小文字、文字列値 |
| 取引がない | 指標準備、銘柄、データ種別、期間、売買条件 |
| 価格刻み／時間帯が不明 | 呼値・数量刻み・取引所時間帯のメタデータ |
| アセンブリ識別不一致 | 実行に使うものと同じパッケージで再コンパイル |
| Research の区間が空 | 各 `[start,end)` 内の実際のデータ有無 |

Ctrl+C で中止を要求し、子プロセスの終了処理を待ってください。この例は候補 60 秒、探索 240 秒、ワークフロー 300 秒の上限です。大きいデータでは必要に応じて延長します。`-1` は時間制限なしです。候補数・世代数の上限は別に適用されます。

## ポータブル CLI のツール設定

旧ポータブル版では対応する CLI、worker、.NET SDK、参照パックを明示する必要があります。この API 版のコンパイラー SDK は **10.0.113** です。異なるビルドの DLL を混ぜないでください。

```bash
"$QR_DOTNET" "$QR_CLI_DLL" compile --workspace "$QR_DEMO/workspace" \
  --source "$QR_EXAMPLES/examples/strategies/MovingAverageCrossover.cs" \
  --name MovingAverageCrossover.cs --dotnet "$QR_DOTNET" \
  --compiler "$QR_CSC_DLL" --references "$QR_REFERENCE_DIR" \
  --sdk-version 10.0.113 --output "$QR_DEMO/strategy-artifact.json"
"$QR_DOTNET" "$QR_CLI_DLL" run --workspace "$QR_DEMO/workspace" \
  --request "$QR_DEMO/backtest.json" --dotnet "$QR_DOTNET" --worker "$QR_WORKER_DLL"
```

変数は配布版の説明に従い絶対パスに設定します。`QR_CSC_DLL` は Roslyn の `csc.dll`、`QR_REFERENCE_DIR` は対応する .NET 参照アセンブリのフォルダー、`QR_WORKER_DLL` は `QuantResearch.StrategyWorker.dll` です。`import`、`list`、`runs`、`show` は compiler／worker の引数を必要としません。別の SDK/API を要求する版には、その版の説明を使うか対応版へ更新してください。

## 確認範囲

M136 候補版と Ubuntu 22.04（WSL）で、公開する全 `.cs` のコンパイルと、合成データによる CLI の取り込み・Backtest・Grid/Genetic・Research を確認しています。クリーンな PC、署名済みインストーラー、本番認証・更新サービスのリリース試験とは別の確認です。アプリ本体のソースや認証情報は本リポジトリに含めません。
