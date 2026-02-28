using Microsoft.AspNetCore.Mvc;
using Complex_for_analyzing_hash_functions.Models;
using Complex_for_analyzing_hash_functions.Services;
using Complex_for_analyzing_hash_functions.Data;
using Complex_for_analyzing_hash_functions.Interfaces;
using Complex_for_analyzing_hash_functions.Helpers;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Text.Json.Serialization;

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

        private (int Min, int Max)? GetRoundsRange(string algorithm)
        {
            return algorithm switch
            {
                "Keccak" => (1, 24),
                "Blake" => (1, 14),
                "Blake2s" => (1, 10),
                "Blake2b" => (1, 12),
                "Blake3" => (1, 7),
                _ => null
            };
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Run(HashTestParameters p)
        {
            var range = GetRoundsRange(p.Algorithm);

            if (range.HasValue)
            {
                if (p.Rounds < range.Value.Min || p.Rounds > range.Value.Max)
                {
                    ModelState.AddModelError(
                        nameof(p.Rounds),
                        $"Для алгоритма {p.Algorithm} допустимый диапазон раундов: {range.Value.Min}–{range.Value.Max}."
                    );

                    return View("Index", p);
                }
            }


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
                Console.WriteLine($"Rounds: {p.Rounds}");
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

            const int streamLength = 4_500_000;

            // Списки для хранения результатов каждого запуска
            var monobitList = new List<double>();
            var freqWithinList = new List<double>();
            var runsNistList = new List<double>();
            var longestRunList = new List<double>();
            var matrixRankList = new List<double>();
            var dftList = new List<double>();
            var nonOverlapList = new List<double>();
            var overlapList = new List<double>();
            var maurerList = new List<double>();
            var lempelZivList = new List<double>();
            var linearComplexityList = new List<double>();
            var serialList = new List<double>();
            var approxEntropyList = new List<double>();
            var cusumList = new List<double>();
            var excursionsList = new List<double>();
            var excursionsVarList = new List<double>();

            var birthdayList = new List<double>();
            var countOnesList = new List<double>();
            var overlapPermList = new List<double>();
            var runsDiehardList = new List<double>();
            var squeezeList = new List<double>();
            var rankList = new List<double>();
            var gcdList = new List<double>();
            var crapsList = new List<double>();

            var collisionU01List = new List<double>();
            var gapU01List = new List<double>();
            var autoU01List = new List<double>();
            var spectralU01List = new List<double>();
            var hammingU01List = new List<double>();
            var serialU01List = new List<double>();
            var multinomialU01List = new List<double>();
            var closePairsU01List = new List<double>();
            var couponU01List = new List<double>();

            var sacMeanFlipRateList = new List<double>();
            var sacStdDevFlipRateList = new List<double>();
            var sacMinPValueList = new List<double>();
            var sacMaxPValueList = new List<double>();

            var bicMeanCorrelationList = new List<double>();
            var bicStdCorrelationList = new List<double>();
            var bicMaxCorrelationAbsList = new List<double>();
            var bicMinCorrelationList = new List<double>();

            for (int i = 0; i < p.TestsCount; i++)
            {
                // Новый поток битов для каждого теста
                string streamBits = _nist.GenerateHashStream(
                    input => hasher.ComputeHash(input, p.Rounds),
                    streamLength);

                // NIST
                monobitList.Add(_nist.MonobitTest(bits));
                freqWithinList.Add(_nist.FrequencyTestWithinBlock(bits, 128));
                runsNistList.Add(_nist.RunsTest(bits));
                longestRunList.Add(_nist.LongestRunOfOnesTest(bits, 128));
                matrixRankList.Add(_nist.BinaryMatrixRankTest(streamBits));
                dftList.Add(_nist.DiscreteFourierTransformTest(streamBits));
                nonOverlapList.Add(_nist.NonOverlappingTemplateMatchingTest(streamBits, "000111"));
                overlapList.Add(_nist.OverlappingTemplateMatchingTest(streamBits, 9));
                maurerList.Add(_nist.MaurersUniversalTest(streamBits));
                lempelZivList.Add(_nist.LempelZivCompressionTest(streamBits));
                linearComplexityList.Add(_nist.LinearComplexityTest(bits, 32));
                serialList.Add(_nist.SerialTest(bits, 2));
                approxEntropyList.Add(_nist.ApproximateEntropyTest(bits, 2));
                cusumList.Add(_nist.CusumTest(bits));
                excursionsList.Add(_nist.RandomExcursionsTest(streamBits));
                excursionsVarList.Add(_nist.RandomExcursionsVariantTest(streamBits));

                // Diehard
                birthdayList.Add(_diehard.BirthdaySpacingsTest(streamBits));
                countOnesList.Add(_diehard.CountOnesTest(bits));
                overlapPermList.Add(_diehard.OverlappingPermutationsTest(bits));
                runsDiehardList.Add(_diehard.RunsTest(streamBits));
                squeezeList.Add(_diehard.SqueezeTest(streamBits));
                rankList.Add(_diehard.RanksOfMatricesTest(streamBits));
                gcdList.Add(_diehard.GcdTest(streamBits));
                crapsList.Add(_diehard.CrapsTest(streamBits));

                // TestU01
                collisionU01List.Add(_testu01.CollisionTest(streamBits));
                gapU01List.Add(_testu01.GapTest(streamBits));
                autoU01List.Add(_testu01.AutocorrelationTest(streamBits));
                spectralU01List.Add(_testu01.SpectralTest(streamBits));
                hammingU01List.Add(_testu01.HammingWeightTest(streamBits));
                serialU01List.Add(_testu01.SerialTest(streamBits));
                multinomialU01List.Add(_testu01.MultinomialTest(streamBits));
                closePairsU01List.Add(_testu01.ClosePairsTest(streamBits));
                couponU01List.Add(_testu01.CouponCollectorTest(streamBits));

                // SAC & BIC (новый вызов для каждого теста)
                var sac = _avalanche.ComputeSAC(
                    input => hasher.ComputeHash(input, p.Rounds),
                    p.InputSizeBytes,
                    trials: 1000);

                var bic = _bic.ComputeBIC(
                    input => hasher.ComputeHash(input, p.Rounds),
                    inputBitLength: p.InputSizeBytes * 8,
                    rounds: p.Rounds,
                    experimentsPerBit: 200);

                sacMeanFlipRateList.Add(sac.MeanFlipRate);
                sacStdDevFlipRateList.Add(sac.StdDevFlipRate);
                sacMinPValueList.Add(sac.MinPValue);
                sacMaxPValueList.Add(sac.MaxPValue);

                bicMeanCorrelationList.Add(bic.MeanCorrelation);
                bicStdCorrelationList.Add(bic.StdCorrelation);
                bicMaxCorrelationAbsList.Add(bic.MaxCorrelationAbs);
                bicMinCorrelationList.Add(bic.MinCorrelation);
            }

            // === Агрегация ===
            var fullStats = new
            {
                Basic = new
                {
                    result.RunDate,
                    p.Algorithm,
                    p.Rounds,
                    p.TestsCount
                },
                NIST = new
                {
                    Monobit = monobitList.Any() ? monobitList.Average() : double.NaN,
                    FrequencyWithinBlock = freqWithinList.Any() ? freqWithinList.Average() : double.NaN,
                    Runs = runsNistList.Any() ? runsNistList.Average() : double.NaN,
                    LongestRunOfOnes = longestRunList.Any() ? longestRunList.Average() : double.NaN,
                    BinaryMatrixRank = matrixRankList.Any() ? matrixRankList.Average() : double.NaN,
                    DiscreteFourier = dftList.Any() ? dftList.Average() : double.NaN,
                    NonOverlappingTemplate = nonOverlapList.Any() ? nonOverlapList.Average() : double.NaN,
                    OverlappingTemplate = overlapList.Any() ? overlapList.Average() : double.NaN,
                    MaurerUniversal = maurerList.Any() ? maurerList.Average() : double.NaN,
                    LempelZiv = lempelZivList.Any() ? lempelZivList.Average() : double.NaN,
                    LinearComplexity = linearComplexityList.Any() ? linearComplexityList.Average() : double.NaN,
                    Serial = serialList.Any() ? serialList.Average() : double.NaN,
                    ApproximateEntropy = approxEntropyList.Any() ? approxEntropyList.Average() : double.NaN,
                    Cusum = cusumList.Any() ? cusumList.Average() : double.NaN,
                    RandomExcursions = excursionsList.Any() ? excursionsList.Average() : double.NaN,
                    RandomExcursionsVariant = excursionsVarList.Any() ? excursionsVarList.Average() : double.NaN
                },
                Diehard = new
                {
                    BirthdaySpacings = birthdayList.Any() ? birthdayList.Average() : double.NaN,
                    CountOnes = countOnesList.Any() ? countOnesList.Average() : double.NaN,
                    OverlappingPermutations = overlapPermList.Any() ? overlapPermList.Average() : double.NaN,
                    RunsDiehard = runsDiehardList.Any() ? runsDiehardList.Average() : double.NaN,
                    Squeeze = squeezeList.Any() ? squeezeList.Average() : double.NaN,
                    MatrixRanks = rankList.Any() ? rankList.Average() : double.NaN,
                    GcdDiehard = gcdList.Any() ? gcdList.Average() : double.NaN,
                    CrapsDiehard = crapsList.Any() ? crapsList.Average() : double.NaN
                },
                TestU01 = new
                {
                    Collision = collisionU01List.Any() ? collisionU01List.Average() : double.NaN,
                    Gap = gapU01List.Any() ? gapU01List.Average() : double.NaN,
                    Autocorrelation = autoU01List.Any() ? autoU01List.Average() : double.NaN,
                    Spectral = spectralU01List.Any() ? spectralU01List.Average() : double.NaN,
                    HammingWeight = hammingU01List.Any() ? hammingU01List.Average() : double.NaN,
                    SerialTest = serialU01List.Any() ? serialU01List.Average() : double.NaN,
                    MultinomialTest = multinomialU01List.Any() ? multinomialU01List.Average() : double.NaN,
                    ClosePairs = closePairsU01List.Any() ? closePairsU01List.Average() : double.NaN,
                    CouponCollector = couponU01List.Any() ? couponU01List.Average() : double.NaN
                },
                Avalanche = new
                {
                    MeanFlipRate = sacMeanFlipRateList.Any() ? sacMeanFlipRateList.Average() : double.NaN,
                    StdDevFlipRate = sacStdDevFlipRateList.Any() ? sacStdDevFlipRateList.Average() : double.NaN,
                    MinPValue = sacMinPValueList.Any() ? sacMinPValueList.Min() : double.NaN,
                    MaxPValue = sacMaxPValueList.Any() ? sacMaxPValueList.Max() : double.NaN,
                    Notes = $"Среднее по количеству запусков: {p.TestsCount}"
                },
                BIC = new
                {
                    MeanCorrelation = bicMeanCorrelationList.Any() ? bicMeanCorrelationList.Average() : double.NaN,
                    StdCorrelation = bicStdCorrelationList.Any() ? bicStdCorrelationList.Average() : double.NaN,
                    MaxCorrelationAbs = bicMaxCorrelationAbsList.Any() ? bicMaxCorrelationAbsList.Max() : double.NaN,
                    MinCorrelationAbs = bicMinCorrelationList.Any() ? bicMinCorrelationList.Min() : double.NaN,
                    Notes = $"Среднее по количеству запусков: {p.TestsCount}"
                }
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Cyrillic),
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
            };
            result.BitFlipJson = JsonSerializer.Serialize(fullStats, options);

            _db.HashTestResults.Add(result);
            _db.SaveChanges();

            return View("Result", result);
        }
    }
}