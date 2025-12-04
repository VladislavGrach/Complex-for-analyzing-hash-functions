using Microsoft.AspNetCore.Mvc;
using Complex_for_analyzing_hash_functions.Models;
using Complex_for_analyzing_hash_functions.Services;
using Complex_for_analyzing_hash_functions.Data;
using Complex_for_analyzing_hash_functions.Interfaces;
using System.Security.Policy;
using Complex_for_analyzing_hash_functions.Helpers;

namespace Complex_for_analyzing_hash_functions.Controllers
{
    public class HashTestController : Controller
    {
        private readonly StatisticsService _stats;
        private readonly ApplicationDbContext _db;
        private readonly INistTestingService _nist;
        private readonly IDiehardTestingService _diehard;
        private readonly ITestU01Service _testu01;

        public HashTestController(
            StatisticsService stats,
            ApplicationDbContext db,
            INistTestingService nist,
            IDiehardTestingService diehard,
            ITestU01Service testu01)
                {
                    _stats = stats;
                    _db = db;
                    _nist = nist;
                    _diehard = diehard;
                    _testu01 = testu01;
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
            //double rank = _diehard.RanksOfMatricesTest(bits);
            double rank = _diehard.RanksOfMatricesTest(
                bits,
                hashFunction: input => _stats.Hash(input, p.Rounds)
            );

            double overlapPerm = _diehard.OverlappingPermutationsTest(bits);
            double runsDiehard = _diehard.RunsTest(bits);
            double gcdP = _diehard.GcdTest(
                bits: "", // пустая строка — будет сделан padding
                hashFunction: input => _stats.Hash(input, rounds: p.Rounds),
                requiredWordsDefault: 200_000
            );
            double squeeze = _diehard.SqueezeTest(bits);
            double craps = _diehard.CrapsTestOnHashStream(
                input => _stats.Hash(input, p.Rounds),
                requiredBits: 1_500_000
            );



            // =========== TestU01 ============
            double collisionU01 = _testu01.CollisionTestOnHashStream(
                hashFunction: input => _stats.Hash(input, p.Rounds),
                requiredBits: 15_000_000,   
                t: 20,
                n: 500_000
            );
            double gapU01 = _testu01.GapTestOnHashStream(
                input => _stats.Hash(input, p.Rounds)
            );
            double autoU01 = _testu01.AutocorrelationTestOnHashStream(
                input => _stats.Hash(input, p.Rounds)
            );
            double spectralU01 = _testu01.SpectralTestOnHashStream(
                input => _stats.Hash(input, p.Rounds)
            );



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
                    Monobit = JsonSanitizer.Fix(monobit),
                    FrequencyWithinBlock = JsonSanitizer.Fix(freqWithin),
                    Runs = JsonSanitizer.Fix(runsNist),
                    LongestRunOfOnes = JsonSanitizer.Fix(longestRun),
                    BinaryMatrixRank = JsonSanitizer.Fix(matrixRank),
                    DiscreteFourier = JsonSanitizer.Fix(dft),
                    NonOverlappingTemplate = JsonSanitizer.Fix(nonOverlap),
                    OverlappingTemplate = JsonSanitizer.Fix(overlap),
                    MaurerUniversal = JsonSanitizer.Fix(maurer),
                    LempelZiv = JsonSanitizer.Fix(lempelZiv),
                    LinearComplexity = JsonSanitizer.Fix(linearComplexity),
                    Serial = JsonSanitizer.Fix(serial),
                    ApproximateEntropy = JsonSanitizer.Fix(approxEntropy),
                    Cusum = JsonSanitizer.Fix(cusum),
                    RandomExcursions = JsonSanitizer.Fix(excursions),
                    RandomExcursionsVariant = JsonSanitizer.Fix(excursionsVar)
                },

                Diehard = new
                {
                    BirthdaySpacings = JsonSanitizer.Fix(birthday),
                    CountOnes = JsonSanitizer.Fix(countOnes),
                    MatrixRanks = JsonSanitizer.Fix(rank),
                    OverlappingPermutations = JsonSanitizer.Fix(overlapPerm),
                    RunsDiehard = JsonSanitizer.Fix(runsDiehard),
                    GcdDiehard = JsonSanitizer.Fix(gcdP),
                    SqueezeDiehard = JsonSanitizer.Fix(squeeze),
                    CrapsDiehard = JsonSanitizer.Fix(craps)
                },

                TestU01 = new
                {
                    Collision = JsonSanitizer.Fix(collisionU01),
                    Gap = JsonSanitizer.Fix(gapU01),
                    Autocorrelation = JsonSanitizer.Fix(autoU01),
                    Spectral = JsonSanitizer.Fix(spectralU01)
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
