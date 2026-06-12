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
| **Coverage** | 2021-06-01 → 2026-06-01, 246 tickers |

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
| `output/alternative/marketcrunch/predictions/*.csv` | Header-less sample data for demos/tests |

## CSV Format

On-disk per-symbol files are **header-less**, one row per prediction date, sorted ascending:

```
create_date,ticker,prediction_date,prediction_price,prediction_change,pred_confidence,last_7d_accuracy,last_30d_accuracy,last_90d_accuracy,model_version,time,cutoff_date
06-12-2026,AAPL,06-01-2021,121.85,-0.002071,0,71.43,63.33,48.89,mc-eod-v1,2026-06-12 21:48:33,05-28-2021
```

(The Reader skips any header row defensively, so files with a header still parse.)

## Data Model Properties

| Property (C#) | Type | Description |
|---|---|---|
| `Time` | DateTime | Cut-off date (prior trading day) — the basis of the prediction. |
| `EndTime` | DateTime | Prediction date — when LEAN fires the point. |
| `PredictionPrice` | decimal | Predicted close (also `Value`). |
| `PredictionChange` | decimal | Predicted change vs. prior close, absolute fraction. |
| `PredConfidence` | int | Model confidence 0–100. |
| `Last7DAccuracy` / `Last30DAccuracy` / `Last90DAccuracy` | decimal | Trailing directional accuracy %. |
| `ModelVersion` | string | Model tag (`mc-eod-v1`). |
| `ProducedTime` | DateTime | When the record was produced. |
| `CreateDate` | DateTime | Load date. |
| `CutoffDate` | DateTime | Last training date (= `Time`). |

## Build & Test

> The `.csproj` files reference the QuantConnect packages with wildcard versions and
> `net9.0` as a starting point. **Reconcile them with the official
> [Lean.DataSource.SDK](https://github.com/QuantConnect/Lean.DataSource.SDK) template** (package
> versions + `TargetFramework`) before building, since that template matches the LEAN engine
> you compile against.

```bash
dotnet build QuantConnect.DataSource.MarketCrunchPredictions.csproj
dotnet build tests/Tests.csproj
dotnet test  tests/Tests.csproj      # run from the repo root so output/ resolves
```

## Reference

- https://www.quantconnect.com/docs/v2/lean-engine/contributions/datasets/key-concepts
- https://github.com/QuantConnect/Lean.DataSource.SDK
