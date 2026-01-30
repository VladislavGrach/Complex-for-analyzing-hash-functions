using Complex_for_analyzing_hash_functions.Interfaces;
using System.Numerics;
using System.Text;


namespace The_complex_of_testing_hash_functions.Services
{
    public class NistTestingService : INistTestingService
    {
        #region Monobit Test
        // Монобит-тест
        public double MonobitTest(string bits)
        {
            int n = bits.Length;
            int sum = bits.Sum(bit => bit == '1' ? 1 : -1);
            double sObs = Math.Abs(sum) / Math.Sqrt(n);
            double pValue = Erfc(sObs / Math.Sqrt(2.0));
            return pValue;
        }

        //// Комплементарная функция ошибок (erfc)
        private double Erfc(double x)
        {
            if (double.IsNaN(x)) return double.NaN;

            double z = Math.Abs(x);

            if (z > 26.0)
                return x < 0 ? 2.0 : 0.0;

            double t = 1.0 / (1.0 + 0.5 * z);

            double ans = t * Math.Exp(
                -z * z - 1.26551223 +
                t * (1.00002368 +
                t * (0.37409196 +
                t * (0.09678418 +
                t * (-0.18628806 +
                t * (0.27886807 +
                t * (-1.13520398 +
                t * (1.48851587 +
                t * (-0.82215223 +
                t * 0.17087277)))))))));

            double result = x >= 0 ? ans : 2.0 - ans;

            if (result < 0.0) return 0.0;
            if (result > 1.0) return 1.0;

            return result;
        }
        #endregion

        #region Frequency Test Within Block
        // Частотный тест в блоках
        public double FrequencyTestWithinBlock(string bits, int blockSize = 128)
        {
            int n = bits.Length;
            int numBlocks = n / blockSize;
            if (numBlocks == 0) return double.NaN;

            double chiSquare = 0;
            for (int i = 0; i < numBlocks; i++)
            {
                string block = bits.Substring(i * blockSize, blockSize);
                int onesCount = block.Count(c => c == '1');
                double pi = (double)onesCount / blockSize;
                chiSquare += 4.0 * blockSize * Math.Pow(pi - 0.5, 2);
            }

            double pValue = ChiSquaredCDF(chiSquare, numBlocks);
            return 1.0 - pValue; // так как мы хотим верхнюю хвостовую вероятность
        }
        #endregion

        #region Runs Test
        // Тест на серийность
        public double RunsTest(string bits)
        {
            int n = bits.Length;
            int ones = bits.Count(c => c == '1');
            double pi = (double)ones / n;

            if (Math.Abs(pi - 0.5) >= (2.0 / Math.Sqrt(n)))
            {
                return 0.0; // последовательность недостаточно сбалансирована — тест неприменим
            }

            int runs = 1;
            for (int i = 1; i < n; i++)
            {
                if (bits[i] != bits[i - 1])
                    runs++;
            }

            double expectedRuns = 2 * n * pi * (1 - pi);
            double variance = 2 * n * pi * (1 - pi) * (2 * n * pi * (1 - pi) - 1) / (n - 1);
            double z = Math.Abs(runs - expectedRuns) / Math.Sqrt(variance);

            return Erfc(z / Math.Sqrt(2.0));
        }
        #endregion

        #region Longest Run Of Ones Test
        // Тест на самую длинную последовательность единиц в блоке
        public double LongestRunOfOnesTest(string bits, int blockSize = 128)
        {
            int n = bits.Length;
            int numBlocks = n / blockSize;
            if (numBlocks == 0) return double.NaN;

            int[] frequencies = new int[4]; // Категории по длине серии
            for (int i = 0; i < numBlocks; i++)
            {
                string block = bits.Substring(i * blockSize, blockSize);
                int maxRun = 0, currentRun = 0;

                foreach (char bit in block)
                {
                    if (bit == '1')
                    {
                        currentRun++;
                        maxRun = Math.Max(maxRun, currentRun);
                    }
                    else
                    {
                        currentRun = 0;
                    }
                }

                // Категории согласно NIST (для blockSize = 128)
                if (maxRun <= 4) frequencies[0]++;
                else if (maxRun == 5) frequencies[1]++;
                else if (maxRun == 6) frequencies[2]++;
                else frequencies[3]++;
            }

            // Ожидаемые вероятности
            double[] pi = { 0.1174, 0.2430, 0.2493, 0.3903 };
            double chiSquared = 0;

            for (int i = 0; i < pi.Length; i++)
            {
                double expected = numBlocks * pi[i];
                double diff = frequencies[i] - expected;
                chiSquared += (diff * diff) / expected;
            }

            return 1.0 - ChiSquaredCDF(chiSquared, pi.Length - 1); // k = 3
        }
        #endregion

