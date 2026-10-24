#!/usr/bin/env bash
# Run from any directory. Requires a packaged QR Studio CLI and Python 3.
set -euo pipefail
repo=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd -P)
QR_CLI=${QR_CLI:-$(command -v qr-studio || true)}
[[ -n "$QR_CLI" && -x "$QR_CLI" ]] || { echo 'Set QR_CLI to your executable QR Studio CLI launcher.' >&2; exit 2; }
work=${1:-$(mktemp -d "${TMPDIR:-/tmp}/qr-cli-example-XXXXXXXX")}
mkdir -p -- "$work"
work=$(cd -- "$work" && pwd -P)
[[ ! -e "$work/import.json" ]] || { echo 'Use a new work directory; existing results are retained.' >&2; exit 2; }
python3 "$repo/examples/ubuntu/prepare.py" data --work "$work"
"$QR_CLI" import --workspace "$work/workspace" --request "$work/import.json" > "$work/import-result.json"
"$QR_CLI" list --workspace "$work/workspace" > "$work/catalogue.json"
"$QR_CLI" compile --workspace "$work/workspace" \
  --source "$repo/examples/strategies/MovingAverageCrossover.cs" \
  --name MovingAverageCrossover.cs --output "$work/strategy-artifact.json" > "$work/compile-result.json"
python3 "$repo/examples/ubuntu/prepare.py" requests --work "$work"
for operation in backtest optimize genetic research; do
  echo "Running $operation..."
  "$QR_CLI" run --workspace "$work/workspace" --request "$work/$operation.json" > "$work/$operation-result.json"
done
"$QR_CLI" runs --workspace "$work/workspace" > "$work/runs.json"
python3 "$repo/examples/ubuntu/check-results.py" "$work"
printf 'Inputs, requests and results: %s\n' "$work"
