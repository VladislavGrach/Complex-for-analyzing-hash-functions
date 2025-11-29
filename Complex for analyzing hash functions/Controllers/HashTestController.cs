using Microsoft.AspNetCore.Mvc;
using Complex_for_analyzing_hash_functions.Models;
using Complex_for_analyzing_hash_functions.Services;
using Complex_for_analyzing_hash_functions.Data;
using Complex_for_analyzing_hash_functions.Interfaces;
using System.Security.Policy;

namespace Complex_for_analyzing_hash_functions.Controllers
{
    public class HashTestController : Controller
    {
        private readonly StatisticsService _stats;
        private readonly ApplicationDbContext _db;
        private readonly INistTestingService _nist;
        private readonly IDiehardTestingService _diehard;

        public HashTestController(
            StatisticsService stats,
            ApplicationDbContext db,
            INistTestingService nist,
            IDiehardTestingService diehard)
                {
                    _stats = stats;
                    _db = db;
                    _nist = nist;
                    _diehard = diehard;
                }

        [HttpGet]
        public IActionResult Index()
        {
            return View(model: new HashTestParameters());
        }

        [HttpPost]
        public IActionResult Run(HashTestParameters p)
        {
            // 1) Запуск основной статистики (существующая логика)
            var result = _stats.RunTest(p); // предполагается, что возвращает HashTestResult

            // 2) Подготовка sample input для NIST/Diehard (опционально: можно использовать реальные входы из статистики)
            byte[] sampleInput = new byte[Math.Max(1, p.InputSizeBytes)];
            new Random().NextBytes(sampleInput);

            // 3) Получаем хэш (с указанным числом раундов)
            byte[] sampleHash;
            try
            {
                sampleHash = _stats.Hash(sampleInput, p.Rounds);
            }
            catch (Exception ex)
            {
                // Логируем и продолжаем — если что-то не так с хешированием
                ModelState.AddModelError("", "Ошибка при вычислении хэша для тестов: " + ex.Message);
                // сохраняем базовую статистику без NIST/Diehard
                _db.HashTestResults.Add(result);
                _db.SaveChanges();
                return View("Result", result);
            }

            // 4) Конвертация хэша в строку бит
            string bits = Helpers.BitUtils.BytesToBitString(sampleHash);

            // ========== NIST TESTS ==========
            double monobit = _nist.MonobitTest(bits);
            double freqWithin = _nist.FrequencyTestWithinBlock(bits, 128);
            double runsNist = _nist.RunsTest(bits);
            double longestRun = _nist.LongestRunOfOnesTest(bits, 128);
            double matrixRank = _nist.BinaryMatrixRankTest(bits);
            double dft = _nist.DiscreteFourierTransformTest(bits);
            double nonOverlap = _nist.NonOverlappingTemplateMatchingTest(bits, "000111");
            double overlap = _nist.OverlappingTemplateMatchingTest(bits, 10);
            //double maurer = _nist.MaurersUniversalTest(bits);
            double maurer = _nist.MaurersUniversalTestOnHashStream(
                input => _stats.Hash(input, p.Rounds),
                1_500_000
            );
            //double lempelZiv = _nist.LempelZivCompressionTest(bits);
            double lempelZiv = _nist.LempelZivCompressionTestOnHashStream(
                input => _stats.Hash(input, p.Rounds),
                200_000
            );

            double linearComplexity = _nist.LinearComplexityTest(bits, 32);
            double serial = _nist.SerialTest(bits, 2);
            double approxEntropy = _nist.ApproximateEntropyTest(bits, 2);
            double cusum = _nist.CusumTest(bits);
            double excursions = _nist.RandomExcursionsTestOnHashStream(
                input => _stats.Hash(input, p.Rounds),
                1_500_000
            );

            double excursionsVar = _nist.RandomExcursionsVariantTestOnHashStream(
                input => _stats.Hash(input, p.Rounds),
                1_500_000
            );




            // ========== DIEHARD TESTS ==========
            double birthday = _diehard.BirthdaySpacingsTest(bits);
            double countOnes = _diehard.CountOnesTest(bits);
            double rank = _diehard.RanksOfMatricesTest(bits);
            double overlapPerm = _diehard.OverlappingPermutationsTest(bits);
            double runsDiehard = _diehard.RunsTest(bits);


            // 7) Собираем всё в JSON и записываем в result.BitFlipJson
            var fullStats = new
            {
                Basic = new
                {
                    result.RunDate,
                    result.Algorithm,
                    result.Rounds,
                    result.TestsCount
                },
                NIST = new
                {
                    Monobit = monobit,
                    FrequencyWithinBlock = freqWithin,
                    Runs = runsNist,
                    LongestRunOfOnes = longestRun,
                    BinaryMatrixRank = matrixRank,
                    DiscreteFourier = dft,
                    NonOverlappingTemplate = nonOverlap,
                    OverlappingTemplate = overlap,
                    MaurerUniversal = maurer,
                    LempelZiv = lempelZiv,
                    LinearComplexity = linearComplexity,
                    Serial = serial,
                    ApproximateEntropy = approxEntropy,
                    Cusum = cusum,
                    RandomExcursions = excursions,
                    RandomExcursionsVariant = excursionsVar
                },
                Diehard = new
                {
                    BirthdaySpacings = birthday,
                    CountOnes = countOnes,
                    MatrixRanks = rank,
                    OverlappingPermutations = overlapPerm,
                    RunsDiehard = runsDiehard
                }
            };


            result.BitFlipJson = System.Text.Json.JsonSerializer.Serialize(fullStats, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            // 8) Сохранение в базу
            _db.HashTestResults.Add(result);
            _db.SaveChanges();

            // 9) Вернуть представление
            return View("Result", result);
        }
    }
}
