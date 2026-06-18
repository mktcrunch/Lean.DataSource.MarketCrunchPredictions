# MarketCrunchPredictions – LEAN Data Source Contribution

## Overview

**MarketCrunch AI – Predictions** – next-trading-day price forecasts for US equities and ETFs.

| Property | Value |
|---|---|
| **Type** | Linked dataset (mapped to equities) |
| **Requires Mapping** | true |
| **Has Universe Data** | no |
| **Resolution** | Daily |
| **Timezone** | America/New_York |
| **Sparse** | true |
| **Streaming** | no |
| **Model version** | `mc-eod-v1` |
| **Coverage** | 2021-06-01 → 2026-06-01, 246 symbols (193 S&P 500 large-caps + 53 ETFs) |

## Files

| File | Purpose |
|---|---|
| `MarketCrunchPredictions.cs` | Data model class (inherits `BaseData`) |
| `MarketCrunchPredictionsAlgorithm.cs` | C# demonstration algorithm |
| `MarketCrunchPredictionsAlgorithm.py` | Python demonstration algorithm |
| `DataProcessing/Program.cs` | Daily incremental data-processing tool |
| `tests/MarketCrunchPredictionsTests.cs` | Unit tests (read sample data from `output/`) |
| `listing-about.md` | Marketplace short description |
| `listing-documentation.md` | Full usage documentation (Python + C#) |
| `output/alternative/marketcrunch/predictions/{aapl,spy}.csv` | Minimal header-less sample data (Jan–Jun 2024) for demos/tests |

## CSV Format

On-disk per-symbol files are **header-less**, one row per prediction date, sorted ascending:

```
create_date,ticker,prediction_date,prediction_price,prediction_change,pred_confidence,last_7d_accuracy,last_30d_accuracy,last_90d_accuracy,model_version,time,cutoff_date
06-12-2026,AAPL,03-20-2024,184.53,0.000009,99,71.43,53.33,47.78,mc-eod-v1,2026-06-12 21:48:33,03-19-2024
```

(The Reader skips any header row defensively, so files with a header still parse.)

## Data Model Properties

| Property (C#) | Type | Description |
|---|---|---|
| `Time` (= `EndTime`) | DateTime | The instant the prediction becomes available: 5:30pm ET on the cut-off day. The data has **no period** (`EndTime == Time`), so the point is emitted at that moment. |
| `PredictionDate` | DateTime | The trading day the prediction applies to (the next session after the cut-off). |
| `PredictionPrice` | decimal | Predicted close for `PredictionDate` (also `Value`). |
| `PredictionChange` | decimal | Predicted change vs. prior close, absolute fraction. |
| `PredConfidence` | int | Model confidence 0–100. |
| `Last7DAccuracy` / `Last30DAccuracy` / `Last90DAccuracy` | decimal | Trailing directional accuracy %. |
| `ModelVersion` | string | Model tag (`mc-eod-v1`). |
| `ProducedTime` | DateTime | When the record was produced. |
| `CreateDate` | DateTime | Load date. |
| `CutoffDate` | DateTime | Last training date (the cut-off day; `Time` is this date at 5:30pm ET). |

## Build & Test

This repository follows the structure of the official
[Lean.DataSource.SDK](https://github.com/QuantConnect/Lean.DataSource.SDK) template: the
data model ships from `QuantConnect.DataSource.csproj`, the demo algorithm is compiled by the
test project, and the data-processing tool is a standalone console project.

```bash
dotnet build QuantConnect.DataSource.csproj      # data model assembly
dotnet build DataProcessing/DataProcessing.csproj
dotnet build tests/Tests.csproj
dotnet test  tests/Tests.csproj                  # run from the repo root so output/ resolves
```

## Integration into LEAN

To use this data source in a local LEAN engine:

1. Clone LEAN and this repository side by side, then build this project:
   `dotnet build QuantConnect.DataSource.csproj`.
2. Copy the built assembly `bin/Debug/QuantConnect.DataSource.MarketCrunchPredictions.dll`
   into LEAN's `Launcher/bin/Debug/`.
3. Copy the sample data from `output/alternative/marketcrunch/predictions/` into LEAN's
   `Data/alternative/marketcrunch/predictions/` folder.
4. Copy `MarketCrunchPredictionsAlgorithm.cs` into `Algorithm.CSharp/` (or the `.py` into
   `Algorithm.Python/`) and run a backtest. The algorithm subscribes via
   `AddData<MarketCrunchPredictions>(equitySymbol)`.

## Data Processing

The daily incremental update reads the previous day's per-symbol files from
`Globals.DataFolder`, merges the new day's export, de-duplicates by prediction date, and
writes the merged result to the temp output directory (never to the repo `output/` folder):

```bash
# Incremental (one day)
QC_DATAFLEET_DEPLOYMENT_DATE=$(date +%Y%m%d) dotnet run --project DataProcessing/DataProcessing.csproj
# Full history
dotnet run --project DataProcessing/DataProcessing.csproj
```

Processing duration (measured over all 246 tickers, ~308,800 prediction rows) —
**Full dataset: ~2.0s** · **One-day update: ~0.3–0.5s**. The one-day update is the pass
QuantConnect's data fleet runs in production (`QC_DATAFLEET_DEPLOYMENT_DATE` set): it merges
that day's export (one row per ticker) into the existing per-symbol history. Measured on an
Ubuntu 22.04 aarch64 sandbox; absolute numbers vary by host.

## Timing & Point-in-Time

Each prediction is produced after market close (~5:30pm ET) on the cut-off day and applies to
the **next** trading day. The data point is **instantaneous — it has no period** (`EndTime ==
Time`); `Time` is the cut-off day at 5:30pm ET, which is exactly when the prediction becomes
available. The predicted day is exposed as the `PredictionDate` property. Because the point is
emitted at 5:30pm ET on the production day and never back-dated, there is no look-ahead bias:
an algorithm receives the prediction the evening before and can act on the next session.

## Ticker Universe

The dataset is **not a hand-picked sample** — it is the full dataset currently available:
**246 symbols** made up of **193 US large-caps (all current S&P 500 constituents)** plus
**53 ETFs** spanning broad-index, sector, leveraged/inverse, fixed-income and commodity funds.
Every series runs the full **Jun 2021 → Jun 2026** span. Coverage is actively expanding over
the coming months; new tickers are appended the same way as history (one row per ticker per day
into each `{ticker}.csv`).

## Market Holidays

The dataset emits only the trading days the model produces — there is no holiday padding or
calendar synchronization. The data is sparse (`IsSparseData() == true`); algorithms should not
assume a point on every calendar day.

## PR / Submission Checklist

- [ ] `QuantConnect.DataSource.csproj` builds without errors
- [ ] `tests/Tests.csproj` builds and `dotnet test` passes
- [ ] `DataProcessing/DataProcessing.csproj` builds and runs an incremental then full pass
- [ ] Apache 2.0 license header on all `.cs` files; `LICENSE` present
- [ ] Data model inherits `BaseData`; `GetSource`/`Reader`/`Clone`/`ToString` implemented
- [ ] `DataTimeZone`, `SupportedResolutions`, `DefaultResolution`, `IsSparseData`,
      `RequiresMapping` return correct values
- [x] `output/` contains only minimal sample data copied from the temp output directory
      (AAPL + SPY, Jan–Jun 2024)
- [ ] Demo algorithms run in both C# and Python without errors
- [ ] `listing-about.md` and `listing-documentation.md` complete; asset classes capitalized
- [ ] CI (`.github/workflows/build.yml`) green
- [ ] Contact support@quantconnect.com to create the Data Market listing

## Reference

- https://www.quantconnect.com/docs/v2/lean-engine/contributions/datasets/key-concepts
- https://github.com/QuantConnect/Lean.DataSource.SDK
