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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using QuantConnect;
using QuantConnect.Configuration;

namespace QuantConnect.DataProcessing
{
    /// <summary>
    /// Data processing program for the MarketCrunchPredictions dataset.
    ///
    /// MarketCrunch produces next-trading-day predictions each evening (~5:30pm ET) from its
    /// end-of-day model and delivers them as a CSV export (one row per covered ticker) in the
    /// agreed 12-column schema. This program has two modes:
    ///
    ///   - INCREMENTAL (the mode QuantConnect's data fleet runs): when
    ///     QC_DATAFLEET_DEPLOYMENT_DATE is set, it reads that day's export, reads the existing
    ///     per-symbol files from Globals.DataFolder, merges the new rows in, de-duplicates by
    ///     prediction date, sorts, and writes each file back.
    ///   - FULL HISTORY: when no deployment date is set, the export is treated as the full
    ///     history and per-symbol files are rebuilt from scratch (no prior data is read).
    ///
    /// In both modes the merged result is written to the temp output directory via an atomic
    /// write (write to a .tmp file, then move into place) — never directly to the repo output/.
    ///
    /// Column order (no header on disk):
    ///   create_date, ticker, prediction_date, prediction_price, prediction_change,
    ///   pred_confidence, last_7d_accuracy, last_30d_accuracy, last_90d_accuracy,
    ///   model_version, time, cutoff_date
    ///
    /// Configuration:
    ///   - Config.Get("temp-output-directory", "/temp-output-directory") – output path
    ///   - Config.Get("marketcrunch-daily-file")                          – path to the export CSV
    ///   - QC_DATAFLEET_DEPLOYMENT_DATE env var (yyyyMMdd)                 – set => incremental mode
    ///
    /// Processing times (246 tickers, ~308,800 rows; Ubuntu 22.04 aarch64):
    ///   Full dataset:   ~2.0s
    ///   One day update: ~0.3-0.5s
    /// </summary>
    public class Program
    {
        private const int PredictionDateIdx = 2;   // prediction_date column, MM-dd-yyyy
        private const string DateFmt = "MM-dd-yyyy";

        private static readonly string RelativeDataPath =
            Path.Combine("alternative", "marketcrunch", "predictions");

        public static void Main(string[] args)
        {
            var leanDataFolder = Globals.DataFolder;
            var tempOutputDir = Config.Get("temp-output-directory", "/temp-output-directory");

            // Path to MarketCrunch's export for this run (produced upstream by the prediction
            // pipeline). One CSV, all covered tickers, agreed schema.
            var exportFile = Config.Get("marketcrunch-daily-file");

            var deploymentDateStr = Environment.GetEnvironmentVariable("QC_DATAFLEET_DEPLOYMENT_DATE");
            var incremental = !string.IsNullOrEmpty(deploymentDateStr);
            var deploymentDate = incremental
                ? DateTime.ParseExact(deploymentDateStr, "yyyyMMdd", CultureInfo.InvariantCulture)
                : DateTime.UtcNow.Date;

            Console.WriteLine($"Mode: {(incremental ? "INCREMENTAL" : "FULL HISTORY")} | export: {exportFile}");

            var sw = Stopwatch.StartNew();

            // Group the export rows by ticker (skip a header row if present).
            var newBySymbol = ReadExport(exportFile);

            foreach (var kvp in newBySymbol)
            {
                var symbol = kvp.Key.ToLowerInvariant();

                // Incremental mode merges with the data already on disk; full-history mode
                // rebuilds from the export alone.
                var existing = incremental
                    ? ReadExisting(Path.Combine(leanDataFolder, RelativeDataPath, $"{symbol}.csv"))
                    : new List<string[]>();

                MergeAndSave(existing, kvp.Value, symbol, tempOutputDir);
            }

            sw.Stop();
            Console.WriteLine($"Processed {newBySymbol.Count} symbols for {deploymentDate:yyyy-MM-dd} in {sw.Elapsed.TotalSeconds:F2}s");
        }

        /// <summary>Read the MarketCrunch export CSV into per-ticker row lists.</summary>
        private static Dictionary<string, List<string[]>> ReadExport(string path)
        {
            var bySymbol = new Dictionary<string, List<string[]>>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Console.WriteLine($"WARNING: export not found at '{path}'");
                return bySymbol;
            }

            foreach (var line in File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(line) || !char.IsDigit(line.TrimStart()[0])) continue; // skip header
                var cols = line.Split(',');
                if (cols.Length < 12) continue;
                var ticker = cols[1];
                if (!bySymbol.TryGetValue(ticker, out var list))
                {
                    list = new List<string[]>();
                    bySymbol[ticker] = list;
                }
                list.Add(cols);
            }
            return bySymbol;
        }

        private static List<string[]> ReadExisting(string path)
        {
            if (!File.Exists(path)) return new List<string[]>();
            return File.ReadAllLines(path)
                .Where(l => !string.IsNullOrWhiteSpace(l) && char.IsDigit(l.TrimStart()[0]))
                .Select(l => l.Split(','))
                .ToList();
        }

        /// <summary>
        /// Merge new rows onto existing, de-dup by prediction_date (new wins), sort ascending,
        /// and write to the temp output directory using an atomic write (.tmp then move).
        /// </summary>
        private static void MergeAndSave(
            List<string[]> existing, List<string[]> newRows, string symbol, string tempOutputDir)
        {
            var merged = existing.Concat(newRows)
                .GroupBy(r => r[PredictionDateIdx])
                .Select(g => g.Last())
                .OrderBy(r => DateTime.ParseExact(r[PredictionDateIdx], DateFmt, CultureInfo.InvariantCulture))
                .ToList();

            var outPath = Path.Combine(tempOutputDir, RelativeDataPath, $"{symbol}.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

            // Atomic write: never leave a partially-written file in place if the process dies.
            var tmpPath = outPath + ".tmp";
            File.WriteAllLines(tmpPath, merged.Select(r => string.Join(",", r)));
            File.Move(tmpPath, outPath, overwrite: true);
        }
    }
}
