using Complex_for_analyzing_hash_functions.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace The_complex_of_testing_hash_functions.Services
{
    public class DiehardTestingService : NistTestingService, IDiehardTestingService
    {
        #region Birthday Spacings Test
        //Тест дней рождения
        public double BirthdaySpacingsTest(string bits)
        {
            int m = bits.Length / 24; // Берем 24-битные числа
            if (m < 2) return -1;

            List<int> birthdays = new();

            for (int i = 0; i < m; i++)
            {
                int val = Convert.ToInt32(bits.Substring(i * 24, 24), 2);
                birthdays.Add(val);
            }

            birthdays.Sort();

            List<int> spacings = new();
            for (int i = 1; i < birthdays.Count; i++)
            {
                spacings.Add(birthdays[i] - birthdays[i - 1]);
            }

            // Количество совпадающих расстояний
            var duplicates = spacings.GroupBy(x => x)
                                     .Count(g => g.Count() > 1);

            // Ожидание по Пуассону для количества повторов:
            // E = n^3 / (4 * m), где m — размер пространства
            double n = m;
            double spaceSize = Math.Pow(2, 24);
            double expected = n * n * n / (4 * spaceSize);
            double chiSquare = Math.Pow(duplicates - expected, 2) / expected;

            int df = 1; // Степени свободы
            return 1.0 - ChiSquaredCDF(chiSquare, df);
        }
        #endregion

        #region Count Ones Test
        // Тест подсчёта единиц
        public double CountOnesTest(string bits)
        {
            int n = bits.Length / 8;
            if (n < 10) return -1;

            int[] freq = new int[9]; // от 0 до 8 единиц

            for (int i = 0; i < n; i++)
            {
                string byteStr = bits.Substring(i * 8, 8);
                int ones = byteStr.Count(c => c == '1');
                freq[ones]++;
            }

            // Биномиальное распределение: P(k) = C(8,k) * (0.5)^8
            double[] expected = new double[9];
            for (int k = 0; k <= 8; k++)
            {
                expected[k] = n * BinomialProbability(8, k, 0.5);
            }

            // χ2 = Σ (O - E)^2 / E
            double chiSquare = 0;
            for (int i = 0; i <= 8; i++)
            {
                if (expected[i] > 0)
                {
                    chiSquare += Math.Pow(freq[i] - expected[i], 2) / expected[i];
                }
            }

            int df = 8; // Степени свободы = 9 - 1
            return 1.0 - ChiSquaredCDF(chiSquare, df);
        }
        #endregion

        #region Ranks Of Matrices Test
        // Тест рангов матриц
        public double RanksOfMatricesTest(string bits, Func<byte[], byte[]> hashFunction = null)
        {
            const int matrixSize = 32;
            const int bitsPerMatrix = matrixSize * matrixSize; 
            const int MIN_MATRICES = 200;                     

            // --- STEP 1. PAD IF NEEDED ---
            int requiredBits = MIN_MATRICES * bitsPerMatrix;

            if (bits.Length < requiredBits)
            {
                if (hashFunction == null)
                    throw new ArgumentException("Недостаточно бит и не передана hashFunction для padding.");

                int missing = requiredBits - bits.Length;
                string extra = GenerateHashStream(hashFunction, missing);
                bits += extra;
            }

            int numMatrices = bits.Length / bitsPerMatrix;
            if (numMatrices < 10) return -1; // минимальный порог

            int rank32 = 0, rank31 = 0, rank30 = 0;

            int offset = 0;

            for (int m = 0; m < numMatrices; m++)
            {
                int[,] A = new int[matrixSize, matrixSize];

                for (int i = 0; i < matrixSize; i++)
                {
                    for (int j = 0; j < matrixSize; j++)
                    {
                        A[i, j] = bits[offset++] - '0';
                    }
                }

                int r = GF2Rank(A, matrixSize);

                if (r == 32) rank32++;
                else if (r == 31) rank31++;
                else rank30++;
            }

            double p32 = 0.2887880950866024;
            double p31 = 0.5775761901732048;
            double p30 = 0.13363571474019285;

            double e32 = numMatrices * p32;
            double e31 = numMatrices * p31;
            double e30 = numMatrices * p30;

            double chi2 =
                (Math.Pow(rank32 - e32, 2) / e32) +
                (Math.Pow(rank31 - e31, 2) / e31) +
                (Math.Pow(rank30 - e30, 2) / e30);

            // df = 3 - 1 = 2 degrees of freedom
            double pValue = 1.0 - ChiSquaredCDF(chi2, 2);

            return Math.Max(0.0, Math.Min(1.0, pValue));
        }
        #endregion

        #region Overlapping Permutations Test
        // Тест на перестановки
        public double OverlappingPermutationsTest(string bits)
        {
            if (bits.Length < 3) return -1;

            Dictionary<string, int> frequencies = new();
            for (int i = 0; i <= bits.Length - 3; i++) // Перекрывающиеся тройки
            {
                string triple = bits.Substring(i, 3);
                if (!frequencies.ContainsKey(triple))
                    frequencies[triple] = 0;
                frequencies[triple]++;
            }

            int total = bits.Length - 2;
            double expected = (double)total / 8;

            double chiSquare = frequencies.Values.Sum(f => Math.Pow(f - expected, 2) / expected);

            // df = 7, т.к. 8 троек - 1
            return ChiSquaredCDF(chiSquare, 7);
        }
        #endregion

        #region Runs Test
        // Тест серийности
        public double RunsTest(string bits, Func<byte[], byte[]> hashFunction = null)
        {
            const int BLOCK_SIZE = 20000;      // Размер блока Diehard
            const int MIN_BLOCKS = 20;         // Минимум 20 блоков для устойчивой статистики
            const int MIN_BITS = BLOCK_SIZE * MIN_BLOCKS;

            // Если мало бит → добиваем криптографическим padding
            if (bits.Length < MIN_BITS)
            {
                if (hashFunction == null)
                    return -1;

                int missing = MIN_BITS - bits.Length;
                bits += GenerateHashStream(hashFunction, missing);
            }

            int totalBits = bits.Length;
            int numBlocks = totalBits / BLOCK_SIZE;

            if (numBlocks < 1)
                return -1;

            // Счётчики серий: длина 1..6 (6 = категория "6 и больше")
            long[] runCount = new long[6];

            int offset = 0;

            for (int b = 0; b < numBlocks; b++)
            {
                char prev = bits[offset];
                int runLen = 1;

                for (int i = 1; i < BLOCK_SIZE; i++)
                {
                    char cur = bits[offset + i];
                    if (cur == prev)
                    {
                        runLen++;
                    }
                    else
                    {
                        int idx = Math.Min(runLen, 6) - 1;
                        runCount[idx]++;

                        runLen = 1;
                        prev = cur;
                    }
                }

                // последний run в блоке
                int lastIdx = Math.Min(runLen, 6) - 1;
                runCount[lastIdx]++;

                offset += BLOCK_SIZE;
            }

            long totalRuns = runCount.Sum();

            // Ожидаемые вероятности (из оригинального Diehard)
            double[] p = {
                0.5,        // длина 1
                0.25,       // длина 2
                0.125,      // 3
                0.0625,     // 4
                0.03125,    // 5
                0.03125     // >= 6
            };

            // χ²
            double chi2 = 0.0;

            for (int i = 0; i < 6; i++)
            {
                double expected = totalRuns * p[i];
                double observed = runCount[i];

                if (expected > 0)
                    chi2 += (observed - expected) * (observed - expected) / expected;
            }

            // Степени свободы: 6 категорий – 1 = 5
            double pValue = 1.0 - ChiSquaredCDF(chi2, 5);

            return Math.Max(0.0, Math.Min(1.0, pValue));
        }

        #endregion

        #region Gcd Test
        public double GcdTest(string bits, Func<byte[], byte[]>? hashFunction = null, int requiredWordsDefault = 100_000)
        {
            const double EXPECTED_COPRIME = 6.0 / (Math.PI * Math.PI); // ≈0.607927

            // если передали hashFunction и вход короткий — дополняем
            if ((bits?.Length ?? 0) < 32 * requiredWordsDefault)
            {
                if (hashFunction == null)
                    return -1.0; // недостаточно данных и нет способа pad'а

                bits = GenerateHashStream(hashFunction, requiredWordsDefault * 32);
            }

            if (string.IsNullOrEmpty(bits) || bits.Length < 64)
                return -1.0;

            long pairs = 0;
            long coprimeCount = 0;

            // Проходим по словам подряд (в потоковом режиме, без лишнего аллока)
            int pos = 0;
            uint prev = 0;
            bool havePrev = false;

            while (pos + 32 <= bits.Length)
            {
                uint w = Convert.ToUInt32(bits.Substring(pos, 32), 2);
                pos += 32;

                if (!havePrev)
                {
                    prev = w;
                    havePrev = true;
                    continue;
                }

                pairs++;
                if (Gcd32(prev, w) == 1U) coprimeCount++;

                prev = w;
            }

            if (pairs == 0) return -1.0;

            double pObs = coprimeCount / (double)pairs;
            double expected = EXPECTED_COPRIME;

            // дисперсия для биномиального приближения
            double var = expected * (1 - expected) / (double)pairs;
            if (var <= 0 || double.IsNaN(var)) return 0.0;

            double z = (pObs - expected) / Math.Sqrt(var);
            double pValue = 2.0 * (1.0 - NormalCDF(Math.Abs(z)));

            // защита от числовых артефактов
            if (double.IsNaN(pValue)) pValue = 0.0;
            return Math.Max(0.0, Math.Min(1.0, pValue));
        }

        private static uint Gcd32(uint a, uint b)
        {
            // быстрый евклид для unsigned
            if (a == 0) return b;
            if (b == 0) return a;
            while (b != 0)
            {
                uint t = a % b;
                a = b;
                b = t;
            }
            return a;
        }

        #endregion

        #region Squeeze Test
        public double SqueezeTest(string bits)
        {
            const int N = 100_000;
            const int K = 23;
            const int MIN_BITS_REQUIRED = N * 6;

            var values = new List<int>(N);
            int bitPos = 0;

            while (values.Count < N && bitPos + 6 <= bits.Length)
            {
                string chunk = bits.Substring(bitPos, 6);
                int num = Convert.ToInt32(chunk, 2);
                if (num < K)
                    values.Add(num);
                bitPos += 6;
            }

            int actualN = values.Count;
            int squeezeCount = 0;
            long current = 1;

            foreach (int v in values)
            {
                if (v == 0)
                    current = 1;
                else
                {
                    current *= v;
                    if (current >= 8_388_608L)
                    {
                        squeezeCount++;
                        current = 1;
                    }
                }
            }

            double expected = (double)actualN / K;
            double variance = expected * (1.0 + 1.0 / K);
            double z = (squeezeCount - expected) / Math.Sqrt(variance);
            double pValue = 2.0 * (1.0 - NormalCDF(Math.Abs(z)));

            return Math.Max(0.0, Math.Min(1.0, pValue));
        }
        #endregion

        #region Craps Test
        public double CrapsTest(string bits)
        {
            const int GAMES = 200_000;
            const int MIN_BITS_REQUIRED = 10_000_000; // большой запас

            if (bits.Length < MIN_BITS_REQUIRED)
            {
                var sb = new StringBuilder(MIN_BITS_REQUIRED);
                while (sb.Length < MIN_BITS_REQUIRED)
                    sb.Append(bits);
                bits = sb.ToString(0, MIN_BITS_REQUIRED);
            }

            int bitPos = 0;

            int ReadBits(int count)
            {
                if (bitPos + count > bits.Length) return 0;
                int v = 0;
                for (int i = 0; i < count; i++)
                    v = (v << 1) | (bits[bitPos++] - '0');
                return v;
            }

            int GetDie()
            {
                while (bitPos + 3 <= bits.Length)
                {
                    int v = ReadBits(3);
                    if (v <= 5)
                        return 1 + v;    // 1..6
                                         // иначе — отбрасываем, но bitPos уже сдвинут — это нормально
                }
                return 4; // если кончились биты — нейтральное значение
            }

            int wins = 0;

            for (int game = 0; game < GAMES; game++)
            {
                int d1 = GetDie();
                int d2 = GetDie();
                int sum = d1 + d2;

                if (sum == 7 || sum == 11)
                {
                    wins++;
                    continue;
                }
                if (sum == 2 || sum == 3 || sum == 12)
                {
                    continue; // проигрыш
                }

                int point = sum;
                while (bitPos + 6 <= bits.Length)
                {
                    sum = GetDie() + GetDie();
                    if (sum == 7) break;
                    if (sum == point)
                    {
                        wins++;
                        break;
                    }
                }
            }

            const double P = 244.0 / 495.0;
            double expected = P * GAMES;
            double variance = GAMES * P * (1 - P);
            double z = (wins - expected) / Math.Sqrt(variance);
            double pValue = 2 * (1 - NormalCDF(Math.Abs(z)));

            return pValue;
        }

        public double CrapsTestOnHashStream(
            Func<byte[], byte[]> hashFunction,
            int requiredBits = 1_500_000)
        {
            string bits = GenerateHashStream(hashFunction, requiredBits);
            return CrapsTest(bits);
        }
        #endregion

        #region Auxiliary calculation
        private double BinomialProbability(int n, int k, double p)
        {
            return BinomialCoefficient(n, k) * Math.Pow(p, k) * Math.Pow(1 - p, n - k);
        }

        private double BinomialCoefficient(int n, int k)
        {
            if (k < 0 || k > n) return 0;
            if (k == 0 || k == n) return 1;

            double result = 1;
            for (int i = 1; i <= k; i++)
            {
                result *= (n - (k - i));
                result /= i;
            }
            return result;
        }

        private int GF2Rank(int[,] A, int N)
        {
            int rank = 0;
            int row = 0;

            for (int col = 0; col < N; col++)
            {
                int pivot = -1;

                for (int r = row; r < N; r++)
                {
                    if (A[r, col] == 1)
                    {
                        pivot = r;
                        break;
                    }
                }

                if (pivot == -1)
                    continue;

                // Swap pivot row ↔ current row
                if (pivot != row)
                {
                    for (int c = 0; c < N; c++)
                    {
                        int tmp = A[pivot, c];
                        A[pivot, c] = A[row, c];
                        A[row, c] = tmp;
                    }
                }

                // Eliminate in all other rows
                for (int r = 0; r < N; r++)
                {
                    if (r != row && A[r, col] == 1)
                    {
                        for (int c = 0; c < N; c++)
                        {
                            A[r, c] ^= A[row, c];
                        }
                    }
                }

                row++;
                rank++;

                if (row == N) break;
            }

            return rank;
        }
        #endregion
    }
}