        #region Binary Matrix Rank Test
        // Тест ранга бинарной матрицы
        public double BinaryMatrixRankTest(string bits)
        {
            int M = 32, Q = 32;
            int matrixSize = M * Q;

            int MIN_MATRICES = 1;

            int availableBits = bits.Length;
            int neededBits = MIN_MATRICES * matrixSize;

            // Если не хватает — дополняем случайными битами
            if (availableBits < neededBits)
            {
                int missing = neededBits - availableBits;
                var rnd = new Random();

                StringBuilder pad = new StringBuilder(bits, availableBits + missing);
                for (int i = 0; i < missing; i++)
                    pad.Append(rnd.Next(2) == 1 ? '1' : '0');

                bits = pad.ToString();
                availableBits = bits.Length;
            }

            long N = availableBits / matrixSize;
            if (N == 0) N = 1; // защита, хотя ситуация уже исключена паддингом

            int fullRank = 0, rankMinusOne = 0, below = 0;

            for (int i = 0; i < N; i++)
            {
                int[,] matrix = new int[M, Q];
                int index = i * matrixSize;

                for (int row = 0; row < M; row++)
                {
                    for (int col = 0; col < Q; col++)
                    {
                        matrix[row, col] = bits[index++] == '1' ? 1 : 0;
                    }
                }

                int rank = ComputeRank(matrix);
                if (rank == M) fullRank++;
                else if (rank == M - 1) rankMinusOne++;
                else below++;
            }

            double[] expectedProbabilities = { 0.2888, 0.5776, 0.1336 };
            int[] observedCounts = { fullRank, rankMinusOne, below };

            double chiSquared = 0.0;
            for (int i = 0; i < 3; i++)
            {
                double expected = expectedProbabilities[i] * N;
                chiSquared += Math.Pow(observedCounts[i] - expected, 2) / expected;
            }

            double pValue = 1.0 - ChiSquaredCDF(chiSquared, 2);

            return double.IsFinite(pValue) ? pValue : 0.0;
        }
        #endregion

        #region Discrete Fourier Transform Test
        // Дискретное преобразование Фурье
        public double DiscreteFourierTransformTest(string bits)
        {
            int n = bits.Length;
            // NIST рекомендует достаточно большую длину; минимально возьмём 100 как у вас
            if (n < 100) return -1;

            // 1) Преобразуем 0 -> -1, 1 -> +1
            double[] sequence = bits.Select(b => b == '1' ? 1.0 : -1.0).ToArray();

            // 2) Вычисляем ДПФ (наивно O(n^2)). При больших n рекомендуется заменить на FFT.
            Complex[] spectrum = new Complex[n];
            for (int k = 0; k < n; k++)
            {
                double real = 0.0;
                double imag = 0.0;
                double twoPiOverN = 2.0 * Math.PI * k / n;
                for (int t = 0; t < n; t++)
                {
                    double angle = twoPiOverN * t;
                    double s = sequence[t];
                    real += s * Math.Cos(angle);
                    imag -= s * Math.Sin(angle);
                }
                spectrum[k] = new Complex(real, imag);
            }

            // 3) Амплитуды (модули)
            double[] magnitudes = spectrum.Select(c => c.Magnitude).ToArray();

            // 4) Порог T по NIST (для уровня значимости 0.05)
            double T = Math.Sqrt(Math.Log(1.0 / 0.05) * n);

            // 5) Рассчитываем число пиков, *меньших* порога, в первой половине спектра
            // NIST рассматривает первые n/2 значений спектра (обычно используют k = 0..n/2-1)
            int m = n / 2;
            int countLess = magnitudes.Take(m).Count(val => val < T);

            // 6) Ожидаемое число таких пиков и дисперсия согласно NIST
            double expected = 0.95 * n / 2.0;
            double variance = n * 0.95 * 0.05 / 4.0;

            // 7) Вычисляем статистику и p-value
            double d = (countLess - expected) / Math.Sqrt(variance);

            // erfc(x) — комплементарная ошибка; pValue = erfc(|d| / sqrt(2))
            double pValue = Erfc(Math.Abs(d) / Math.Sqrt(2.0));

            if (!double.IsFinite(pValue)) return 0.0;
            return pValue;
        }
        #endregion

