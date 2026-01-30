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

        static readonly string[] NistTests =
        {
            "Monobit",
            "FrequencyWithinBlock",
            "Runs",
            "LongestRunOfOnes",
            "BinaryMatrixRank",
            "DiscreteFourier",
            "NonOverlappingTemplate",
            "OverlappingTemplate",
            "MaurerUniversal",
            "LempelZiv",
            "LinearComplexity",
            "Serial",
            "ApproximateEntropy",
            "Cusum",
            "RandomExcursions",
            "RandomExcursionsVariant"
        };

        static readonly string[] DiehardTests =
        {
            "BirthdaySpacings",
            "CountOnes",
            "MatrixRanks",
            "OverlappingPermutations",
            "RunsDiehard",
            "GcdDiehard",
            "SqueezeDiehard",
            "CrapsDiehard"
        };

        static readonly string[] TestU01Tests =
        {
            "Collision",
            "Gap",
            "Autocorrelation",
            "Spectral",
            "HammingWeight",
            "SerialTest",
            "MultinomialTest",
            "ClosePairs",
            "CouponCollector"
        };

        public IActionResult Aggregated(string algorithm = "Keccak", string suite = "diff")
        {
            var raw = _db.HashTestResults
                .Where(r => r.Algorithm == algorithm)
                .ToList();

            var parsed = raw.Select(r =>
            {
                using var doc = JsonDocument.Parse(r.BitFlipJson);
                var root = JsonUtils.NormalizeToObject(doc.RootElement);

                var metrics = new Dictionary<string, double?>();

                // === NIST ===
                if (root.TryGetProperty("NIST", out var nist))
                {
                    foreach (var test in NistTests)
                    {
                        if (nist.TryGetProperty(test, out var v))
                            metrics[test] = v.GetDouble();
                        else
                            metrics[test] = null;
                    }
                }

                // ===== DIEHARD =====
                if (root.TryGetProperty("Diehard", out var diehard))
                {
                    foreach (var test in DiehardTests)
                    {
                        if (diehard.TryGetProperty(test, out var v))
                            metrics[test] = v.GetDouble();
                        else
                            metrics[test] = null;
                    }
                }

                // ===== TESTU01 =====
                if (root.TryGetProperty("TestU01", out var testu01))
                {
                    foreach (var test in TestU01Tests)
                    {
                        if (testu01.TryGetProperty(test, out var v))
                            metrics[test] = v.GetDouble();
                        else
                            metrics[test] = null;
                    }
                }

                // === SAC ===
                if (root.TryGetProperty("Avalanche", out var a) &&
                    a.TryGetProperty("MeanFlipRate", out var sac))
                {
                    metrics["SAC"] = sac.GetDouble();
                }

                // === BIC ===
                if (root.TryGetProperty("BIC", out var b) &&
                    b.TryGetProperty("MaxCorrelationAbs", out var bic))
                {
                    metrics["BIC"] = bic.GetDouble();
                }

                return new
                {
                    r.Rounds,
                    Metrics = metrics
                };
            })
            .ToList();

            var aggregated = parsed
                .GroupBy(x => x.Rounds)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var point = new RoundStatisticAggregatedPoint
                    {
                        Rounds = g.Key
                    };

                    var allMetricNames = g
                        .SelectMany(x => x.Metrics.Keys)
                        .Distinct();

                    foreach (var name in allMetricNames)
                    {
                        var values = g
                            .Select(x =>
                                x.Metrics.TryGetValue(name, out var v) ? v : null)
                            .Where(v => v.HasValue)
                            .Select(v => v.Value)
                            .ToList();

                        if (values.Count == 0)
                            continue;

                        // BOOTSTRAP КВАНТИЛИ для p-value + обычные расчёты для SAC/BIC
                        if (IsPValueMetric(name))
                        {
                            // Bootstrap 95% квантили: 2.5% и 97.5%
                            var validP = values.Where(p => p >= 0 && p <= 1 && !double.IsNaN(p)).ToList();
                            if (validP.Count == 0) continue;

                            validP.Sort();
                            double meanP = validP.Average();

                            double lowerP, upperP;
                            if (validP.Count <= 5)
                            {
                                // Мало точек — используем min/max
                                lowerP = validP.Min();
                                upperP = validP.Max();
                            }
                            else
                            {
                                // 95% квантили
                                lowerP = validP[(int)(0.025 * (validP.Count - 1))];
                                upperP = validP[(int)(0.975 * (validP.Count - 1))];
                            }

                            double pseudoStd = (upperP - lowerP) / 4.0; // чтобы JS мог делать mean ± pseudoStd

                            point.Metrics[name] = new AggregatedMetric
                            {
                                Mean = meanP,
                                Std = pseudoStd,
                                Ci = pseudoStd,  // совместимость с твоим JS
                                Lower = lowerP,
                                Upper = upperP
                            };
                        }
                        else
                        {
                            // SAC/BIC: обычные расчёты (не p-value)
                            double mean0 = values.Average();
                            double std0 = values.Count > 1 ? Std(values) : 0;

                            point.Metrics[name] = new AggregatedMetric
                            {
                                Mean = mean0,
                                Std = std0,
                                Ci = std0,  // JS использует Ci как "радиус"
                                Lower = mean0 - std0,
                                Upper = mean0 + std0
                            };
                        }
                    }

                    return point;
                })
                .ToList();

            ViewBag.Algorithm = algorithm;
            ViewBag.Suite = suite;

            return View(aggregated);
        }

        private static bool IsPValueMetric(string name)
        {
            return !string.Equals(name, "SAC", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(name, "BIC", StringComparison.OrdinalIgnoreCase);
        }


        private static double Std(IEnumerable<double> values)
        {
            var arr = values.Where(v => !double.IsNaN(v)).ToArray();
            if (arr.Length < 2) return 0;

            double mean = arr.Average();
            return Math.Sqrt(arr.Sum(v => (v - mean) * (v - mean)) / (arr.Length - 1));
        }

        private const double P_EPS = 1e-12;

        private static double Clamp01Open(double p)
        {
            if (double.IsNaN(p)) return double.NaN;
            if (p <= P_EPS) return P_EPS;
            if (p >= 1.0 - P_EPS) return 1.0 - P_EPS;
            return p;
        }

        private static double Logit(double p)
        {
            p = Clamp01Open(p);
            return Math.Log(p / (1.0 - p));
        }

        private static double Expit(double z)
        {
            // численно стабильный вариант
            if (z >= 0)
            {
                var ez = Math.Exp(-z);
                return 1.0 / (1.0 + ez);
            }
            else
            {
                var ez = Math.Exp(z);
                return ez / (1.0 + ez);
            }
        }

        public IActionResult Compare(
            int rounds = 8,
            string suite = "diff",
            string metric = "SAC")
        {
            suite = (suite ?? "diff").Trim();
            metric = (metric ?? "SAC").Trim();

            var algorithms = new[] { "Keccak", "Blake", "Blake2s", "Blake2b", "Blake3" };

            var raw = _db.HashTestResults
                .Where(r =>
                    r.Rounds == rounds &&
                    algorithms.Contains(r.Algorithm))
                .ToList();

            var parsed = raw.Select(r =>
            {
                using var doc = JsonDocument.Parse(r.BitFlipJson);
                var root = JsonUtils.NormalizeToObject(doc.RootElement);

                double value = double.NaN;

                switch (suite)
                {
                    case "diff":
                        if (metric.Equals("SAC", StringComparison.OrdinalIgnoreCase) &&
                            root.TryGetProperty("Avalanche", out var a) &&
                            a.TryGetProperty("MeanFlipRate", out var mfr))
                        {
                            value = mfr.GetDouble();
                        }
                        else if (metric.Equals("BIC", StringComparison.OrdinalIgnoreCase) &&
                            root.TryGetProperty("BIC", out var b) &&
                            b.TryGetProperty("MaxCorrelationAbs", out var mc))
                        {
                            value = mc.GetDouble();
                        }
                        break;

                    case "nist":
                        if (root.TryGetProperty("NIST", out var nist) &&
                            nist.TryGetProperty(metric, out var nval))
                        {
                            value = nval.GetDouble();
                        }
                        break;

                    case "diehard":
                        if (root.TryGetProperty("Diehard", out var diehard) &&
                            diehard.TryGetProperty(metric, out var dval))
                        {
                            value = dval.GetDouble();
                        }
                        break;

                    case "testu01":
                        if (root.TryGetProperty("TestU01", out var testu01) &&
                            testu01.TryGetProperty(metric, out var tval))
                        {
                            value = tval.GetDouble();
                        }
                        break;
                }

                return new
                {
                    r.Algorithm,
                    Value = value
                };
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
            ViewBag.Suite = suite;
            ViewBag.Metric = metric;

            return View(comparison);
        }

        [HttpGet]
        public IActionResult CompareData(int rounds = 8, string suite = "diff", string metric = "SAC")
        {
            suite = (suite ?? "diff").Trim();
            metric = (metric ?? "SAC").Trim();

            var algorithms = new[] { "Keccak", "Blake", "Blake2s", "Blake2b", "Blake3" };

            var raw = _db.HashTestResults
                .Where(r => r.Rounds == rounds && algorithms.Contains(r.Algorithm))
                .ToList();

            var parsed = raw.Select(r =>
            {
                using var doc = JsonDocument.Parse(r.BitFlipJson);
                var root = JsonUtils.NormalizeToObject(doc.RootElement);

                double value = double.NaN;

                switch (suite)
                {
                    case "diff":
                        if (metric.Equals("SAC", StringComparison.OrdinalIgnoreCase) &&
                            root.TryGetProperty("Avalanche", out var a) &&
                            a.TryGetProperty("MeanFlipRate", out var mfr))
                            value = mfr.GetDouble();
                        else if (metric.Equals("BIC", StringComparison.OrdinalIgnoreCase) &&
                            root.TryGetProperty("BIC", out var b) &&
                            b.TryGetProperty("MaxCorrelationAbs", out var mc))
                            value = mc.GetDouble();
                        break;

                    case "nist":
                        if (root.TryGetProperty("NIST", out var nist) && nist.TryGetProperty(metric, out var nval))
                            value = nval.GetDouble();
                        break;

                    case "diehard":
                        if (root.TryGetProperty("Diehard", out var diehard) && diehard.TryGetProperty(metric, out var dval))
                            value = dval.GetDouble();
                        break;

                    case "testu01":
                        if (root.TryGetProperty("TestU01", out var testu01) && testu01.TryGetProperty(metric, out var tval))
                            value = tval.GetDouble();
                        break;
                }

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

            // фронту удобнее так:
            return Json(new
            {
                algorithms = comparison.Select(x => x.Algorithm).ToArray(),
                mean = comparison.Select(x => x.Mean).ToArray()
            });
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
