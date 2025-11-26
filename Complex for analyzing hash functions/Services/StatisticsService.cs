using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Complex_for_analyzing_hash_functions.Models;

namespace Complex_for_analyzing_hash_functions.Services
{
    public class StatisticsService
    {
        private readonly IHashFunction _hash;

        public StatisticsService(IHashFunction hashFunction)
        {
            _hash = hashFunction;
        }

        public HashTestResult RunTest(HashTestParameters p)
        {
            var rnd = new Random(12345);

            var fullRounds = 24; // baseline
            int testCount = p.TestsCount;

            var hamming = new List<int>();
            var collisions = new Dictionary<string, int>();

            // digest size
            int digestSize = _hash.ComputeHash(new byte[1], fullRounds).Length;
            long[] bitFlipCounts = new long[digestSize * 8];

            for (int t = 0; t < testCount; t++)
            {
                byte[] input = new byte[p.InputSizeBytes];
                rnd.NextBytes(input);

                byte[] full = _hash.ComputeHash(input, fullRounds);
                byte[] reduced = _hash.ComputeHash(input, p.Rounds);

                string key = BitConverter.ToString(reduced);
                if (!collisions.ContainsKey(key)) collisions[key] = 0;
                collisions[key]++;

                int hd = Hamming(full, reduced);
                hamming.Add(hd);

                for (int i = 0; i < digestSize; i++)
                {
                    byte diff = (byte)(full[i] ^ reduced[i]);
                    for (int b = 0; b < 8; b++)
                    {
                        if (((diff >> b) & 1) == 1)
                            bitFlipCounts[i * 8 + b]++;
                    }
                }
            }

            double avg = hamming.Average();
            double std = Math.Sqrt(hamming.Select(v => (v - avg) * (v - avg)).Average());
            int coll = collisions.Values.Count(v => v > 1);

            double chi2 = 0;
            for (int i = 0; i < bitFlipCounts.Length; i++)
            {
                double obs = bitFlipCounts[i];
                double exp = testCount / 2.0;
                chi2 += (obs - exp) * (obs - exp) / exp;
            }

            return new HashTestResult
            {
                RunDate = DateTime.Now,
                Algorithm = p.Algorithm,
                Rounds = p.Rounds,
                TestsCount = p.TestsCount,
                AvgHamming = avg,
                StdDevHamming = std,
                CollisionCount = coll,
                ChiSquare = chi2,
                BitFlipJson = System.Text.Json.JsonSerializer.Serialize(bitFlipCounts)
            };
        }

        private static int Hamming(byte[] a, byte[] b)
        {
            int sum = 0;
            for (int i = 0; i < a.Length; i++)
                sum += BitOperations.PopCount((byte)(a[i] ^ b[i]));
            return sum;
        }
    }
}
