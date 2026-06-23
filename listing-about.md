# MarketCrunch Predictions

MarketCrunch Predictions provides next-trading-day price forecasts for US large-cap equities,
produced by MarketCrunch AI's end-of-day machine-learning model (version `mc-eod-v1`). Each
record contains the predicted close, the predicted change versus the prior close, the model's
confidence, trailing directional-accuracy statistics (7/30/90-day), and the training cut-off
date used for the prediction.

The dataset is point-in-time: every record reflects only information available as of the prior
trading day's close, so it is free of look-ahead bias and suitable for backtesting. The initial
release covers 2021-06-01 to 2026-06-01 across the **survivorship-bias-free S&P 100 point-in-time
universe (113 symbols)** — every name that was an S&P 100 (OEX) constituent at any point in the
window, including the 12 removed during it — and is updated daily after market close. Coverage is
actively expanding.
