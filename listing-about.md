# MarketCrunch Predictions

MarketCrunch Predictions provides next-trading-day price forecasts for US equities and ETFs,
produced by MarketCrunch AI's end-of-day machine-learning model (version `mc-eod-v1`). Each
record contains the predicted close, the predicted change versus the prior close, the model's
confidence, trailing directional-accuracy statistics (7/30/90-day), and the training cut-off
date used for the prediction.

The dataset is point-in-time: every record reflects only information available as of the prior
trading day's close, so it is free of look-ahead bias and suitable for backtesting. The initial
release covers 2021-06-01 to 2026-06-01 across 246 US equities and ETFs and is updated daily
after market close.
