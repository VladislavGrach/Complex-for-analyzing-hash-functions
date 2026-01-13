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

        public IActionResult Index(string algorithm = "Keccak")
        {
            var results = _db.HashTestResults
                .Where(r => r.Algorithm == algorithm)
                .OrderBy(r => r.Rounds)
                .ToList();

            var points = results.Select(r =>
            {
                using var doc = JsonDocument.Parse(r.BitFlipJson);
                var root = JsonUtils.NormalizeToObject(doc.RootElement);

                JsonElement avalanche = default;
                JsonElement bic = default;

                bool hasAvalanche = root.TryGetProperty("Avalanche", out avalanche);
                bool hasBic = root.TryGetProperty("BIC", out bic);

                return new RoundStatisticPoint
                {
                    Rounds = r.Rounds,
                    AvgHamming = r.AvgHamming,

                    MeanFlipRate = hasAvalanche && avalanche.TryGetProperty("MeanFlipRate", out var mfr)
                        ? mfr.GetDouble()
                        : double.NaN,

                    StdDevFlipRate = hasAvalanche && avalanche.TryGetProperty("StdDevFlipRate", out var sffr)
                        ? sffr.GetDouble()
                        : double.NaN,

                    BicMaxCorrelation = hasBic && bic.TryGetProperty("MaxCorrelationAbs", out var maxc)
                        ? maxc.GetDouble()
                        : double.NaN,

                    BicStdCorrelation = hasBic && bic.TryGetProperty("StdCorrelation", out var stdc)
                        ? stdc.GetDouble()
                        : double.NaN
                };
            }).ToList();

            ViewBag.SelectedAlgorithm = algorithm;
            return View(points);
        }
    }
}
