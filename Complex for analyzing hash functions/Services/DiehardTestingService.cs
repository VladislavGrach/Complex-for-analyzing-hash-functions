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
        public double BirthdaySpacingsTest(string bits)
        {
            const int WORD_BITS = 24;
            const int M = 1 << WORD_BITS;
            const int m = 512; // КЛЮЧЕВО!

            if (bits.Length < m * WORD_BITS)
                return double.NaN;

            uint[] values = new uint[m];

            for (int i = 0; i < m; i++)
            {
                int pos = i * WORD_BITS;
                values[i] = Convert.ToUInt32(bits.Substring(pos, WORD_BITS), 2);
            }

            Array.Sort(values);

            // spacings
            int n = values.Length;
            uint[] spacings = new uint[n];

            for (int i = 1; i < n; i++)
                spacings[i - 1] = values[i] - values[i - 1];

            spacings[n - 1] = (uint)(M - (values[n - 1] - values[0]));

            Array.Sort(spacings);

            // считаем число совпадений spacings
            int collisions = 0;
            for (int i = 1; i < n; i++)
                if (spacings[i] == spacings[i - 1])
                    collisions++;

            // параметр Пуассона
            double lambda = (double)m * m * m / (4.0 * M);

            // p-value: P(X ≥ collisions | Pois(lambda))
            double pValue = 1.0 - PoissonCDF(collisions - 1, lambda);

            return Math.Clamp(pValue, 0.0, 1.0);
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
        public double RanksOfMatricesTest(string streamBits)
        {
            const int MATRIX_SIZE = 32;
            const int BITS_PER_MATRIX = MATRIX_SIZE * MATRIX_SIZE;
            const int MIN_MATRICES = 100;

            if (string.IsNullOrEmpty(streamBits))
                return double.NaN;

            int numMatrices = streamBits.Length / BITS_PER_MATRIX;
            if (numMatrices < MIN_MATRICES)
                return double.NaN;

            int rank32 = 0;
            int rank31 = 0;
            int rank30orLess = 0;

            int offset = 0;

            for (int m = 0; m < numMatrices; m++)
            {
                int[,] matrix = new int[MATRIX_SIZE, MATRIX_SIZE];

                for (int i = 0; i < MATRIX_SIZE; i++)
                {
                    for (int j = 0; j < MATRIX_SIZE; j++)
                    {
                        matrix[i, j] = streamBits[offset++] - '0';
                    }
                }

                int rank = GF2Rank(matrix, MATRIX_SIZE);

                if (rank == 32) rank32++;
                else if (rank == 31) rank31++;
                else rank30orLess++;
            }

            // Теоретические вероятности (NIST / Diehard)
            const double p32 = 0.2887880950866024;
            const double p31 = 0.5775761901732048;
            const double p30 = 0.13363571474019285;

            double e32 = numMatrices * p32;
            double e31 = numMatrices * p31;
            double e30 = numMatrices * p30;

            double chi2 =
                Math.Pow(rank32 - e32, 2) / e32 +
                Math.Pow(rank31 - e31, 2) / e31 +
                Math.Pow(rank30orLess - e30, 2) / e30;

            // df = 3 − 1 = 2
            double pValue = 1.0 - ChiSquaredCDF(chi2, 2);

            return Math.Clamp(pValue, 0.0, 1.0);
        }
        #endregion

        #region Overlapping Permutations Test
        // Тест на перестановки
        public double OverlappingPermutationsTest(string bits)
        {
            const int m = 5;
            int n = bits.Length;

            var seen = new HashSet<string>();
            int pos = 0;
            int uniqueCount = 0;

            while (pos <= n - m)
            {
                string window = bits.Substring(pos, m);
                if (seen.Add(window))
                    uniqueCount++;
                pos++; // перекрытие на 1 бит
            }

            int maxPossible = 1 << m; // 32

            // Приближённое ожидаемое число уникальных
            double expected = maxPossible * (1 - Math.Pow(1 - 1.0 / maxPossible, n - m + 1));
            double variance = maxPossible * Math.Exp(-(n - m + 1) / (double)maxPossible) * (1 - Math.Exp(-(n - m + 1) / (double)maxPossible));

            double z = (uniqueCount - expected) / Math.Sqrt(variance + 1e-10);
            double pValue = Erfc(Math.Abs(z) / Math.Sqrt(2.0));

            return pValue;
        }
        #endregion

        #region Runs Test
        // Тест серийности
        public double RunsTest(string bits)
        {
            const int BLOCK_SIZE = 20000;
            const int MIN_BLOCKS = 20;
            const int MIN_BITS = BLOCK_SIZE * MIN_BLOCKS;

            int n = bits.Length;
            if (n < MIN_BITS)
            {
                return double.NaN;
            }

            // Обрезаем до целого числа блоков
            int numBlocks = n / BLOCK_SIZE;
            if (numBlocks < MIN_BLOCKS)
                return double.NaN;

            long[] runCount = new long[6]; // 1..5, >=6
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

                // Последний run в блоке
                int lastIdx = Math.Min(runLen, 6) - 1;
                runCount[lastIdx]++;
                offset += BLOCK_SIZE;
            }

            long totalRuns = runCount.Sum();
            if (totalRuns == 0) return double.NaN;

            // Точные вероятности для независимых битов p=0.5
            double[] p = new double[6]
            {
                0.5,       // run=1
                0.25,      // =2
                0.125,     // =3
                0.0625,    // =4
                0.03125,   // =5
                0.03125    // >=6
            };

            double chi2 = 0.0;
            for (int i = 0; i < 6; i++)
            {
                double exp = totalRuns * p[i];
                if (exp < 5.0)
                {
                    return double.NaN;
                }
                double obs = runCount[i];
                chi2 += Math.Pow(obs - exp, 2) / exp;
            }

            int df = 5;
            double pValue = 1.0 - ChiSquaredCDF(chi2, df);

            return Math.Clamp(pValue, 0.0, 1.0);
        }
        #endregion

        #region Gcd Test
        // Diehard GCD Test (Marsaglia)
        public double GcdTest(string streamBits)
        {
            const int WORD_SIZE = 32;
            const int MIN_WORDS = 100_000;
            const double EXPECTED = 6.0 / (Math.PI * Math.PI);

            if (string.IsNullOrEmpty(streamBits))
            {
                return double.NaN;
            }

            int totalWords = streamBits.Length / WORD_SIZE;
            if (totalWords < MIN_WORDS)
            {
                return double.NaN;
            }

            long pairs = 0;
            long coprime = 0;

            uint prev = 0;
            bool havePrev = false;

            int pos = 0;
            while (pos + WORD_SIZE <= streamBits.Length)
            {
                uint current = ReadUInt32FromBits(streamBits, pos);
                pos += WORD_SIZE;

                if (!havePrev)
                {
                    prev = current;
                    havePrev = true;
                    continue;
                }

                pairs++;
                if (Gcd32(prev, current) == 1)
                    coprime++;

                prev = current;
            }

            if (pairs == 0)
            {
                return double.NaN;
            }

            double pObs = coprime / (double)pairs;

            double variance = EXPECTED * (1.0 - EXPECTED) / pairs;
            if (variance <= 0 || double.IsNaN(variance))
            {
                return 0.0;
            }

            double z = (pObs - EXPECTED) / Math.Sqrt(variance);

            double pValue = 2.0 * (1.0 - NormalCDF(Math.Abs(z)));
            return Math.Clamp(pValue, 0.0, 1.0);
        }


        private static uint ReadUInt32FromBits(string bits, int offset)
        {
            uint value = 0;
            for (int i = 0; i < 32; i++)
            {
                value = (value << 1) | (uint)(bits[offset + i] - '0');
            }
            return value;
        }

        private static uint Gcd32(uint a, uint b)
        {
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
            const int WORD_BITS = 32;
            const double THRESHOLD = 1.0 / 4294967296.0; // 2^-32
            const int TARGET_SAMPLES = 10_000;
            const int MIN_SAMPLES = 1_000;

            if (string.IsNullOrEmpty(bits) || bits.Length < WORD_BITS * 100_000)
            {
                return double.NaN;
            }

            List<int> ks = new();
            int pos = 0;
            int nBits = bits.Length;
            int maxK = 0;

            while (ks.Count < TARGET_SAMPLES && pos + WORD_BITS <= nBits)
            {
                double S = 1.0;
                int k = 0;

                while (S > THRESHOLD && pos + WORD_BITS <= nBits)
                {
                    uint word = 0;
                    for (int i = 0; i < WORD_BITS; i++)
                    {
                        if (bits[pos + i] == '1')
                            word |= (1u << (WORD_BITS - 1 - i));
                    }
                    pos += WORD_BITS;

                    // Diehard-преобразование
                    double U = (word + 1.0) / 4294967297.0; // 2^32 + 1

                    S *= U;
                    k++;

                    if (k > 10_000) break; // защита
                }

                if (S <= THRESHOLD && k > 0)
                {
                    ks.Add(k);
                    if (k > maxK) maxK = k;
                }
            }

            if (ks.Count < MIN_SAMPLES)
            {
                return double.NaN;
            }

            // --- статистика Diehard ---
            double mean = ks.Average();

            double expected = WORD_BITS * Math.Log(2.0); // 32 * ln(2)
            double variance = expected;

            double z = (mean - expected) / Math.Sqrt(variance / ks.Count);
            double pValue = Erfc(Math.Abs(z) / Math.Sqrt(2.0));

            return Math.Clamp(pValue, 0.0, 1.0);
        }

        #endregion

        #region Craps Test
        public double CrapsTest(string bits)
        {
            const int GAMES = 200_000;

            int n = bits.Length;

            int bitPos = 0;

            int ReadBits(int count)
            {
                if (bitPos + count > n)
                    return -1; // сигнал конца
                int v = 0;
                for (int i = 0; i < count; i++)
                    v = (v << 1) | (bits[bitPos++] - '0');
                return v;
            }

            int GetDie()
            {
                while (bitPos + 3 <= n)
                {
                    int v = ReadBits(3);
                    if (v == -1) return -1;
                    if (v <= 5)
                        return 1 + v; // 1..6
                                      // иначе отбрасываем (6 или 7)
                }
                return -1; // конец битов
            }

            int wins = 0;
            int gamesPlayed = 0;

            for (int game = 0; game < GAMES; game++)
            {
                int d1 = GetDie();
                int d2 = GetDie();
                if (d1 == -1 || d2 == -1) break;

                int sum = d1 + d2;
                gamesPlayed++;

                if (sum == 7 || sum == 11)
                {
                    wins++;
                    continue;
                }

                if (sum == 2 || sum == 3 || sum == 12)
                    continue; // проигрыш

                int point = sum;
                bool resolved = false;

                while (!resolved)
                {
                    d1 = GetDie();
                    d2 = GetDie();
                    if (d1 == -1 || d2 == -1) break;

                    sum = d1 + d2;
                    if (sum == 7)
                    {
                        resolved = true; // проигрыш
                    }
                    else if (sum == point)
                    {
                        wins++;
                        resolved = true;
                    }
                }
            }

            if (gamesPlayed < GAMES / 2)
            {
                return double.NaN;
            }

            const double P = 244.0 / 495.0; // ≈ 0.493939
            double expected = P * gamesPlayed;
            double variance = gamesPlayed * P * (1 - P);

            double z = (wins - expected) / Math.Sqrt(variance);
            double pValue = 2.0 * (1.0 - NormalCDF(Math.Abs(z)));

            return Math.Clamp(pValue, 0.0, 1.0);
        }
        #endregion

        #region Auxiliary calculation
        private double BinomialCoefficient(int n, int k)
        {
            if (k < 0 || k > n) return 0.0;
            if (k == 0 || k == n) return 1.0;

            k = Math.Min(k, n - k);

            double result = 1.0;
            for (int i = 1; i <= k; i++)
            {
                result *= (n - i + 1);
                result /= i;
            }

            return result;
        }

        private double BinomialProbability(int n, int k, double p)
        {
            return BinomialCoefficient(n, k) * Math.Pow(p, k) * Math.Pow(1 - p, n - k);
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

        private static double PoissonCDF(int k, double lambda)
        {
            if (k < 0)
                return 0.0;

            double sum = 0.0;
            double term = Math.Exp(-lambda); // k = 0

            sum = term;

            for (int i = 1; i <= k; i++)
            {
                term *= lambda / i;
                sum += term;
            }

            return sum;
        }
        #endregion
    }
}