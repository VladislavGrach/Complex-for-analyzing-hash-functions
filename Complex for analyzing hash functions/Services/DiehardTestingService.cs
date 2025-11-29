using Complex_for_analyzing_hash_functions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace The_complex_of_testing_hash_functions.Services
{
    public class DiehardTestingService : NistTestingService, IDiehardTestingService
    {
        #region Birthday Spacings Test
        // Тест дней рождения
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
        public double RanksOfMatricesTest(string bits)
        {
            int size = 32;
            int matrixBits = size * size;
            int numMatrices = bits.Length / matrixBits;
            if (numMatrices < 1) return -1;

            int rank32 = 0, rank31 = 0, rank30 = 0;

            for (int i = 0; i < numMatrices; i++)
            {
                int[,] matrix = new int[size, size];

                for (int row = 0; row < size; row++)
                {
                    for (int col = 0; col < size; col++)
                    {
                        matrix[row, col] = bits[i * matrixBits + row * size + col] == '1' ? 1 : 0;
                    }
                }

                int rank = GetMatrixRank(matrix);
                if (rank == 32) rank32++;
                else if (rank == 31) rank31++;
                else rank30++;
            }

            double[] expectedProbs = { 0.2888, 0.5776, 0.1336 };
            int[] observedCounts = { rank32, rank31, rank30 };
            double[] expectedCounts = expectedProbs.Select(p => p * numMatrices).ToArray();

            double chiSquare = 0;
            for (int i = 0; i < 3; i++)
            {
                chiSquare += Math.Pow(observedCounts[i] - expectedCounts[i], 2) / expectedCounts[i];
            }

            return 1.0 - ChiSquaredCDF(chiSquare, 2); // df = 3 - 1
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
        public double RunsTest(string bits)
        {
            int n = bits.Length;
            int onesCount = bits.Count(b => b == '1');
            double pi = onesCount / (double)n;

            // Если pi слишком далеко от 0.5, тест неприменим
            if (Math.Abs(pi - 0.5) > 0.01)
                return -1; // Недостаточно случайности в распределении единиц и нулей

            // Подсчитываем количество смен (run)
            int Vn = 1;
            for (int i = 1; i < n; i++)
            {
                if (bits[i] != bits[i - 1])
                    Vn++;
            }

            // Вычисляем статистику Z (приближение нормального распределения)
            double expectedRuns = 2 * n * pi * (1 - pi);
            double variance = 2 * n * pi * (1 - pi) * (2 * pi * (1 - pi) - 1) + 1;

            // Иногда можно использовать упрощённую дисперсию:
            variance = 2 * n * pi * (1 - pi);

            double z = (Vn - expectedRuns) / Math.Sqrt(variance);

            // Возвращаем p-value через стандартное нормальное распределение
            return 2 * (1 - NormalCDF(Math.Abs(z)));
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

        private int GetMatrixRank(int[,] matrix)
        {
            int rank = 0;
            int size = matrix.GetLength(0);
            bool[] rowUsed = new bool[size];

            for (int col = 0; col < size; col++)
            {
                int row = -1;
                for (int i = 0; i < size; i++)
                {
                    if (!rowUsed[i] && matrix[i, col] == 1)
                    {
                        row = i;
                        break;
                    }
                }

                if (row == -1) continue;

                rank++;
                rowUsed[row] = true;

                for (int i = 0; i < size; i++)
                {
                    if (i != row && matrix[i, col] == 1)
                    {
                        for (int j = 0; j < size; j++)
                        {
                            matrix[i, j] ^= matrix[row, j];
                        }
                    }
                }
            }

            return rank;
        }
        #endregion
    }
}
