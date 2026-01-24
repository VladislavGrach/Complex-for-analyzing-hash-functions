using Complex_for_analyzing_hash_functions.Data;
using Complex_for_analyzing_hash_functions.Helpers;
using Complex_for_analyzing_hash_functions.Models;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Complex_for_analyzing_hash_functions.Controllers
{
    public class HashAnalysisController : Controller
    {
        private readonly ApplicationDbContext _db;

        public HashAnalysisController(ApplicationDbContext db)
        {
            _db = db;
        }

        public IActionResult Index(string algorithm = "Keccak")
        {
            return RedirectToAction(nameof(Aggregated), new { algorithm });
        }

        public IActionResult Aggregated(string algorithm = "Keccak", string suite = "diff")
        {
            var raw = _db.HashTestResults
                .Where(r => r.Algorithm == algorithm)
                .ToList();

            var parsed = raw.Select(r =>
            {
                using var doc = JsonDocument.Parse(r.BitFlipJson);
                var root = JsonUtils.NormalizeToObject(doc.RootElement);

                double sac =
                    root.TryGetProperty("Avalanche", out var a) &&
                    a.TryGetProperty("MeanFlipRate", out var m)
                        ? m.GetDouble()
                        : double.NaN;

                double bic =
                    root.TryGetProperty("BIC", out var b) &&
                    b.TryGetProperty("MaxCorrelationAbs", out var c)
                        ? c.GetDouble()
                        : double.NaN;

                double monobit =
                    root.TryGetProperty("NIST", out var n) &&
                    n.TryGetProperty("Monobit", out var mb)
                        ? mb.GetDouble()
                        : double.NaN;

                return new
                {
                    r.Rounds,
                    Sac = sac,
                    Bic = bic,
                    Monobit = monobit
                };
            })
            .Where(x =>
                !double.IsNaN(x.Sac) &&
                !double.IsNaN(x.Bic) &&
                !double.IsNaN(x.Monobit))
            .ToList();

            var aggregated = parsed
                .GroupBy(x => x.Rounds)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    int n = g.Count();

                    double sacStd = n > 1 ? Std(g.Select(x => x.Sac)) : 0;
                    double bicStd = n > 1 ? Std(g.Select(x => x.Bic)) : 0;
                    double monoStd = n > 1 ? Std(g.Select(x => x.Monobit)) : 0;

                    return new RoundStatisticAggregatedPoint
                    {
                        Rounds = g.Key,
                        Metrics =
                        {
                            ["SAC"] = new AggregatedMetric
                            {
                                Mean = g.Average(x => x.Sac),
                                Std  = sacStd,
                                Ci   = n > 1 ? 1.96 * sacStd / Math.Sqrt(n) : 0
                            },
                            ["BIC"] = new AggregatedMetric
                            {
                                Mean = g.Average(x => x.Bic),
                                Std  = bicStd,
                                Ci   = n > 1 ? 1.96 * bicStd / Math.Sqrt(n) : 0
                            },
                            ["Monobit"] = new AggregatedMetric
                            {
                                Mean = g.Average(x => x.Monobit),
                                Std  = monoStd,
                                Ci   = n > 1 ? 1.96 * monoStd / Math.Sqrt(n) : 0
                            }
                        }
                    };
                })
                .ToList();

            ViewBag.Algorithm = algorithm;
            ViewBag.Suite = suite;

            return View(aggregated);
        }

        private static double Std(IEnumerable<double> values)
        {
            var arr = values.Where(v => !double.IsNaN(v)).ToArray();
            if (arr.Length < 2) return 0;

            double mean = arr.Average();
            return Math.Sqrt(arr.Sum(v => (v - mean) * (v - mean)) / (arr.Length - 1));
        }

        public IActionResult Compare(int rounds = 8, string metric = "sac")
        {
            var algorithms = new[] { "Keccak", "Blake", "Blake2s", "Blake2b", "Blake3" };
            metric = (metric ?? "sac").Trim().ToLowerInvariant();

            var raw = _db.HashTestResults
                .Where(r => r.Rounds == rounds && algorithms.Contains(r.Algorithm))
                .ToList();

            // Берём только выбранную метрику => меньше шансов получить пусто из-за NaN в других полях
            var parsed = raw.Select(r =>
            {
                using var doc = JsonDocument.Parse(r.BitFlipJson);
                var root = JsonUtils.NormalizeToObject(doc.RootElement);

                double value = metric switch
                {
                    "sac" => (root.TryGetProperty("Avalanche", out var a) &&
                              a.ValueKind == JsonValueKind.Object &&
                              a.TryGetProperty("MeanFlipRate", out var mfr) &&
                              mfr.ValueKind == JsonValueKind.Number)
                             ? mfr.GetDouble()
                             : double.NaN,

                    "bic" => (root.TryGetProperty("BIC", out var b) &&
                              b.ValueKind == JsonValueKind.Object &&
                              b.TryGetProperty("MaxCorrelationAbs", out var mc) &&
                              mc.ValueKind == JsonValueKind.Number)
                             ? mc.GetDouble()
                             : double.NaN,

                    "mono" => (root.TryGetProperty("NIST", out var nist) &&
                               nist.ValueKind == JsonValueKind.Object &&
                               nist.TryGetProperty("Monobit", out var mb) &&
                               mb.ValueKind == JsonValueKind.Number)
                              ? mb.GetDouble()
                              : double.NaN,

                    _ => double.NaN
                };

                return new { r.Algorithm, Value = value };
            })
            .Where(x => !double.IsNaN(x.Value))
            .ToList();

            var comparison = parsed
                .GroupBy(x => x.Algorithm)
                .Select(g =>
                {
                    var arr = g.Select(x => x.Value).ToArray();

                    return new AlgorithmComparisonPoint
                    {
                        Algorithm = g.Key,
                        Mean = arr.Average(),
                        Std = arr.Length > 1 ? Std(arr) : 0.0
                    };
                })
                .OrderBy(x => x.Algorithm)
                .ToList();

            ViewBag.Rounds = rounds;
            ViewBag.Metric = metric; // "sac"/"bic"/"mono"

            return View(comparison);
        }

        public IActionResult AggregatedExport(string algorithm = "Keccak", string format = "csv")
        {
            var raw = _db.HashTestResults
                .Where(r => r.Algorithm == algorithm)
                .ToList();

            var parsed = raw.Select(r =>
            {
                using var doc = JsonDocument.Parse(r.BitFlipJson);
                var root = JsonUtils.NormalizeToObject(doc.RootElement);

                double sac =
                    root.TryGetProperty("Avalanche", out var a) &&
                    a.TryGetProperty("MeanFlipRate", out var m)
                        ? m.GetDouble()
                        : double.NaN;

                double bic =
                    root.TryGetProperty("BIC", out var b) &&
                    b.TryGetProperty("MaxCorrelationAbs", out var c)
                        ? c.GetDouble()
                        : double.NaN;

                double mono =
                    root.TryGetProperty("NIST", out var n) &&
                    n.TryGetProperty("Monobit", out var mb)
                        ? mb.GetDouble()
                        : double.NaN;

                return new
                {
                    r.Rounds,
                    Sac = sac,
                    Bic = bic,
                    Mono = mono
                };
            })
            .Where(x =>
                !double.IsNaN(x.Sac) &&
                !double.IsNaN(x.Bic) &&
                !double.IsNaN(x.Mono))
            .ToList();

            var aggregated = parsed
                .GroupBy(x => x.Rounds)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    int n = g.Count();

                    double sacStd = n > 1 ? Std(g.Select(x => x.Sac)) : 0;
                    double bicStd = n > 1 ? Std(g.Select(x => x.Bic)) : 0;
                    double monoStd = n > 1 ? Std(g.Select(x => x.Mono)) : 0;

                    return new
                    {
                        Rounds = g.Key,

                        SacMean = g.Average(x => x.Sac),
                        SacCi = n > 1 ? 1.96 * sacStd / Math.Sqrt(n) : 0,

                        BicMean = g.Average(x => x.Bic),
                        BicCi = n > 1 ? 1.96 * bicStd / Math.Sqrt(n) : 0,

                        MonobitMean = g.Average(x => x.Mono),
                        MonobitCi = n > 1 ? 1.96 * monoStd / Math.Sqrt(n) : 0
                    };
                })
                .ToList();

            // ===== JSON =====
            if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
                };

                var json = JsonSerializer.Serialize(aggregated, jsonOptions);
                return File(
                    Encoding.UTF8.GetBytes(json),
                    "application/json",
                    $"aggregated_{algorithm}.json"
                );
            }

            // ===== CSV =====
            var sb = new StringBuilder();
            sb.AppendLine("Rounds,SAC_Mean,SAC_CI,BIC_Mean,BIC_CI,Monobit_Mean,Monobit_CI");

            foreach (var x in aggregated)
            {
                sb.AppendLine(string.Join(",",
                    x.Rounds,
                    x.SacMean.ToString(CultureInfo.InvariantCulture),
                    x.SacCi.ToString(CultureInfo.InvariantCulture),
                    x.BicMean.ToString(CultureInfo.InvariantCulture),
                    x.BicCi.ToString(CultureInfo.InvariantCulture),
                    x.MonobitMean.ToString(CultureInfo.InvariantCulture),
                    x.MonobitCi.ToString(CultureInfo.InvariantCulture)
                ));
            }

            return File(
                Encoding.UTF8.GetBytes(sb.ToString()),
                "text/csv",
                $"aggregated_{algorithm}.csv"
            );
        }
    }
}