        #region Non Overlapping Template Matching Test
        //  Тест на несовпадающие шаблоны
        public double NonOverlappingTemplateMatchingTest(string bits, string template = "000111")
        {
            int m = template.Length;
            int n = bits.Length;

            // NIST рекомендует размер блока M = 1000, количество блоков K >= 5
            int M = 1000;
            int K = n / M;

            if (K < 5)
            {
                // Делаем padding по NIST — дополняем до 5 блоков
                int needed = M * 5 - n;
                var rnd = new Random();
                var pad = new StringBuilder(bits);
                for (int i = 0; i < needed; i++)
                    pad.Append(rnd.Next(2) == 1 ? '1' : '0');
                bits = pad.ToString();
                n = bits.Length;
                K = n / M;
            }

            double[] W = new double[K];

            // Выполняем поиск шаблона в каждом блоке (неперекрывающаяся сверка)
            for (int i = 0; i < K; i++)
            {
                string block = bits.Substring(i * M, M);
                int count = 0;

                for (int j = 0; j <= M - m;)
                {
                    if (block.Substring(j, m) == template)
                    {
                        count++;
                        j += m;   // non-overlapping
                    }
                    else j++;
                }

                W[i] = count;
            }

            // λ и дисперсия
            double lambda = (M - m + 1) / Math.Pow(2, m);
            double variance = lambda * (1 - (1.0 / Math.Pow(2, m)));

            if (variance <= 0) return 0;

            // χ2 статистика
            double chi2 = 0.0;
            for (int i = 0; i < K; i++)
                chi2 += Math.Pow(W[i] - lambda, 2) / variance;

            // p-value через комплементарную гамма-функцию:
            // p = Q(K/2, χ2/2) = igamc(K/2, χ2/2)
            double pValue = Igamc(K / 2.0, chi2 / 2.0);

            return double.IsFinite(pValue) ? pValue : 0.0;
        }

        #endregion

        #region Overlapping Template Matching Test
        // Тест на совпадающие шаблоны
        public double OverlappingTemplateMatchingTest(string bits, int m = 10)
        {
            if (m <= 0 || m > bits.Length) return -1;

            int pattern = Convert.ToInt32(new string('1', m), 2);
            int count = 0;

            for (int i = 0; i <= bits.Length - m; i++)
            {
                int subPattern = Convert.ToInt32(bits.Substring(i, m), 2);
                if (subPattern == pattern) count++;
            }

            // Примерная оценка p-value
            double lambda = (bits.Length - m + 1) / Math.Pow(2, m);
            double pValue = Math.Exp(-lambda) * Math.Pow(lambda, count) / Factorial(count);
            return pValue;
        }
        #endregion

        #region Maurers Universal Test
        // Универсальный тест Маурера
        private const int MAURER_MIN_BITS = 1_500_000;

        public double MaurersUniversalTestOnHashStream(
            Func<byte[], byte[]> hashFunction,
            int requiredBits = MAURER_MIN_BITS)
        {
            if (hashFunction == null)
                throw new ArgumentNullException(nameof(hashFunction));

            if (requiredBits < MAURER_MIN_BITS)
                requiredBits = MAURER_MIN_BITS;

            string bits = GenerateHashStream(hashFunction, requiredBits);

            return MaurersUniversalTest(bits);
        }

