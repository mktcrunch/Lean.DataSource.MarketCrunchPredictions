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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NodaTime;
using QuantConnect.Data;
using QuantConnect.Util;

namespace QuantConnect.DataSource
{
    /// <summary>
    /// MarketCrunch AI next-trading-day price predictions for US equities and ETFs.
    /// One record per security per prediction date, including the predicted close,
    /// predicted change, model confidence, trailing directional-accuracy statistics,
    /// and the training cut-off date used to produce the prediction.
    /// </summary>
    public class MarketCrunchPredictions : BaseData
    {
        private const string DateFmt = "MM-dd-yyyy";
        private const string TimeFmt = "yyyy-MM-dd HH:mm:ss";

        // NOTE: This type intentionally does NOT override EndTime. The prediction is an
        // instantaneous point (no period): BaseData.EndTime returns Time, so the point is
        // emitted at its Time — 5:30pm ET on the cut-off day, when the prediction is produced.

        /// <summary>The trading day the prediction applies to (the next session after the cut-off).</summary>
        public DateTime PredictionDate { get; set; }

        /// <summary>Date the record was created/loaded (load date of the dataset).</summary>
        public DateTime CreateDate { get; set; }

        /// <summary>Predicted close price for the prediction date. Also mapped to <see cref="BaseData.Value"/>.</summary>
        public decimal PredictionPrice { get; set; }

        /// <summary>Predicted change vs. the prior close, expressed as an absolute fraction (e.g. 0.0027 = +0.27%).</summary>
        public decimal PredictionChange { get; set; }

        /// <summary>Model confidence score (0–100). 0 where the model did not emit a confidence.</summary>
        public int PredConfidence { get; set; }

        /// <summary>Trailing 7-day directional (sign) accuracy %, as of the prior trading day's close.</summary>
        public decimal Last7DAccuracy { get; set; }

        /// <summary>Trailing 30-day directional accuracy %, as of the prior trading day's close.</summary>
        public decimal Last30DAccuracy { get; set; }

        /// <summary>Trailing 90-day directional accuracy %, as of the prior trading day's close.</summary>
        public decimal Last90DAccuracy { get; set; }

        /// <summary>Model version tag that produced the prediction (e.g. mc-eod-v1).</summary>
        public string ModelVersion { get; set; }

        /// <summary>Timestamp when the record was produced.</summary>
        public DateTime ProducedTime { get; set; }

        /// <summary>Last training date used for the next-day prediction (the prior trading day).</summary>
        public DateTime CutoffDate { get; set; }

