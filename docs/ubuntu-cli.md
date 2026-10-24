# Ubuntu CLI: import, backtest, optimization and research

[日本語](ubuntu-cli.ja.md) · [Strategy API examples](strategy-api.md) · [Repository home](../README.md)

These examples target the QR Studio CLI API in **M136 / 0.8.0-rc.6**. The installer candidate is still undergoing publisher verification and release testing; this page does not announce a public installer release. Use a matching QR Studio Ubuntu package from the publisher. Ubuntu is the CLI/headless edition. A licence with headless access is required when the package enforces activation.

The walkthrough uses generated **synthetic** prices, makes no broker connection, and needs no provider key. Its results demonstrate the workflow, not trading performance. The request and strategy interfaces are the same ones used for user data.

| Task | Command |
| --- | --- |
| Import a file | `import --workspace ABS --request ABS` |
| List imported datasets | `list --workspace ABS` |
| Compile one strategy | `compile --workspace ABS --source ABS --name NAME.cs --output ABS` |
| Backtest / Optimize / Research | `run --workspace ABS --request ABS` |
| List saved runs | `runs --workspace ABS` |
| Read a saved run | `show --workspace ABS --id RUN_ID` |

`ABS` means an absolute path. There are no separate `backtest`, `optimize` or `research` CLI subcommands: `operation` in the request selects the workflow. JSON properties and enum values are case-sensitive; examples use camelCase.

## 1. Get the examples and choose the CLI

Prerequisites: Ubuntu x64, Bash, Python 3 (standard library only for these helpers), Git to clone this repository, and a compatible QR Studio CLI package. CSV import itself does not need Python. The packaged M136 launcher supplies its own .NET runtime, compiler and reference assemblies. Keep its package files together.

```bash
git clone https://github.com/quantresearcher-studio/qr-studio-feedback.git
cd qr-studio-feedback
export QR_EXAMPLES="$(pwd -P)"
export QR_CLI="$(command -v qr-studio)"
test -x "$QR_CLI"
```

