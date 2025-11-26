using Microsoft.AspNetCore.Mvc;
using Complex_for_analyzing_hash_functions.Models;
using Complex_for_analyzing_hash_functions.Services;
using Complex_for_analyzing_hash_functions.Data;

namespace Complex_for_analyzing_hash_functions.Controllers
{
    public class HashTestController : Controller
    {
        private readonly StatisticsService _stats;
        private readonly ApplicationDbContext _db;

        public HashTestController(StatisticsService stats, ApplicationDbContext db)
        {
            _stats = stats;
            _db = db;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View(model: new HashTestParameters());
        }

        [HttpPost]
        public IActionResult Run(HashTestParameters p)
        {
            var result = _stats.RunTest(p);

            _db.HashTestResults.Add(result);
            _db.SaveChanges();

            return View("Result", result);
        }
    }
}