        public double MaurersUniversalTest(string bits)
        {
            int n = bits.Length;

            int L;
            if (n >= 3878400) L = 15;
            else if (n >= 1715000) L = 14;
            else if (n >= 904960) L = 13;
            else if (n >= 462400) L = 12;
            else if (n >= 206848) L = 11;
            else if (n >= 90400) L = 10;
            else return 0.0;   // длина слишком мала

            int Q = 10 * (1 << L);         // TRAINING
            int K = (n / L) - Q;           // TEST BLOCKS

            if (K < 1000)
                return 0.0;

            int Tsize = 1 << L;
            int[] table = new int[Tsize];

            int idx = 0;

            for (int i = 0; i < Q; i++)
            {
                int pattern = Convert.ToInt32(bits.Substring(idx, L), 2);
                table[pattern] = i + 1;
                idx += L;
            }

            // ==========================
            // TEST
            // ==========================
            double sum = 0.0;

            for (int i = Q; i < Q + K; i++)
            {
                int pattern = Convert.ToInt32(bits.Substring(idx, L), 2);

                int distance = (i + 1) - table[pattern];

                if (distance <= 0)
                    return 0.0;

                sum += Math.Log(distance, 2);

                table[pattern] = i + 1;
                idx += L;
            }

            double fn = sum / K;

            double[] expected = {
                0,0,0,0,0,0,
                5.2177052, 6.1962507, 7.1836656, 8.1764248,
                9.1723243,10.170032,11.168765,12.168070,
               13.167693,14.167488
            };

            double[] variance = {
                0,0,0,0,0,0,
                2.954, 3.125, 3.238, 3.311,
                3.356, 3.384, 3.401, 3.410,
                3.416, 3.419
            };

            double mu = expected[L];
            double var = variance[L];

            double z = (fn - mu) / Math.Sqrt(var);

            // p-value = erfc(|z|/√2)
            double pValue = Erfc(Math.Abs(z) / Math.Sqrt(2));

            return pValue;
        }

        #endregion

        #region Lempel Ziv Compression Test
        // Тест Лемпеля-Зива
        // 1) Обёртка: генерируем поток хэшей (правильный padding) и запускаем тест
        private const int LZ_MIN_BITS_REQUIRED = 1_500_000;

        public double LempelZivCompressionTestOnHashStream(
            Func<byte[], byte[]> hashFunction,
            int requiredBits = LZ_MIN_BITS_REQUIRED)
        {
            string bits = GenerateHashStream(hashFunction, requiredBits);
            return LempelZivCompressionTest(bits);
        }

        // 2) Основной тест Lempel-Ziv: делает парсинг по всей строке
        public double LempelZivCompressionTest(string bits)
        {
            int n = bits.Length;

            if (n < LZ_MIN_BITS_REQUIRED)
                return 0.0;

            // --- 1. Считаем число фраз по точному LZ76 ---
            int c = CountLZ76Phrases(bits);

            // --- 2. Статистика по NIST SP 800-22 ---
            double log2n = Math.Log(n, 2);
            double mean = n / log2n;
            double variance = 0.266 * n / Math.Pow(log2n, 3);

            double z = (c - mean) / Math.Sqrt(variance);
            double pValue = Erfc(Math.Abs(z) / Math.Sqrt(2));

            return Math.Clamp(pValue, 0.0, 1.0);
        }

        private int CountLZ76Phrases(string bits)
        {
            int n = bits.Length;
            int c = 0;
            int i = 0;

            while (i < n)
            {
                int maxLen = 0;

                // ищем самую длинную подстроку, которая уже встречалась
                for (int j = 0; j < i; j++)
                {
                    int k = 0;
                    while (i + k < n && bits[j + k] == bits[i + k])
                    {
                        k++;
                    }

                    if (k > maxLen)
                        maxLen = k;
                }

                // новая фраза
                c++;
                i += maxLen + 1;
            }

            return c;
        }
        #endregion

