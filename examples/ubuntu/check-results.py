#!/usr/bin/env python3
"""Check nested results as well as process exit codes; print a short summary."""
import json
import sys
from pathlib import Path

work = Path(sys.argv[1])


def require(condition, message):
    if not condition:
        raise SystemExit(message)


def worker(value, label):
    require(value and value["status"] == "succeeded" and value["processExited"], f"{label}: worker failed or still running")
    response = value.get("response") or {}
    require(response.get("status") == "succeeded" and response.get("result") is not None, f"{label}: replay did not succeed")


for operation in ("backtest", "optimize", "genetic", "research"):
    result = json.loads((work / f"{operation}-result.json").read_text(encoding="utf-8"))
    expected = "succeeded" if operation == "backtest" else "completed"
    require(result["status"] == expected, f"{operation}: {result['status']}: {result.get('error')}")
    if operation == "backtest":
        worker(result["backtest"], operation)
        detail = f"{result['backtest']['response']['result']['eventsProcessed']} replay events"
    elif operation in ("optimize", "genetic"):
        search = result["search"]
        require(search and search["status"] == "completed" and search["best"], f"{operation}: no completed search/winner")
        for trial in search["trials"]:
            require(trial["status"] == "succeeded", f"{operation}: unsuccessful trial")
            worker(trial["outcome"], operation)
        if operation == "optimize":
            require(len(search["trials"]) == 4, "grid did not evaluate all four combinations")
        detail = f"{len(search['trials'])} trials; best score {search['best']['score']}"
    else:
        research = result["research"]
        require(research and research["status"] == "completed" and len(research["folds"]) == 2, "research: incomplete folds")
        for fold in research["folds"]:
            require(fold["status"] == "completed" and fold["selected"] and fold["outOfSample"], "research: incomplete selection/OOS")
            require(fold["selected"]["candidate"]["candidateId"] == fold["outOfSample"]["candidate"]["candidateId"], "research: OOS candidate differs from selection")
            for trial in fold["training"]["trials"] + fold["validation"] + [fold["outOfSample"]]:
                require(trial["status"] == "succeeded", "research: unsuccessful trial")
                worker(trial["outcome"], "research")
        detail = f"2 folds; independent OOS net profit {research['independentOutOfSampleNetProfit']}"
    print(f"{operation}: completed; {detail}; run {result['runId']}")