        /// <summary>
        /// Return the path of the source file. Data is one CSV per (mapped) security under
        /// alternative/marketcrunch/predictions/{symbol}.csv, read as a local file.
        /// </summary>
        public override SubscriptionDataSource GetSource(
            SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        {
            return new SubscriptionDataSource(
                Path.Combine(
                    Globals.DataFolder,
                    "alternative",
                    "marketcrunch",
                    "predictions",
                    $"{config.Symbol.Value.ToLowerInvariant()}.csv"
                ),
                SubscriptionTransportMedium.LocalFile,
                FileFormat.Csv
            );
        }

        /// <summary>
        /// Parse one CSV line into a data point. Header rows (and blank lines) are skipped
        /// by returning null. CSV column order:
        /// create_date, ticker, prediction_date, prediction_price, prediction_change,
        /// pred_confidence, last_7d_accuracy, last_30d_accuracy, last_90d_accuracy,
        /// model_version, time, cutoff_date
        /// </summary>
        public override BaseData Reader(
            SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        {
            // Skip the header row / blank lines (data rows start with a digit, e.g. "06-01-2021").
            if (string.IsNullOrWhiteSpace(line) || !char.IsDigit(line.TrimStart()[0]))
            {
                return null;
            }

            var csv = line.Split(',');
            if (csv.Length < 12)
            {
                return null;
            }

            var predictionDate = ParseDate(csv[2], DateFmt);
            var cutoffDate = ParseDate(csv[11], DateFmt);

            // The prediction is produced after the close (~5:30pm ET) of the cut-off day and
            // becomes available at that moment. No period: Time == EndTime, so the point fires
            // at 5:30pm ET on the cut-off day — look-ahead free and actionable for the prediction
            // day's session.
            var availableTime = (cutoffDate != default ? cutoffDate : predictionDate)
                .Date.AddHours(17.5);

            // Parse numeric cells as nullable so empty cells become null (not 0) and we can
            // distinguish "no value" from a genuine zero. Value falls back to 0m only at assignment.
            var predictionPrice = ParseDecimal(csv[3]);
            var confidence = ParseDecimal(csv[5]);

            return new MarketCrunchPredictions
            {
                Symbol = config.Symbol,
                Time = availableTime,                     // EndTime == Time (no period)
                PredictionDate = predictionDate,
                CutoffDate = cutoffDate,
                CreateDate = ParseDate(csv[0], DateFmt),
                PredictionPrice = predictionPrice ?? 0m,
                PredictionChange = ParseDecimal(csv[4]) ?? 0m,
                PredConfidence = (int)(confidence ?? 0m),
                Last7DAccuracy = ParseDecimal(csv[6]) ?? 0m,
                Last30DAccuracy = ParseDecimal(csv[7]) ?? 0m,
                Last90DAccuracy = ParseDecimal(csv[8]) ?? 0m,
                ModelVersion = csv[9],
                ProducedTime = ParseTimestamp(csv[10]),
                Value = predictionPrice ?? 0m             // Value == PredictionPrice
            };
        }

        private static decimal? ParseDecimal(string s)
            => s.IfNotNullOrEmpty<decimal?>(
                v => decimal.Parse(v, NumberStyles.Any, CultureInfo.InvariantCulture));

        private static DateTime ParseDate(string s, string fmt)
            => string.IsNullOrWhiteSpace(s)
                ? default
                : DateTime.ParseExact(s, fmt, CultureInfo.InvariantCulture);

        private static DateTime ParseTimestamp(string s)
            => string.IsNullOrWhiteSpace(s)
                ? default
                : DateTime.ParseExact(s, TimeFmt, CultureInfo.InvariantCulture);

        /// <summary>The time zone of the data source (US equity market time).</summary>
        public override DateTimeZone DataTimeZone() => TimeZones.NewYork;

        /// <summary>Supported resolutions for this data type.</summary>
        public override List<Resolution> SupportedResolutions() => new() { Resolution.Daily };

        /// <summary>Default resolution when one is not specified.</summary>
        public override Resolution DefaultResolution() => Resolution.Daily;

        /// <summary>Data is sparse — not every security has a prediction on every day.</summary>
        public override bool IsSparseData() => true;

        /// <summary>Linked to equities; requires ticker mapping through time.</summary>
        public override bool RequiresMapping() => true;

        /// <summary>Creates a copy of the instance.</summary>
        public override BaseData Clone()
        {
            return new MarketCrunchPredictions
            {
                Symbol = Symbol,
                Time = Time,
                PredictionDate = PredictionDate,
                CreateDate = CreateDate,
                PredictionPrice = PredictionPrice,
                PredictionChange = PredictionChange,
                PredConfidence = PredConfidence,
                Last7DAccuracy = Last7DAccuracy,
                Last30DAccuracy = Last30DAccuracy,
                Last90DAccuracy = Last90DAccuracy,
                ModelVersion = ModelVersion,
                ProducedTime = ProducedTime,
                CutoffDate = CutoffDate,
                Value = Value
            };
        }

        /// <summary>String representation for debugging.</summary>
        public override string ToString()
            => $"{Symbol} pred {PredictionDate:yyyy-MM-dd} - Price: {PredictionPrice}, Change: {PredictionChange}, Confidence: {PredConfidence}";
    }
}