        #region Linear Complexity Test
        // Тест линейной сложности
        public double LinearComplexityTest(string bits, int M = 32) // Уменьшенный блок
        {
            if (bits.Length < M * 4) return -1;

            int N = bits.Length / M;
            double[] pi = { 0.2, 0.3, 0.5 }; // Упрощенные веса
            int[] v = new int[3];

            for (int i = 0; i < N; i++)
            {
                string block = bits.Substring(i * M, M);
                int L = BerlekampMassey(block); // Упрощенная реализация

                if (L < M * 0.4) v[0]++;
                else if (L < M * 0.6) v[1]++;
                else v[2]++;
            }

            double chiSquared = 0;
            for (int i = 0; i < 3; i++)
                chiSquared += Math.Pow(v[i] - N * pi[i], 2) / (N * pi[i]);

            return ChiSquaredCDF(chiSquared, 2);
        }
        #endregion

        #region Serial Test
        // Серийный тест
        public double SerialTest(string bits, int m = 2)
        {
            // Проверка минимальной длины
            int n = bits.Length;
            if (n < 10 * Math.Pow(2, m)) return -1; // Минимум 10*2^m бит

            Dictionary<string, int> freq = new();

            // Подсчёт частот (без перекрытия для независимости)
            for (int i = 0; i <= n - m; i += m)
            {
                string pattern = bits.Substring(i, m);
                freq[pattern] = freq.GetValueOrDefault(pattern, 0) + 1;
            }

            // Расчёт χ2
            double expected = (double)(n / m) / Math.Pow(2, m);
            double chiSquare = 0;
            foreach (var count in freq.Values)
            {
                chiSquare += Math.Pow(count - expected, 2) / expected;
            }

            // Преобразование χ2 в p-value
            return ChiSquaredCDF(chiSquare, (int)Math.Pow(2, m) - 1);
        }
        #endregion

        #region Approximate Entropy Test
        // Тест приближенной энтропии
        public double ApproximateEntropyTest(string bits, int m = 2)
        {
            int n = bits.Length;
            if (m < 1 || n < m + 1) return -1;

            double phi_m = Phi(bits, m);
            double phi_m1 = Phi(bits, m + 1);

            double apEn = phi_m - phi_m1;
            double chiSquared = 2.0 * n * (Math.Log(2) - apEn);
            int degreesOfFreedom = (1 << (m - 1));

            return ChiSquaredCDF(chiSquared, degreesOfFreedom);
        }
        #endregion

        #region Cusum Test
        // Тест накопленных сумм
        public double CusumTest(string bits)
        {
            int n = bits.Length;
            if (n < 1) return -1;

            // Преобразуем в последовательность +1/-1
            int[] x = bits.Select(b => b == '1' ? 1 : -1).ToArray();

            // Кумулятивная сумма
            int[] S = new int[n];
            S[0] = x[0];
            for (int i = 1; i < n; i++) S[i] = S[i - 1] + x[i];

            // Максимальное отклонение от нуля
            int z = S.Select(Math.Abs).Max();

            // Вычисление p-value (двусторонний тест)
            double sum = 0.0;
            for (int k = ((-n / z + 1) / 4); k <= ((n / z - 1) / 4); k++)
            {
                double term1 = NormalCDF((4 * k + 1) * z / Math.Sqrt(n));
                double term2 = NormalCDF((4 * k - 1) * z / Math.Sqrt(n));
                sum += term1 - term2;
            }

            for (int k = ((-n / z - 3) / 4); k <= ((n / z - 1) / 4); k++)
            {
                double term1 = NormalCDF((4 * k + 3) * z / Math.Sqrt(n));
                double term2 = NormalCDF((4 * k + 1) * z / Math.Sqrt(n));
                sum -= term1 - term2;
            }

            double p = 1.0 - sum;
            return p;
        }

        #endregion

