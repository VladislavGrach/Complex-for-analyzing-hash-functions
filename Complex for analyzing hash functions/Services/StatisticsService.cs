using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Complex_for_analyzing_hash_functions.Interfaces;
using Complex_for_analyzing_hash_functions.Models;

namespace Complex_for_analyzing_hash_functions.Services
{
    public class StatisticsService
    {
        private readonly IHashFunction? _hash; // может быть null, тогда используем ResolveHasher

        // Пустой конструктор: для DI-совместимости
        public StatisticsService()
        {
            _hash = null;
        }

        // Конструктор с конкретным хэшем
        public StatisticsService(IHashFunction hashFunction)
        {
            _hash = hashFunction ?? throw new ArgumentNullException(nameof(hashFunction));
        }

        // Разрешатель по имени алгоритма (на случай, если _hash == null)
        private IHashFunction ResolveHasher(string algorithm)
        {
            return algorithm switch
            {
                "Keccak" => new KeccakHash(256),
                "Blake" => new Blake256Hash(),
                "Blake2s" => new Blake2sHash(),
                "Blake2b" => new Blake2bHash(),
                "Blake3" => new Blake3Hash(),
                _ => throw new Exception($"Unknown hash algorithm '{algorithm}'")
            };
        }

        // Если у нас есть уже установленный _hash, используем его.
        // Иначе создаём по имени алгоритма.
        public byte[] Hash(string algorithm, byte[] input, int rounds)
        {
            var hasher = _hash ?? ResolveHasher(algorithm);
            return hasher.ComputeHash(input, rounds);
        }

        // Удобная перегрузка: если StatisticsService был создан с конкретным hasher
        public byte[] Hash(byte[] input, int rounds)
        {
            if (_hash == null) throw new InvalidOperationException("No hash function available. Use Hash(string algorithm, ... ) or construct StatisticsService with a hasher.");
            return _hash.ComputeHash(input, rounds);
        }

        public HashTestResult RunTest(HashTestParameters p)
        {
            // если _hash задан в конструкторе, используем его; иначе разрешаем по имени p.Algorithm
            var hasher = _hash ?? ResolveHasher(p.Algorithm);

            var rnd = new Random(12345);
            int maxRounds = p.Algorithm switch
            {
                "Blake" => 14,
                "Blake2s" => 10,
                "Blake2b" => 10,
                "Blake3" => 10,
                "Keccak" => 12,
                _ => 24
            };
            int fullRounds = maxRounds;           // baseline
            
            int testCount = p.TestsCount;

            var hamming = new List<int>();
            var collisions = new Dictionary<string, int>();

            int digestSize = hasher.ComputeHash(new byte[1], fullRounds).Length;
            long[] bitFlipCounts = new long[digestSize * 8];

            for (int t = 0; t < testCount; t++)
            {
                byte[] input = new byte[p.InputSizeBytes];
                rnd.NextBytes(input);

                byte[] full = hasher.ComputeHash(input, fullRounds);
                byte[] reduced = hasher.ComputeHash(input, p.Rounds);

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
                BitFlipJson = System.Text.Json.JsonSerializer.Serialize(bitFlipCounts)
            };
        }

        private static int Hamming(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) throw new ArgumentException("Arrays must have same length");
            int sum = 0;
            for (int i = 0; i < a.Length; i++)
                sum += BitOperations.PopCount((byte)(a[i] ^ b[i]));
            return sum;
        }
    }
}
