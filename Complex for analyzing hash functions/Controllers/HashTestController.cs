using Microsoft.AspNetCore.Mvc;
using Complex_for_analyzing_hash_functions.Models;
using Complex_for_analyzing_hash_functions.Services;
using Complex_for_analyzing_hash_functions.Data;
using Complex_for_analyzing_hash_functions.Interfaces;
using Complex_for_analyzing_hash_functions.Helpers;

namespace Complex_for_analyzing_hash_functions.Controllers
{
    public class HashTestController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly INistTestingService _nist;
        private readonly IDiehardTestingService _diehard;
        private readonly ITestU01Service _testu01;
        private readonly IAvalancheService _avalanche;
        private readonly IBitIndependenceService _bic;

        public HashTestController(
            ApplicationDbContext db,
            INistTestingService nist,
            IDiehardTestingService diehard,
            ITestU01Service testu01,
            IAvalancheService avalanche,
            IBitIndependenceService bic)
        {
            _db = db;
            _nist = nist;
            _diehard = diehard;
            _testu01 = testu01;
            _avalanche = avalanche;
            _bic = bic;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View(model: new HashTestParameters());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Run(HashTestParameters p)
        {
            if (!ModelState.IsValid)
            {
                return View("Index", p);
            }

            // --- 0. Создаём хэшер ---
            IHashFunction hasher = AlgorithmSelector.Create(p.Algorithm);

            // --- 1. Запуск основной статистики ---
            var stats = new StatisticsService(hasher);
            var result = stats.RunTest(p);

            // --- 2. Sample input ---
            byte[] sampleInput = new byte[Math.Max(1, p.InputSizeBytes)];
            new Random().NextBytes(sampleInput);

            // --- 3. Получаем sample hash ---
            byte[] sampleHash;
            try
            {
                sampleHash = hasher.ComputeHash(sampleInput, p.Rounds);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Hashing error: " + ex.Message);
                _db.HashTestResults.Add(result);
                _db.SaveChanges();
                return View("Result", result);
            }

            // --- 4. BIT STRING ---
            string bits = BitUtils.BytesToBitString(sampleHash);

            // === NIST TESTS ===
            double monobit = _nist.MonobitTest(bits);
            double freqWithin = _nist.FrequencyTestWithinBlock(bits, 128);
            double runsNist = _nist.RunsTest(bits);
            double longestRun = _nist.LongestRunOfOnesTest(bits, 128);
            double matrixRank = _nist.BinaryMatrixRankTest(bits);
            double dft = _nist.DiscreteFourierTransformTest(bits);
            double nonOverlap = _nist.NonOverlappingTemplateMatchingTest(bits, "000111");
            double overlap = _nist.OverlappingTemplateMatchingTest(bits, 10);

            double maurer = _nist.MaurersUniversalTestOnHashStream(
                input => hasher.ComputeHash(input, p.Rounds),
                1_500_000);

            double lempelZiv = _nist.LempelZivCompressionTestOnHashStream(
                input => hasher.ComputeHash(input, p.Rounds),
                200_000);

            double linearComplexity = _nist.LinearComplexityTest(bits, 32);
            double serial = _nist.SerialTest(bits, 2);
            double approxEntropy = _nist.ApproximateEntropyTest(bits, 2);
            double cusum = _nist.CusumTest(bits);

            double excursions = _nist.RandomExcursionsTestOnHashStream(
                input => hasher.ComputeHash(input, p.Rounds),
                1_500_000);

            double excursionsVar = _nist.RandomExcursionsVariantTestOnHashStream(
                input => hasher.ComputeHash(input, p.Rounds),
                1_500_000);

            // === DIEHARD TESTS ===
            double birthday = _diehard.BirthdaySpacingsTest(bits);
            double countOnes = _diehard.CountOnesTest(bits);

            double rank = _diehard.RanksOfMatricesTest(
                bits,
                input => hasher.ComputeHash(input, p.Rounds));

            double overlapPerm = _diehard.OverlappingPermutationsTest(bits);
            double runsDiehard = _diehard.RunsTest(bits);

            double gcd = _diehard.GcdTest(
                bits: "",
                hashFunction: input => hasher.ComputeHash(input, p.Rounds),
                requiredWordsDefault: 200_000);

            double squeeze = _diehard.SqueezeTest(bits);

            double craps = _diehard.CrapsTestOnHashStream(
                input => hasher.ComputeHash(input, p.Rounds),
                requiredBits: 1_500_000);

            // === TEST U01 ===
            double collisionU01 = _testu01.CollisionTestOnHashStream(
                input => hasher.ComputeHash(input, p.Rounds),
                requiredBits: 15_000_000,
                t: 20,
                n: 500_000);

            double gapU01 = _testu01.GapTestOnHashStream(
                input => hasher.ComputeHash(input, p.Rounds));

            double autoU01 = _testu01.AutocorrelationTestOnHashStream(
                input => hasher.ComputeHash(input, p.Rounds));

            double spectralU01 = _testu01.SpectralTestOnHashStream(
                input => hasher.ComputeHash(input, p.Rounds));

            double hammingU01 = _testu01.HammingWeightTestOnHashStream(
                input => hasher.ComputeHash(input, p.Rounds));

            double serialU01 = _testu01.SerialTestOnHashStream(
                input => hasher.ComputeHash(input, p.Rounds));

            double multinomialU01 = _testu01.MultinomialTestOnHashStream(
                input => hasher.ComputeHash(input, p.Rounds));

            double closePairsU01 = _testu01.ClosePairsTestOnHashStream(
                input => hasher.ComputeHash(input, p.Rounds));

            double couponU01 = _testu01.CouponCollectorTestOnHashStream(
                input => hasher.ComputeHash(input, p.Rounds));

            // ==== SAC & BIC ====
            var sac = _avalanche.ComputeSAC(
                input => hasher.ComputeHash(input, p.Rounds),
                p.InputSizeBytes,
                trials: 1000);

            var bic = _bic.ComputeBIC(
                input => hasher.ComputeHash(input, p.Rounds),
                inputBitLength: p.InputSizeBytes * 8,
                rounds: p.Rounds,
                experimentsPerBit: 200);

            // ==== RESULT JSON ====
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
                    GcdDiehard = JsonSanitizer.Fix(gcd),
                    SqueezeDiehard = JsonSanitizer.Fix(squeeze),
                    CrapsDiehard = JsonSanitizer.Fix(craps)
                },

                TestU01 = new
                {
                    Collision = JsonSanitizer.Fix(collisionU01),
                    Gap = JsonSanitizer.Fix(gapU01),
                    Autocorrelation = JsonSanitizer.Fix(autoU01),
                    Spectral = JsonSanitizer.Fix(spectralU01),
                    HammingWeight = JsonSanitizer.Fix(hammingU01),
                    SerialTest = JsonSanitizer.Fix(serialU01),
                    MultinomialTest = JsonSanitizer.Fix(multinomialU01),
                    ClosePairs = JsonSanitizer.Fix(closePairsU01),
                    CouponCollector = JsonSanitizer.Fix(couponU01)
                },

                Avalanche = new
                {
                    MeanFlipRate = JsonSanitizer.Fix(sac.MeanFlipRate),
                    StdDevFlipRate = JsonSanitizer.Fix(sac.StdDevFlipRate),
                    MinPValue = JsonSanitizer.Fix(sac.MinPValue),
                    MaxPValue = JsonSanitizer.Fix(sac.MaxPValue),
                    Notes = sac.Notes
                },

                BIC = new
                {
                    MeanCorrelation = JsonSanitizer.Fix(bic.MeanCorrelation),
                    StdCorrelation = JsonSanitizer.Fix(bic.StdCorrelation),
                    MaxCorrelationAbs = JsonSanitizer.Fix(bic.MaxCorrelationAbs),
                    MinCorrelationAbs = JsonSanitizer.Fix(bic.MinCorrelation),
                    Notes = bic.Notes
                }
            };

            result.BitFlipJson = System.Text.Json.JsonSerializer.Serialize(
                fullStats,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            _db.HashTestResults.Add(result);
            _db.SaveChanges();

            return View("Result", result);
        }
    }
}