        #region Random Excursions Test
        // Тест случайных экскурсий
        public double RandomExcursionsTest(string bits)
        {
            int n = bits.Length;
            if (n < 1_000_000)
                return 0.0;

            int[] x = new int[n];
            for (int i = 0; i < n; i++)
                x[i] = bits[i] == '1' ? 1 : -1;

            int[] S = new int[n + 1];
            for (int i = 0; i < n; i++)
                S[i + 1] = S[i] + x[i];

            List<int> cycles = new List<int>();
            cycles.Add(0);
            for (int i = 1; i <= n; i++)
                if (S[i] == 0)
                    cycles.Add(i);

            int J = cycles.Count - 1;
            if (J < 100)
                return 0.0;

            int[] states = { -4, -3, -2, -1, 1, 2, 3, 4 };

            double pValueProduct = 1.0;

            // Количество посещений
            foreach (int s in states)
            {
                int K = 6; // number of bins
                int[] frequencies = new int[K];

                for (int j = 0; j < J; j++)
                {
                    int start = cycles[j];
                    int end = cycles[j + 1];

                    int v = 0;

                    for (int i = start + 1; i <= end; i++)
                        if (S[i] == s)
                            v++;

                    int idx = v >= 5 ? 5 : v;
                    frequencies[idx]++;
                }

                double[] P = GetNistExcursionProbabilities(s);

                double chi2 = 0.0;
                for (int k = 0; k < 6; k++)
                {
                    double expected = J * P[k];
                    chi2 += Math.Pow(frequencies[k] - expected, 2.0) / expected;
                }

                double pValue = Igamc(2.5, chi2 / 2.0); // Complemented gamma (df=5)
                pValueProduct *= pValue;
            }

            return pValueProduct;
        }

        private double[] GetNistExcursionProbabilities(int s)
        {
            // NIST SP 800-22 Table 4
            switch (Math.Abs(s))
            {
                case 1: return new[] { 0.5, 0.25, 0.125, 0.0625, 0.03125, 0.03125 };
                case 2: return new[] { 0.75, 0.0625, 0.046875, 0.03515625, 0.0263672, 0.0791016 };
                case 3: return new[] { 0.8333333, 0.0277778, 0.0185185, 0.0123457, 0.00823045, 0.099794 };
                case 4: return new[] { 0.875, 0.015625, 0.00976562, 0.00610352, 0.0038147, 0.0890503 };
                default: throw new ArgumentException("state must be 1..4");
            }
        }

        #endregion

        #region Random Excursions Variant Test
        // Тест вариантов случайных экскурсий
        public double RandomExcursionsVariantTest(string bits)
        {
            int n = bits.Length;
            if (n < 1000) return -1;

            int[] x = new int[n];
            for (int i = 0; i < n; i++)
                x[i] = bits[i] == '1' ? 1 : -1;

            List<int> cumulativeSum = new();
            int s = 0;
            for (int i = 0; i < x.Length; i++)
            {
                s += x[i];
                cumulativeSum.Add(s);
            }

            Dictionary<int, int> stateVisits = new();
            for (int i = 0; i < cumulativeSum.Count; i++)
            {
                int val = cumulativeSum[i];
                if (val == 0) continue;
                if (Math.Abs(val) > 9) continue;

                if (!stateVisits.ContainsKey(val)) stateVisits[val] = 0;
                stateVisits[val]++;
            }

            double sqrt2n = Math.Sqrt(2.0 * n);
            List<double> pValues = new();

            for (int xState = -9; xState <= 9; xState++)
            {
                if (xState == 0) continue;

                int count = stateVisits.ContainsKey(xState) ? stateVisits[xState] : 0;
                double expected = 2.0 * (NormalCDF((xState + 0.5) / sqrt2n) - NormalCDF((xState - 0.5) / sqrt2n));
                double p = Math.Exp(-2.0 * n * Math.Pow(expected - ((double)count / n), 2));
                pValues.Add(p);
            }

            return pValues.Count > 0 ? pValues.Min() : -1;
        }
        #endregion

        #region For Random Excursions Test
        public double RandomExcursionsTestOnHashStream(Func<byte[], byte[]> hashFunction, int requiredBits = 1_000_000)
        {
            string bits = GenerateHashStream(hashFunction, requiredBits);
            return RandomExcursionsTest(bits);
        }

        public double RandomExcursionsVariantTestOnHashStream(Func<byte[], byte[]> hashFunction, int requiredBits = 1_000_000)
        {
            string bits = GenerateHashStream(hashFunction, requiredBits);
            return RandomExcursionsVariantTest(bits);
        }
        #endregion