If the launcher is outside `PATH`, set `QR_CLI` to its absolute executable path, for example `export QR_CLI="$HOME/.local/bin/qr-studio"`. A matching packaged AppImage can also be used as `QR_CLI` with `export APPIMAGE_EXTRACT_AND_RUN=1`. Install and activate according to the instructions supplied with your release. Do not use a Windows executable on Ubuntu. See [portable CLI configuration](#portable-cli-configuration) if your package requires explicit toolchain arguments.

For all steps in one command:

```bash
bash examples/ubuntu/run-all.sh
```

It creates a fresh temporary directory, prints its location, retains inputs/results for inspection, and checks nested workflow outcomes. It uses at most two concurrent candidate workers and finite timeouts. It never deletes an existing workspace. The following sections show the same commands individually.

## 2. Data import

Use a fresh directory for each walkthrough; compilation artifacts are deliberately not overwritten.

```bash
export QR_DEMO="$(mktemp -d "$HOME/qr-cli-demo-XXXXXXXX")"
python3 "$QR_EXAMPLES/examples/ubuntu/prepare.py" data --work "$QR_DEMO"
"$QR_CLI" import --workspace "$QR_DEMO/workspace" \
  --request "$QR_DEMO/import.json" > "$QR_DEMO/import-result.json"
"$QR_CLI" list --workspace "$QR_DEMO/workspace" > "$QR_DEMO/catalogue.json"
python3 -m json.tool "$QR_DEMO/import-result.json"
```

[prepare.py](../examples/ubuntu/prepare.py) creates 360 one-minute OHLCV bars for the fictional `DEMO` instrument, beginning at `2026-01-05T00:00:00+00:00`. Prices are deterministic and generated locally. The generated `import.json` is a complete editable request:

```json
{
  "sourcePath": "/absolute/path/to/synthetic-bars.csv",
  "format": "csv",
  "metadata": {
    "displayName": "Synthetic DEMO minute bars",
    "streamId": "demo-bars",
    "vendor": "Synthetic tutorial",
    "kind": "bar",
    "instrument": {
      "symbol": "DEMO",
      "currency": "USD",
      "executionFeedMode": "barOpen",
      "priceTickSize": 0.01,
      "quantityStep": 1,
      "contractMultiplier": 1,
      "accounting": "cashEquity",
      "exchangeTimeZoneId": "Asia/Tokyo"
    },
    "priceBasis": "raw"
  },
  "layout": {
    "columns": {
      "timestamp": "timestamp", "open": "open", "high": "high",
      "low": "low", "close": "close", "volume": "volume"
    },
    "delimiter": ",",
    "time": { "mode": "explicitOffset" },
    "barStamp": "open",
    "barIntervalTicks": 600000000
  }
}
```

`columns` maps QR field names on the left to your CSV column headers on the right. A .NET tick is 100 ns, so `600000000` is one minute. `barStamp: open` means the CSV timestamp is the candle's opening time; final OHLCV becomes available at its close. Rows must be ordered and bars must not overlap. The demonstration imports one symbol per file.

For your own CSV, change `sourcePath`, column mappings, symbol, currency, price increment, quantity increment and timing convention. These settings describe your data; the illustrative USD/Tokyo combination is not a real instrument mapping. Never infer an exchange's tick size from the number of decimal places in a price.

Timestamp choices:

| Source timestamps | `layout.time` |
| --- | --- |
| `2026-01-05T09:00:00+09:00` | `{"mode":"explicitOffset"}` |
| Offset-free values known to be UTC | `{"mode":"utc"}` |
| Offset-free local exchange times | `{"mode":"timeZone","zoneId":"Asia/Tokyo"}` |
| Fixed offset of UTC+09:00 | `{"mode":"fixedOffset","offsetMinutes":540}` |

Source timestamp interpretation and `exchangeTimeZoneId` have different jobs: import converts events to UTC; the exchange zone is metadata used by `Symbol.TimeZone`. Selected streams are replayed in unified UTC event order. Supply the correct source zone/offset for every dataset; the engine cannot guess it. Use IANA zones on Ubuntu, such as `America/New_York`, for daylight-saving rules. Ambiguous local timestamps need an explicit offset rather than a guess.

Import returns a manifest with a `datasetId`; use that value in workflow requests, not the CSV filename. To use multiple datasets, import each and list their actual IDs in `datasetIds`, with matching instrument definitions in `configuration.instruments`. Do not select overlapping duplicate partitions. Quote and trade strategies need matching data and execution modes; this bar example cannot demonstrate quote fills.

## 3. Compile and create workflow requests

```bash
"$QR_CLI" compile --workspace "$QR_DEMO/workspace" \
  --source "$QR_EXAMPLES/examples/strategies/MovingAverageCrossover.cs" \
  --name MovingAverageCrossover.cs \
  --output "$QR_DEMO/strategy-artifact.json" > "$QR_DEMO/compile-result.json"
python3 "$QR_EXAMPLES/examples/ubuntu/prepare.py" requests --work "$QR_DEMO"
```

The `--output` file is the compiled artifact descriptor. Standard output is a separate compilation report. The helper reads the actual artifact and imported dataset ID, then creates `backtest.json`, `optimize.json`, `genetic.json` and `research.json`. No hashes or dataset IDs are invented. The descriptor refers to a local compiled assembly; recompile after moving machines or changing the application build.

Each request contains `operation`, `artifact`, `strategyType`, `parameters`, `datasetIds`, `configuration`, `domains`, `options` and `folds`. Inspect the generated files before substituting real data. Parameters and search values are strings, for example `"Fast":"3"`; configuration quantities/prices are JSON numbers. The parameter key remains `"Symbol"` even though the C# property is called `SymbolName`.

## 4. Backtest

```bash
"$QR_CLI" run --workspace "$QR_DEMO/workspace" \
  --request "$QR_DEMO/backtest.json" > "$QR_DEMO/backtest-result.json"
```

This selects `StudioExamples.MovingAverageCrossover`, `Fast=3`, `Slow=10`, one-minute indicators and one share. It starts with USD 10,000, charges USD 0.01 per fill, and captures equity and trade history. `domains` and `folds` are empty for a backtest. Orders decided at candle close fill only on a later eligible observation under `barOpen` execution.

The example uses `researchLedger` accounting and `unbounded` liquidity. ResearchLedger does not enforce a broker's funding/borrowing constraints; unbounded liquidity does not model available market size. Choose the appropriate assumptions before interpreting a real run. A positive score is not a validation of profitability.

## 5. Optimization: grid and genetic search

```bash
"$QR_CLI" run --workspace "$QR_DEMO/workspace" \
  --request "$QR_DEMO/optimize.json" > "$QR_DEMO/optimize-result.json"
"$QR_CLI" run --workspace "$QR_DEMO/workspace" \
  --request "$QR_DEMO/genetic.json" > "$QR_DEMO/genetic-result.json"
```

Both requests use `operation: optimize`. The grid searches four combinations:

```json
"domains": [
  { "name": "Fast", "values": ["3", "5"] },
  { "name": "Slow", "values": ["10", "15"] }
]
```

Keep searched keys out of fixed `parameters`. Grid uses `options.method: grid`; its Cartesian product must fit `maximumEvaluations`. Genetic uses `method: genetic`, `seed: 1`, `populationSize: 4`, `generations: 2`, `mutationPercent: 20`, and a four-evaluation budget for this tiny demonstration. That budget can finish the search before all requested generations; it is not a recommendation for real research.

The current score is terminal portfolio equity minus initial cash, ranked highest first. Compare `search.trials` and `search.best`; do not interpret the score as Sharpe or percentage return. Invalid strategy parameter combinations may be recorded as `skipped_invalid` and cannot win. Other errors are not automatically successful trials.

## 6. Research: training, validation and out-of-sample

```bash
"$QR_CLI" run --workspace "$QR_DEMO/workspace" \
  --request "$QR_DEMO/research.json" > "$QR_DEMO/research-result.json"
```

The research request uses `operation: research`, the same search domains and two explicit folds. All times below are UTC on 5 January 2026; windows include their start and exclude their end.

| Fold | Training | Validation | Out-of-sample |
| --- | --- | --- | --- |
| fold_1 | 00:00–02:00 | 02:00–03:00 | 03:00–04:00 |
| fold_2 | 01:00–03:00 | 03:00–04:00 | 04:00–05:00 |

Training searches the parameter space. The top two training candidates are replayed in validation (`validationShortlist: 2`). One validation winner is fixed before its out-of-sample replay. OOS results do not select that fold's candidate. Earlier selected prices can warm indicators independently for each segment; warm-up does not trade or enter segment metrics. Keep a separate final holdout for decisions made after examining research results.

Read `research.folds` for each phase and `research.independentOutOfSampleNetProfit` for the independent fold aggregate. A fold without usable observations or a successful candidate is not evidence of a completed OOS test. For research, use explicit `folds` and leave the top-level `window` absent/null. Fold IDs start with a letter or underscore and contain only ASCII letters, digits and underscores.

## 7. Inspect results and troubleshoot

```bash
"$QR_CLI" runs --workspace "$QR_DEMO/workspace" > "$QR_DEMO/runs.json"
RUN_ID=$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["runId"])' "$QR_DEMO/backtest-result.json")
"$QR_CLI" show --workspace "$QR_DEMO/workspace" --id "$RUN_ID" > "$QR_DEMO/saved-backtest.json"
python3 "$QR_EXAMPLES/examples/ubuntu/check-results.py" "$QR_DEMO"
```

Keep the workspace and compiled assembly for later `show`/verification. Result JSON includes parameters, source/input identities and nested outcomes. A zero process exit code can also mean `completed_with_failures`; check the top-level and individual trial/fold statuses. Compiler/worker diagnostics appear in their report fields and standard error. Our result checker intentionally requires every demonstration trial to succeed.

| Symptom | What to check |
| --- | --- |
| Unknown command or missing toolchain options | Package compatibility and launcher; see below. |
| Source or request path error | Use absolute paths; quote paths containing spaces. |
| Existing artifact/output | Choose a fresh work directory or output filename. |
| Unknown parameter | Use the saved parameter key, exact case and string values. |
| No trades | Indicator warm-up, selected symbol/feed, date coverage and strategy conditions. |
| Off-step prices / missing exchange zone | Instrument `priceTickSize`, quantity step and `exchangeTimeZoneId`. |
| Worker/assembly identity mismatch | Recompile with the same package used to run. |
| Research has an empty segment | Check actual data coverage in each `[start,end)` window. |

Press Ctrl+C to request cancellation and allow workers to finish cleanup. These example requests have finite deadlines: 60 seconds per trial, 240 seconds per search and 300 seconds per workflow. Increase them deliberately for larger inputs; `-1` requests no deadline. Real dataset files, provider licences and machine resources determine what is practical.

## Portable CLI configuration

Older portable packages may require explicit arguments. Use the publisher's matching CLI, worker, .NET SDK and reference pack; this version requires compiler SDK **10.0.113**. Do not mix DLLs from different builds.

```bash
"$QR_DOTNET" "$QR_CLI_DLL" compile --workspace "$QR_DEMO/workspace" \
  --source "$QR_EXAMPLES/examples/strategies/MovingAverageCrossover.cs" \
  --name MovingAverageCrossover.cs --dotnet "$QR_DOTNET" \
  --compiler "$QR_CSC_DLL" --references "$QR_REFERENCE_DIR" \
  --sdk-version 10.0.113 --output "$QR_DEMO/strategy-artifact.json"
"$QR_DOTNET" "$QR_CLI_DLL" run --workspace "$QR_DEMO/workspace" \
  --request "$QR_DEMO/backtest.json" --dotnet "$QR_DOTNET" --worker "$QR_WORKER_DLL"
```

Set these variables to absolute paths from your package instructions. `QR_CSC_DLL` is Roslyn's `csc.dll`, `QR_REFERENCE_DIR` is the matching .NET reference-assembly directory, and `QR_WORKER_DLL` is `QuantResearch.StrategyWorker.dll`. `import`, `list`, `runs` and `show` need no compiler/worker options. For a package with a different required SDK/API, use its own release documentation or update to a compatible release.

## Verification scope

The commands and all public `.cs` examples are checked with the M136 candidate in Ubuntu 22.04 under WSL. The walkthrough covers actual local CLI import, compilation, backtest, grid/genetic search and research execution with synthetic inputs. This is separate from the pending clean-machine, signed-installer and production-service release gates. No activation credentials, account data or proprietary engine source are included here.
