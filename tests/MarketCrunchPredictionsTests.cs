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
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using QuantConnect.Data;
using QuantConnect.DataSource;

namespace QuantConnect.DataLibrary.Tests
{
    /// <summary>
    /// Unit tests for the MarketCrunchPredictions data source.
    /// Reads header-less sample data from the repo output/ directory to verify the Reader.
    /// </summary>
    [TestFixture]
    public class MarketCrunchPredictionsTests
    {
        private readonly string _sampleDataPath = Path.Combine(
            "output", "alternative", "marketcrunch", "predictions", "aapl.csv");

        private SubscriptionDataConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = new SubscriptionDataConfig(
                typeof(MarketCrunchPredictions),
                Symbol.Create("AAPL", SecurityType.Base, Market.USA),
                Resolution.Daily,
                TimeZones.NewYork,
                TimeZones.NewYork,
                false,
                false,
                false
            );
        }

        [Test]
        public void SampleDataFileExists()
        {
            Assert.IsTrue(File.Exists(_sampleDataPath),
                $"Sample data file not found at {_sampleDataPath}.");
        }

        [Test]
        public void ReaderParsesAllSampleDataLines()
        {
            Assert.IsTrue(File.Exists(_sampleDataPath), $"Missing: {_sampleDataPath}");

            var lines = File.ReadAllLines(_sampleDataPath)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            Assert.IsNotEmpty(lines, "Sample data file is empty");

            var instance = new MarketCrunchPredictions();
            var parsed = 0;

            foreach (var line in lines)
            {
                var result = instance.Reader(_config, line, DateTime.UtcNow, false) as MarketCrunchPredictions;

                // Header lines (if any) parse to null and are skipped.
                if (result == null) continue;
                parsed++;

                Assert.AreEqual(_config.Symbol, result.Symbol, $"Wrong Symbol for line: {line}");
                Assert.AreNotEqual(default(DateTime), result.Time, $"Time not set for line: {line}");
                Assert.AreNotEqual(default(DateTime), result.PredictionDate, $"PredictionDate not set for line: {line}");
                Assert.AreNotEqual(0m, result.PredictionPrice, $"PredictionPrice is zero for line: {line}");
                // The data is instantaneous (no period): EndTime must equal Time.
                Assert.AreEqual(result.Time, result.EndTime, $"Data must have no period for line: {line}");
                // Emitted at 5:30pm ET on the cut-off day.
                Assert.AreEqual(new TimeSpan(17, 30, 0), result.Time.TimeOfDay, $"Expected 5:30pm emission for line: {line}");
            }

            Assert.Greater(parsed, 0, "Expected at least one parsed data row");
        }

        [Test]
        public void CloneCopiesAllPropertiesFromSampleData()
        {
            var firstLine = File.ReadLines(_sampleDataPath)
                .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l) && char.IsDigit(l.TrimStart()[0]));
            Assert.IsNotNull(firstLine, "Sample data file has no data lines");

            var instance = new MarketCrunchPredictions();
            var original = instance.Reader(_config, firstLine, DateTime.UtcNow, false) as MarketCrunchPredictions;
            Assert.IsNotNull(original);

            var clone = original.Clone() as MarketCrunchPredictions;
            Assert.IsNotNull(clone);
            Assert.AreEqual(original.Symbol, clone.Symbol);
            Assert.AreEqual(original.Time, clone.Time);
            Assert.AreEqual(original.EndTime, clone.EndTime);
            Assert.AreEqual(original.Value, clone.Value);
            Assert.AreEqual(original.PredictionPrice, clone.PredictionPrice);
            Assert.AreEqual(original.PredictionChange, clone.PredictionChange);
            Assert.AreEqual(original.PredConfidence, clone.PredConfidence);
            Assert.AreEqual(original.Last7DAccuracy, clone.Last7DAccuracy);
            Assert.AreEqual(original.Last30DAccuracy, clone.Last30DAccuracy);
            Assert.AreEqual(original.Last90DAccuracy, clone.Last90DAccuracy);
            Assert.AreEqual(original.ModelVersion, clone.ModelVersion);
            Assert.AreEqual(original.PredictionDate, clone.PredictionDate);
            Assert.AreEqual(original.CutoffDate, clone.CutoffDate);
            Assert.AreEqual(original.CreateDate, clone.CreateDate);
        }

        [Test]
        public void ToStringReturnsNonEmpty()
        {
            var firstLine = File.ReadLines(_sampleDataPath)
                .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l) && char.IsDigit(l.TrimStart()[0]));
            Assert.IsNotNull(firstLine);

            var instance = new MarketCrunchPredictions();
            var result = instance.Reader(_config, firstLine, DateTime.UtcNow, false) as MarketCrunchPredictions;
            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result.ToString());
        }

        [Test]
        public void GetSourceReturnsLocalFile()
        {
            var instance = new MarketCrunchPredictions();
            var source = instance.GetSource(_config, DateTime.UtcNow, false);
            Assert.IsNotNull(source);
            Assert.AreEqual(SubscriptionTransportMedium.LocalFile, source.TransportMedium);
        }

        [Test]
        public void DefaultResolutionIsCorrect()
        {
            Assert.AreEqual(Resolution.Daily, new MarketCrunchPredictions().DefaultResolution());
        }

        [Test]
        public void RequiresMappingIsCorrect()
        {
            Assert.AreEqual(true, new MarketCrunchPredictions().RequiresMapping());
        }

        [Test]
        public void IsSparseDataIsCorrect()
        {
            Assert.AreEqual(true, new MarketCrunchPredictions().IsSparseData());
        }

        /// <summary>
        /// Serializes and deserializes a fully-populated instance through JSON and asserts
        /// every property round-trips. Catches missing/incorrect serialization attributes.
        /// </summary>
        [Test]
        public void JsonRoundTrip()
        {
            var expected = CreateNewInstance();
            var type = expected.GetType();
            var serialized = JsonConvert.SerializeObject(expected);
            var result = JsonConvert.DeserializeObject(serialized, type);

            AssertAreEqual(expected, result);
        }

        private static void AssertAreEqual(object expected, object result)
        {
            foreach (var propertyInfo in expected.GetType().GetProperties())
            {
                // Skip indexers and write-only properties.
                if (propertyInfo.GetIndexParameters().Length != 0 || !propertyInfo.CanRead)
                {
                    continue;
                }
                Assert.AreEqual(propertyInfo.GetValue(expected), propertyInfo.GetValue(result), propertyInfo.Name);
            }
            foreach (var fieldInfo in expected.GetType().GetFields())
            {
                Assert.AreEqual(fieldInfo.GetValue(expected), fieldInfo.GetValue(result), fieldInfo.Name);
            }
        }

        private static MarketCrunchPredictions CreateNewInstance()
        {
            return new MarketCrunchPredictions
            {
                Symbol = Symbol.Empty,
                Time = new DateTime(2024, 3, 19, 17, 30, 0),
                PredictionDate = new DateTime(2024, 3, 20),
                CreateDate = new DateTime(2026, 6, 12),
                PredictionPrice = 184.53m,
                PredictionChange = 0.000009m,
                PredConfidence = 99,
                Last7DAccuracy = 71.43m,
                Last30DAccuracy = 53.33m,
                Last90DAccuracy = 47.78m,
                ModelVersion = "mc-eod-v1",
                ProducedTime = new DateTime(2026, 6, 12, 21, 48, 33),
                CutoffDate = new DateTime(2024, 3, 19),
                Value = 184.53m
            };
        }
    }
}