        #region Auxiliary calculation
        // χ2 CDF аппроксимация с помощью серии
        public double ChiSquaredCDF(double x, int k)
        {
            if (x < 0 || k <= 0) return -1;

            double a = k / 2.0;
            double gamma = GammaLowerIncomplete(a, x / 2.0);
            double fullGamma = GammaFunction(a);
            return gamma / fullGamma;
        }

        // Γ(s, x) — нижняя неполная гамма-функция
        public double GammaLowerIncomplete(double s, double x)
        {
            double sum = 0.0;
            double term = 1.0 / s;
            double n = 0;
            while (term > 1e-15)
            {
                sum += term;
                n++;
                term *= x / (s + n);
            }

            return Math.Pow(x, s) * Math.Exp(-x) * sum;
        }

        // Γ(s) — гамма-функция через ланцос-аппроксимацию
        public double GammaFunction(double z)
        {
            double[] p = {
                676.5203681218851, -1259.1392167224028,
                771.32342877765313, -176.61502916214059,
                12.507343278686905, -0.13857109526572012,
                9.9843695780195716e-6, 1.5056327351493116e-7
            };

            int g = 7;
            if (z < 0.5)
            {
                return Math.PI / (Math.Sin(Math.PI * z) * GammaFunction(1 - z));
            }

            z -= 1;
            double x = 0.99999999999980993;
            for (int i = 0; i < p.Length; i++)
                x += p[i] / (z + i + 1);

            double t = z + g + 0.5;
            return Math.Sqrt(2 * Math.PI) * Math.Pow(t, z + 0.5) * Math.Exp(-t) * x;
        }

        // 1 - erf(x), быстрая аппроксимация
        public double ErfComplement(double x)
        {
            double z = Math.Abs(x);
            double t = 1.0 / (1.0 + 0.5 * z);
            double ans = t * Math.Exp(-z * z - 1.26551223 +
                t * (1.00002368 +
                t * (0.37409196 +
                t * (0.09678418 +
                t * (-0.18628806 +
                t * (0.27886807 +
                t * (-1.13520398 +
                t * (1.48851587 +
                t * (-0.82215223 +
                t * 0.17087277)))))))));
            return x >= 0.0 ? ans : 2.0 - ans;
        }

        public double NormalCDF(double x)
        {
            if (double.IsNaN(x))
                return 0.0;

            if (double.IsPositiveInfinity(x))
                return 1.0;

            if (double.IsNegativeInfinity(x))
                return 0.0;

            // Используем свойство симметрии для отрицательных x
            if (x < 0)
                return 1.0 - NormalCDF(-x);

            // Для больших x возвращаем 1.0 (избегаем численных погрешностей)
            if (x > 8.0)
                return 1.0;

            return 1.0 - 0.5 * ErfComplement(x / Math.Sqrt(2));
        }

        public int ComputeRank(int[,] matrix)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            int rank = 0;

            for (int row = 0, col = 0; row < rows && col < cols; col++)
            {
                // Найти первую строку с ненулевым элементом в текущем столбце
                int pivotRow = -1;
                for (int i = row; i < rows; i++)
                {
                    if (matrix[i, col] == 1)
                    {
                        pivotRow = i;
                        break;
                    }
                }

                // Если опорного элемента нет — переход к следующему столбцу
                if (pivotRow == -1)
                    continue;

                // Обмен строк (если необходимо)
                if (pivotRow != row)
                {
                    for (int j = 0; j < cols; j++)
                    {
                        int temp = matrix[row, j];
                        matrix[row, j] = matrix[pivotRow, j];
                        matrix[pivotRow, j] = temp;
                    }
                }

                // Обнуление всех 1-х ниже текущей строки в текущем столбце
                for (int i = 0; i < rows; i++)
                {
                    if (i != row && matrix[i, col] == 1)
                    {
                        for (int j = 0; j < cols; j++)
                        {
                            matrix[i, j] ^= matrix[row, j]; // XOR строк
                        }
                    }
                }

                rank++;
                row++;
            }

            return rank;
        }

        // Преобразование hex в binary
        public string ConvertHexToBinary(string hex)
        {
            return string.Join("", hex.Select(c => Convert.ToString(Convert.ToInt32(c.ToString(), 16), 2).PadLeft(4, '0')));
        }

