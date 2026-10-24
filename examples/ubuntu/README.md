# Ubuntu CLI examples / Ubuntu CLI 使用例

Start with the complete walkthrough: [English](../../docs/ubuntu-cli.md) / [日本語](../../docs/ubuntu-cli.ja.md).

- `prepare.py`: creates synthetic bars/import settings, then builds workflow requests from the real import manifest and compiled artifact.
- `run-all.sh`: runs import, compilation, backtest, grid optimization, genetic optimization and two research folds in a new directory.
- `check-results.py`: checks nested trial/fold outcomes and worker completion, not only the command's exit code.

Set `QR_CLI` to the compatible packaged CLI launcher, then run `bash examples/ubuntu/run-all.sh` from the repository root. Python 3 standard library only; no API keys or real market data. The script prints where it retained the generated data and results.

`QR_CLI` に対応パッケージの実行可能ランチャーを設定し、リポジトリ直下から `bash examples/ubuntu/run-all.sh` を実行してください。Python 3 標準ライブラリのみを使用し、API キーや実市場データは不要です。生成データ・結果を残し、保存先を表示します。
