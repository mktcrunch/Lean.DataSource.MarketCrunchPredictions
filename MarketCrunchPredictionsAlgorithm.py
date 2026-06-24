# region imports
from AlgorithmImports import *
from QuantConnect.DataSource import *
# endregion


class MarketCrunchPredictionsAlgorithm(QCAlgorithm):
    """
    Demonstration algorithm showing how to use the MarketCrunchPredictions custom dataset.
    Subscribes to AAPL equity and the linked MarketCrunch prediction stream, and acts on
    the predicted next-day direction.
    """

    def initialize(self):
        self.set_start_date(2024, 1, 1)
        self.set_end_date(2024, 7, 1)
        self.set_cash(100000)

        # Subscribe to equity first, then add the linked custom data.
        self._equity_symbol = self.add_equity("AAPL", Resolution.DAILY).symbol
        self._custom_data_symbol = self.add_data(MarketCrunchPredictions, self._equity_symbol).symbol

    def on_data(self, slice: Slice):
        if not slice.contains_key(self._custom_data_symbol):
            return

        data = slice.get(MarketCrunchPredictions, self._custom_data_symbol)

        self.log(f"{self.time:%Y-%m-%d} - {data}")

        # Example: go long when the model predicts an up day with reasonable confidence.
        if data.prediction_change > 0 and data.pred_confidence >= 50 and not self.portfolio[self._equity_symbol].invested:
            self.set_holdings(self._equity_symbol, 1)
        elif data.prediction_change < 0 and self.portfolio[self._equity_symbol].invested:
            self.liquidate(self._equity_symbol)