        // Факториал числа
        public double Factorial(int n)
        {
            if (n < 0) return 0;
            if (n == 0) return 1;

            double result = 1;
            for (int i = 1; i <= n; i++)
            {
                result *= i;
            }
            return result;
        }

        // Берлекамп Мэсси
        public int BerlekampMassey(string bits)
        {
            int n = bits.Length;
            int[] C = new int[n];
            int[] B = new int[n];
            C[0] = B[0] = 1;

            int L = 0, m = -1, d;

            for (int N = 0; N < n; N++)
            {
                d = bits[N] - '0';
                for (int i = 1; i <= L; i++)
                    d ^= C[i] * (bits[N - i] - '0');

                if (d == 1)
                {
                    int[] T = (int[])C.Clone();
                    for (int i = 0; i < n - N + m; i++)
                        C[N - m + i] ^= B[i];
                    if (2 * L <= N)
                    {
                        L = N + 1 - L;
                        m = N;
                        B = T;
                    }
                }
            }
            return L;
        }

        public double Phi(string bits, int m)
        {
            int n = bits.Length;
            Dictionary<string, int> freq = new();
            string extended = bits + bits.Substring(0, m - 1); // циркулярность

            for (int i = 0; i < n; i++)
            {
                string pattern = extended.Substring(i, m);
                if (!freq.ContainsKey(pattern)) freq[pattern] = 0;
                freq[pattern]++;
            }

            double sum = 0;
            foreach (int count in freq.Values)
            {
                double p = (double)count / n;
                sum += p * Math.Log(p);
            }

            return sum;
        }

        public double Igamc(double a, double x)
        {
            // Используем аппроксимацию через регуляризованную верхнюю гамма-функцию
            // Q(a, x) = Γ(a, x) / Γ(a)

            // Численная стабильная реализация
            // Серия Лагерра / непрерывная дробь
            double eps = 1e-14;
            double sum = 1.0 / a;
            double value = sum;
            for (int n = 1; n < 1000; n++)
            {
                sum *= x / (a + n);
                value += sum;
                if (sum < eps * value) break;
            }
            double front = Math.Exp(-x + a * Math.Log(x) - LogGamma(a));
            return 1.0 - front * value;
        }

        private double LogGamma(double x)
        {
            // Ланцош-аппроксимация
            double[] coef = {
                76.18009172947146,
               -86.50532032941677,
                24.01409824083091,
                -1.231739572450155,
                 0.1208650973866179e-2,
                -0.5395239384953e-5
            };
            double y = x;
            double tmp = x + 5.5;
            tmp -= (x + 0.5) * Math.Log(tmp);
            double ser = 1.000000000190015;
            for (int j = 0; j <= 5; j++) ser += coef[j] / ++y;
            return -tmp + Math.Log(2.5066282746310005 * ser / x);
        }

        public string GenerateHashStream(Func<byte[], byte[]> hashFunction, int requiredBits)
        {
            if (hashFunction == null)
                throw new ArgumentNullException(nameof(hashFunction));

            if (requiredBits <= 0)
                throw new ArgumentOutOfRangeException(nameof(requiredBits));

            // Ограничение на максимально возможный размер строки в .NET
            // 2 млрд бит ~= 250 MB. Это предел.
            const int MAX_BITS = 2_000_000_000;

            if (requiredBits > MAX_BITS)
                requiredBits = MAX_BITS; // защита от переполнений

            var sb = new StringBuilder();

            byte[] counter = new byte[8];

            while (sb.Length < requiredBits)
            {
                byte[] input = counter.ToArray();
                byte[] hash = hashFunction(input);

                foreach (byte b in hash)
                {
                    for (int bit = 7; bit >= 0; bit--)
                    {
                        sb.Append(((b >> bit) & 1) == 1 ? '1' : '0');

                        if (sb.Length >= requiredBits)
                            return sb.ToString();
                    }
                }

                // Инкремент счётчика
                for (int i = 0; i < counter.Length; i++)
                {
                    if (++counter[i] != 0)
                        break;
                }
            }

            return sb.ToString();
        }
        #endregion
    }
}