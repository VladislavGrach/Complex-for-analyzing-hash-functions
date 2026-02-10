using System;
using System.Linq;
using Complex_for_analyzing_hash_functions.Interfaces;
using Complex_for_analyzing_hash_functions.Models;

namespace Complex_for_analyzing_hash_functions.Services
{
    public class AvalancheService : IAvalancheService
    {
        private readonly Random _rnd;

        public AvalancheService(int seed = 0)
        {
            _rnd = seed == 0 ? new Random() : new Random(seed);
        }

        public AvalancheResult ComputeSAC(
            Func<byte[], byte[]> hashFunction,
            int inputSizeBytes = 16,
            int trials = 1000,
            AvalancheMode mode = AvalancheMode.Sampled,
            int seed = 0)
        {
            if (hashFunction == null) throw new ArgumentNullException(nameof(hashFunction));
            if (inputSizeBytes <= 0) throw new ArgumentOutOfRangeException(nameof(inputSizeBytes));
            if (trials <= 0) throw new ArgumentOutOfRangeException(nameof(trials));

            Random rnd = seed == 0 ? new Random() : new Random(seed);

            // Узнаём размер хэша
            byte[] tmp = new byte[inputSizeBytes];
            rnd.NextBytes(tmp);
            byte[] tmpHash = hashFunction(tmp);
            int outputBits = tmpHash.Length * 8;

            long[] flipCounts = new long[outputBits];

            // ----------- MAIN EXPERIMENT -----------
            int totalExperiments;

            if (mode == AvalancheMode.Exhaustive)
            {
                int inputBits = inputSizeBytes * 8;
                totalExperiments = trials * inputBits;

                for (int t = 0; t < trials; t++)
                {
                    byte[] input = new byte[inputSizeBytes];
                    rnd.NextBytes(input);
                    byte[] baseHash = hashFunction(input);

                    for (int bit = 0; bit < inputBits; bit++)
                    {
                        byte[] modified = (byte[])input.Clone();
                        modified[bit / 8] ^= (byte)(1 << (bit % 8));

                        byte[] h2 = hashFunction(modified);
                        AccumulateXor(baseHash, h2, flipCounts);
                    }
                }
            }
            else
            {
                totalExperiments = trials;

                for (int t = 0; t < trials; t++)
                {
                    byte[] input = new byte[inputSizeBytes];
                    rnd.NextBytes(input);

                    byte[] baseHash = hashFunction(input);

                    int bit = rnd.Next(0, inputSizeBytes * 8);
                    byte[] modified = (byte[])input.Clone();
                    modified[bit / 8] ^= (byte)(1 << (bit % 8));

                    byte[] h2 = hashFunction(modified);

                    AccumulateXor(baseHash, h2, flipCounts);
                }
            }

            // ----------- COMPUTE METRICS -----------

            double[] flipRates = flipCounts.Select(c => (double)c / totalExperiments).ToArray();

            double mean = flipRates.Average();
            double std = Math.Sqrt(flipRates.Select(r => (r - mean) * (r - mean)).Average());
            double maxDev = flipRates.Max(r => Math.Abs(r - 0.5));

            // P-values
            double sigma = Math.Sqrt(0.25 / totalExperiments);

            double[] pvals = flipRates
                .Select(r => Math.Clamp(Erfc(Math.Abs((r - 0.5) / sigma) / Math.Sqrt(2)), 0.0, 1.0))
                .ToArray();

            double minP = pvals.Min();
            double maxP = pvals.Max();

            return new AvalancheResult
            {
                MeanFlipRate = mean,
                StdDevFlipRate = std,
                MaxDeviationFromHalf = maxDev,
                MinPValue = minP,
                MaxPValue = maxP,
                Notes = $"SAC computed: trials={trials}, outputBits={outputBits}, mode={mode}"
            };
        }

        private static void AccumulateXor(byte[] a, byte[] b, long[] flipCounts)
        {
            int n = Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++)
            {
                byte x = (byte)(a[i] ^ b[i]);
                for (int bit = 0; bit < 8; bit++)
                {
                    if (((x >> bit) & 1) == 1)
                        flipCounts[i * 8 + bit]++;
                }
            }
        }

        private static double Erfc(double x)
        {
            double z = Math.Abs(x);
            double t = 1.0 / (1.0 + 0.5 * z);
            double ans =
                t * Math.Exp(-z * z - 1.26551223 +
                t * (1.00002368 +
                t * (0.37409196 +
                t * (0.09678418 +
                t * (-0.18628806 +
                t * (0.27886807 +
                t * (-1.13520398 +
                t * (1.48851587 +
                t * (-0.82215223 +
                t * 0.17087277)))))))));
            return x >= 0 ? ans : 2.0 - ans;
        }
    }
}
