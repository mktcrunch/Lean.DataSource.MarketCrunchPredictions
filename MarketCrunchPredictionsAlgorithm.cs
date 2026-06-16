/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using QuantConnect.Data;
using QuantConnect.DataSource;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Demonstration algorithm showing how to use the MarketCrunchPredictions custom dataset.
    /// Subscribes to SPY equity and the linked MarketCrunch prediction stream, and acts on
    /// the predicted next-day direction.
    /// </summary>
    public class MarketCrunchPredictionsAlgorithm : QCAlgorithm
    {
        private Symbol _customDataSymbol;
        private Symbol _equitySymbol;

        public override void Initialize()
        {
            SetStartDate(2024, 1, 1);
            SetEndDate(2024, 7, 1);
            SetCash(100000);

            // Subscribe to equity first, then add the linked custom data.
            _equitySymbol = AddEquity("SPY", Resolution.Daily).Symbol;
            _customDataSymbol = AddData<MarketCrunchPredictions>(_equitySymbol).Symbol;
        }

        public override void OnData(Slice slice)
        {
            if (!slice.ContainsKey(_customDataSymbol))
            {
                return;
            }

            var data = slice.Get<MarketCrunchPredictions>(_customDataSymbol);

            Log($"{Time:yyyy-MM-dd} - {data}");

            // Example: go long when the model predicts an up day with reasonable confidence.
            if (data.PredictionChange > 0m && data.PredConfidence >= 50 && !Portfolio[_equitySymbol].Invested)
            {
                SetHoldings(_equitySymbol, 1);
            }
            else if (data.PredictionChange < 0m && Portfolio[_equitySymbol].Invested)
            {
                Liquidate(_equitySymbol);
            }
        }
    }
}
