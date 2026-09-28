using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Mediarion.Benchmarks
{
    /// <summary>
    /// Compares a benchmark run against <c>benchmarks/baseline.json</c> and fails when a scenario
    /// has got worse by more than the allowed margin.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The test suite says what a request returns. Nothing in it says how long the request takes
    /// or what it allocates, and the two come apart easily: the processor behaviours were
    /// registered unconditionally for three releases, costing a hundred and ten nanoseconds and
    /// four hundred and eighty bytes on every request in every application, with the whole suite
    /// green throughout.
    /// </para>
    /// <para>
    /// Two things are compared. The time is a multiple of the reference entrant measured in the
    /// same run on the same machine, never the nanoseconds themselves, because a shared runner is
    /// slow and erratic in ways that move both numbers together. The allocation is the byte count
    /// itself, held as a ceiling and with no tolerance, because allocation per operation is
    /// decided by the code and not by the machine: it does not move between runners at all.
    /// </para>
    /// </remarks>
    internal static class Budget
    {
        internal static int Check(string baselineFile, string resultsDirectory)
        {
            if (!File.Exists(baselineFile))
            {
                Console.Error.WriteLine("No baseline at " + baselineFile + ".");
                return 1;
            }

            if (!Directory.Exists(resultsDirectory))
            {
                Console.Error.WriteLine(
                    "No results at " + resultsDirectory + ". The benchmarks have to run first, " +
                    "with --exporters json.");
                return 1;
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(baselineFile));
            JsonElement root = document.RootElement;

            double tolerance = root.GetProperty("tolerance").GetDouble();
            string reference = root.GetProperty("reference").GetString()!;

            Dictionary<(string Type, string Method), Measurement> measured = Measure(resultsDirectory);

            var failures = new List<string>();
            var missing = new List<string>();

            Console.WriteLine(
                "{0,-22} {1,-22} {2,9} {3,9} {4,9} {5,9}   {6}",
                "Scenario", "Entrant", "allowed", "measured", "bytes", "allowed", string.Empty);

            foreach (JsonElement scenario in root.GetProperty("scenarios").EnumerateArray())
            {
                string type = scenario.GetProperty("type").GetString()!;
                string method = scenario.GetProperty("method").GetString()!;
                double expected = scenario.GetProperty("ratio").GetDouble();
                long bytes = scenario.GetProperty("bytes").GetInt64();
                double margin = scenario.TryGetProperty("tolerance", out JsonElement own)
                    ? own.GetDouble()
                    : tolerance;

                double allowed = expected * (1 + margin);

                if (!measured.TryGetValue((type, method), out Measurement entrant)
                    || !measured.TryGetValue((type, reference), out Measurement against))
                {
                    missing.Add(type + "." + method);
                    continue;
                }

                if (against.Mean <= 0)
                {
                    missing.Add(type + "." + reference + ", which the others are measured against");
                    continue;
                }

                double actual = entrant.Mean / against.Mean;
                bool slower = actual > allowed;
                bool fatter = entrant.Bytes > bytes;

                if (slower)
                {
                    failures.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}.{1}: {2:0.000}x of {3}, over the {4:0.000}x allowed (baseline {5:0.000}x plus {6:0}%).",
                        type, method, actual, reference, allowed, expected, margin * 100));
                }

                if (fatter)
                {
                    failures.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}.{1}: {2} bytes per request, over the {3} allowed. Allocation does not " +
                        "vary between machines, so this is the code and not the runner.",
                        type, method, entrant.Bytes, bytes));
                }

                Console.WriteLine(
                    "{0,-22} {1,-22} {2,9} {3,9} {4,9} {5,9}   {6}",
                    type.Replace("Benchmarks", string.Empty),
                    method,
                    allowed.ToString("0.000", CultureInfo.InvariantCulture) + "x",
                    actual.ToString("0.000", CultureInfo.InvariantCulture) + "x",
                    entrant.Bytes.ToString(CultureInfo.InvariantCulture) + " B",
                    bytes.ToString(CultureInfo.InvariantCulture) + " B",
                    slower || fatter ? "WORSE" : "ok");
            }

            foreach (string absent in missing)
            {
                Console.Error.WriteLine("Not measured: " + absent + ". The run did not include it.");
            }

            foreach (string failure in failures)
            {
                Console.Error.WriteLine(failure);
            }

            if (failures.Count == 0 && missing.Count == 0)
            {
                Console.WriteLine();
                Console.WriteLine("Every scenario is within its budget.");
                return 0;
            }

            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "If the change is meant to cost this, raise the number in benchmarks/baseline.json " +
                "in the same pull request, so the cost is agreed rather than discovered.");

            return 1;
        }

        private static Dictionary<(string, string), Measurement> Measure(string directory)
        {
            var measurements = new Dictionary<(string, string), Measurement>();

            foreach (string file in Directory.EnumerateFiles(directory, "*-report-full-compressed.json"))
            {
                using JsonDocument report = JsonDocument.Parse(File.ReadAllText(file));

                foreach (JsonElement entry in report.RootElement.GetProperty("Benchmarks").EnumerateArray())
                {
                    long bytes = entry.TryGetProperty("Memory", out JsonElement memory)
                        && memory.ValueKind == JsonValueKind.Object
                        && memory.TryGetProperty("BytesAllocatedPerOperation", out JsonElement allocated)
                            ? allocated.GetInt64()
                            : -1;

                    measurements[(entry.GetProperty("Type").GetString()!, entry.GetProperty("Method").GetString()!)] =
                        new Measurement(
                            entry.GetProperty("Statistics").GetProperty("Mean").GetDouble(),
                            bytes);
                }
            }

            return measurements;
        }

        private readonly struct Measurement
        {
            internal Measurement(double mean, long bytes)
            {
                Mean = mean;
                Bytes = bytes;
            }

            internal double Mean { get; }

            internal long Bytes { get; }
        }
    }
}
