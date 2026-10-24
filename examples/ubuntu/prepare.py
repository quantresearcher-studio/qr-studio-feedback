#!/usr/bin/env python3
"""Generate synthetic inputs and public CLI requests; no provider or broker access."""
import argparse
import csv
import json
from datetime import datetime, timedelta, timezone
from pathlib import Path


def write_json(path, value):
    with path.open("x", encoding="utf-8") as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)
        stream.write("\n")


def timestamp(minute):
    return (datetime(2026, 1, 5, tzinfo=timezone.utc) + timedelta(minutes=minute)).isoformat()


def prepare_data(work):
    instrument = {
        "symbol": "DEMO", "currency": "USD", "executionFeedMode": "barOpen",
        "priceTickSize": 0.01, "quantityStep": 1, "contractMultiplier": 1,
        "accounting": "cashEquity", "exchangeTimeZoneId": "Asia/Tokyo",
    }
    source = work / "synthetic-bars.csv"
    with source.open("x", encoding="utf-8", newline="") as stream:
        writer = csv.writer(stream)
        writer.writerow(["timestamp", "open", "high", "low", "close", "volume"])
        prior = 10000
        for minute in range(360):
            phase = minute % 40
            close = 10000 + (phase if phase < 20 else 40 - phase) * 5 + minute // 40
            def money(cents):
                return f"{cents // 100}.{cents % 100:02d}"
            writer.writerow([timestamp(minute), money(prior), money(max(prior, close) + 2),
                             money(min(prior, close) - 2), money(close), 100])
            prior = close
    write_json(work / "import.json", {
        "sourcePath": str(source), "format": "csv",
        "metadata": {"displayName": "Synthetic DEMO minute bars", "streamId": "demo-bars",
                     "vendor": "Synthetic tutorial", "kind": "bar", "instrument": instrument,
                     "priceBasis": "raw"},
        "layout": {"columns": {key: key for key in ("timestamp", "open", "high", "low", "close", "volume")},
                   "delimiter": ",", "time": {"mode": "explicitOffset"},
                   "barStamp": "open", "barIntervalTicks": 600000000},
    })


def prepare_requests(work):
    artifact = json.loads((work / "strategy-artifact.json").read_text(encoding="utf-8"))
    manifest = json.loads((work / "import-result.json").read_text(encoding="utf-8"))
    options = {"method": "grid", "maximumEvaluations": 4, "maximumConcurrency": 2,
               "trialTimeoutMilliseconds": 60000, "overallTimeoutMilliseconds": 240000,
               "continueOnFailure": False, "seed": 1}
    parameters = {"Symbol": "DEMO", "Quantity": "1", "FastTimeframeMinutes": "1",
                  "SlowTimeframeMinutes": "1"}
    common = {
        "artifact": artifact, "strategyType": "StudioExamples.MovingAverageCrossover",
        "parameters": parameters, "datasetIds": [manifest["datasetId"]],
        "configuration": {"currency": "USD", "initialCash": 10000,
                          "instruments": [manifest["metadata"]["instrument"]],
                          "commissionPerFill": 0.01, "marginModel": "researchLedger",
                          "liquidityModel": "unbounded", "captureEquityTrace": True,
                          "captureTradeHistory": True},
        "domains": [{"name": "Fast", "values": ["3", "5"]},
                    {"name": "Slow", "values": ["10", "15"]}],
        "options": options, "folds": [], "validationShortlist": 2,
        "timeoutMilliseconds": 300000,
    }
    write_json(work / "backtest.json", dict(common, operation="backtest", domains=[],
               parameters=dict(parameters, Fast="3", Slow="10")))
    write_json(work / "optimize.json", dict(common, operation="optimize"))
    write_json(work / "genetic.json", dict(common, operation="optimize", options=dict(
        options, method="genetic", populationSize=4, generations=2, mutationPercent=20)))

    def window(start, end):
        return {"start": timestamp(start), "end": timestamp(end)}

    folds = [
        {"id": "fold_1", "train": window(0, 120), "validation": window(120, 180),
         "outOfSample": window(180, 240)},
        {"id": "fold_2", "train": window(60, 180), "validation": window(180, 240),
         "outOfSample": window(240, 300)},
    ]
    write_json(work / "research.json", dict(common, operation="research", folds=folds))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("stage", choices=["data", "requests"])
    parser.add_argument("--work", required=True, help="Directory for generated inputs and results")
    args = parser.parse_args()
    work = Path(args.work).expanduser().resolve()
    work.mkdir(parents=True, exist_ok=True)
    try:
        (prepare_data if args.stage == "data" else prepare_requests)(work)
    except FileExistsError as error:
        raise SystemExit(f"Existing file retained: {error.filename}. Use a new work directory.")
    print(f"Prepared {args.stage}: {work}")
