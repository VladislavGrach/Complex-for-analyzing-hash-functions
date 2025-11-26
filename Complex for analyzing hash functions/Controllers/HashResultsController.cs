using Microsoft.AspNetCore.Mvc;
using Complex_for_analyzing_hash_functions.Data;
using Complex_for_analyzing_hash_functions.Models;
using System.Linq;

namespace Complex_for_analyzing_hash_functions.Controllers
{
    public class HashResultsController : Controller
    {
        private readonly ApplicationDbContext _db;

        public HashResultsController(ApplicationDbContext db)
        {
            _db = db;
        }

        // GET: /HashResults
        public IActionResult Index()
        {
            var results = _db.HashTestResults
                             .OrderByDescending(r => r.RunDate)
                             .ToList();

            return View(results);
        }
    }
}