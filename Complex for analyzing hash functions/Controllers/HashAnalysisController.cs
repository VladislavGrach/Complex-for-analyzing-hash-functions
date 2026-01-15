using Complex_for_analyzing_hash_functions.Data;
using Complex_for_analyzing_hash_functions.Helpers;
using Complex_for_analyzing_hash_functions.Models;
using Microsoft.AspNetCore.Mvc;
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

        //public IActionResult Index(string algorithm = "Keccak")
        //{
        //    var results = _db.HashTestResults
        //        .Where(r => r.Algorithm == algorithm)
        //        .OrderBy(r => r.Rounds)
        //        .ToList();

        //    var points = results.Select(r =>
        //    {
        //        using var doc = JsonDocument.Parse(r.BitFlipJson);
        //        var root = JsonUtils.NormalizeToObject(doc.RootElement);

        //        JsonElement avalanche = default;
        //        JsonElement bic = default;

        //        bool hasAvalanche = root.TryGetProperty("Avalanche", out avalanche);
        //        bool hasBic = root.TryGetProperty("BIC", out bic);

        //        return new RoundStatisticPoint
        //        {
        //            Rounds = r.Rounds,
        //            AvgHamming = r.AvgHamming,

        //            MeanFlipRate = hasAvalanche && avalanche.TryGetProperty("MeanFlipRate", out var mfr)
        //                ? mfr.GetDouble()
        //                : double.NaN,

        //            StdDevFlipRate = hasAvalanche && avalanche.TryGetProperty("StdDevFlipRate", out var sffr)
        //                ? sffr.GetDouble()
        //                : double.NaN,

        //            BicMaxCorrelation = hasBic && bic.TryGetProperty("MaxCorrelationAbs", out var maxc)
        //                ? maxc.GetDouble()
        //                : double.NaN,

        //            BicStdCorrelation = hasBic && bic.TryGetProperty("StdCorrelation", out var stdc)
        //                ? stdc.GetDouble()
        //                : double.NaN
        //        };
        //    }).ToList();

        //    ViewBag.SelectedAlgorithm = algorithm;
        //    return View(points);
        //}

        public IActionResult Index(string algorithm = "Keccak")
        {
            return RedirectToAction(nameof(Aggregated), new { algorithm });
        }

        public IActionResult Aggregated(string algorithm = "Keccak")
        {
            var raw = _db.HashTestResults
                .Where(r => r.Algorithm == algorithm)
                .ToList();

            var parsed = raw.Select(r =>
            {
                using var doc = JsonDocument.Parse(r.BitFlipJson);
                var root = JsonUtils.NormalizeToObject(doc.RootElement);

                double meanFlip = root.TryGetProperty("Avalanche", out var a) &&
                                  a.TryGetProperty("MeanFlipRate", out var m)
                    ? m.GetDouble()
                    : double.NaN;

                double bicMax = root.TryGetProperty("BIC", out var b) &&
                                b.TryGetProperty("MaxCorrelationAbs", out var c)
                    ? c.GetDouble()
                    : double.NaN;

                return new
                {
                    r.Rounds,
                    r.AvgHamming,
                    MeanFlipRate = meanFlip,
                    BicMaxCorrelation = bicMax
                };
            })
            .Where(x =>
                !double.IsNaN(x.AvgHamming) &&
                !double.IsNaN(x.MeanFlipRate) &&
                !double.IsNaN(x.BicMaxCorrelation))
            .ToList();

            var aggregated = parsed
                .GroupBy(x => x.Rounds)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    int n = g.Count();

                    double hStd = Std(g.Select(x => x.AvgHamming));
                    double fStd = Std(g.Select(x => x.MeanFlipRate));
                    double bStd = Std(g.Select(x => x.BicMaxCorrelation));

                    return new RoundStatisticAggregatedPoint
                    {
                        Rounds = g.Key,

                        AvgHammingMean = g.Average(x => x.AvgHamming),
                        AvgHammingStd = hStd,
                        AvgHammingCi = n > 1 ? 1.96 * hStd / Math.Sqrt(n) : 0,

                        MeanFlipRateMean = g.Average(x => x.MeanFlipRate),
                        MeanFlipRateStd = fStd,
                        MeanFlipRateCi = n > 1 ? 1.96 * fStd / Math.Sqrt(n) : 0,

                        BicMaxCorrelationMean = g.Average(x => x.BicMaxCorrelation),
                        BicMaxCorrelationStd = bStd,
                        BicMaxCorrelationCi = n > 1 ? 1.96 * bStd / Math.Sqrt(n) : 0
                    };
                })
                .ToList();

            ViewBag.Algorithm = algorithm;
            return View(aggregated);
        }


        private static double Std(IEnumerable<double> values)
        {
            var arr = values.Where(v => !double.IsNaN(v)).ToArray();
            if (arr.Length < 2) return 0;

            double mean = arr.Average();
            return Math.Sqrt(arr.Sum(v => (v - mean) * (v - mean)) / (arr.Length - 1));
        }

        public IActionResult Compare(int rounds = 8)
        {
            var algorithms = new[] { "Keccak", "Blake", "Blake2s", "Blake2b", "Blake3" };

            var results = _db.HashTestResults
                .Where(r => r.Rounds == rounds && algorithms.Contains(r.Algorithm))
                .ToList();

            var parsed = results.Select(r =>
            {
                using var doc = JsonDocument.Parse(r.BitFlipJson);
                var root = JsonUtils.NormalizeToObject(doc.RootElement);

                double sac = root.TryGetProperty("Avalanche", out var a) &&
                             a.TryGetProperty("MeanFlipRate", out var mfr)
                    ? mfr.GetDouble()
                    : double.NaN;

                double bic = root.TryGetProperty("BIC", out var b) &&
                             b.TryGetProperty("MaxCorrelationAbs", out var mc)
                    ? mc.GetDouble()
                    : double.NaN;

                return new
                {
                    r.Algorithm,
                    r.AvgHamming,
                    Sac = sac,
                    Bic = bic
                };
            })
            .Where(x =>
                !double.IsNaN(x.Sac) &&
                !double.IsNaN(x.Bic) &&
                !double.IsNaN(x.AvgHamming))
            .ToList();

            var comparison = parsed
                .GroupBy(x => x.Algorithm)
                .Select(g =>
                {
                    var n = g.Count();
                    return new AlgorithmComparisonPoint
                    {
                        Algorithm = g.Key,

                        SacMean = g.Average(x => x.Sac),
                        SacStd = n > 1 ? Std(g.Select(x => x.Sac)) : 0,

                        BicMean = g.Average(x => x.Bic),
                        BicStd = n > 1 ? Std(g.Select(x => x.Bic)) : 0,

                        HammingMean = g.Average(x => x.AvgHamming),
                        HammingStd = n > 1 ? Std(g.Select(x => x.AvgHamming)) : 0
                    };
                })
                .OrderBy(x => x.Algorithm)
                .ToList();

            ViewBag.Rounds = rounds;
            return View(comparison);
        }

    }
}
