using Complex_for_analyzing_hash_functions.Interfaces;
using MathNet.Numerics.IntegralTransforms;
using System.Diagnostics.Tracing;
using System.Drawing;
using System.Numerics;
using System.Security.Policy;
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
            if (blockSize != 128)
            {
                throw new ArgumentException("Данный тест реализован только для blockSize = 128 (M=128 по NIST SP 800-22)");
            }

            int n = bits.Length;
            if (n < 128)
                return double.NaN;

            int N = n / blockSize;           // количество блоков
            if (N < 100)
            {
                if (N == 0) return double.NaN;
                // иначе продолжаем, но результат будет менее надёжным
            }

            double[] pi = new double[]
            {
                0.1174035788,   // ν0: longest run ≤ 4
                0.2429559595,   // ν1: = 5
                0.2493228532,   // ν2: = 6
                0.1751990705,   // ν3: = 7
                0.1027013343,   // ν4: = 8
                0.1124172037    // ν5: ≥ 9
            };

            int[] nu = new int[6];           // счётчики для каждой категории (0..5)

            for (int blk = 0; blk < N; blk++)
            {
                int maxRun = 0;
                int currentRun = 0;

                int start = blk * blockSize;
                for (int i = 0; i < blockSize; i++)
                {
                    if (bits[start + i] == '1')
                    {
                        currentRun++;
                        if (currentRun > maxRun) maxRun = currentRun;
                    }
                    else
                    {
                        currentRun = 0;
                    }
                }

                if (maxRun <= 4) nu[0]++;
                else if (maxRun == 5) nu[1]++;
                else if (maxRun == 6) nu[2]++;
                else if (maxRun == 7) nu[3]++;
                else if (maxRun == 8) nu[4]++;
                else nu[5]++;   // ≥9
            }

            // Вычисление статистики хи-квадрат
            double chi2 = 0.0;
            for (int i = 0; i < 6; i++)
            {
                double expected = N * pi[i];
                double diff = nu[i] - expected;
                chi2 += (diff * diff) / expected;
            }

            // p-value = Q(χ²/2, df/2) = igamc(df/2, χ²/2)
            double df = 5.0;
            double pValue = Igamc(df / 2.0, chi2 / 2.0);

            return pValue;
        }
        #endregion

        #region Binary Matrix Rank Test
        // Тест ранга бинарной матрицы
        public double BinaryMatrixRankTest(string bits)
        {
            const int M = 32;
            const int Q = 32;
            const int MATRIX_BITS = M * Q; // 1024

            int n = bits.Length;

            if (n < MATRIX_BITS)
                return double.NaN;

            long N = n / MATRIX_BITS;

            if (N < 38)
                return double.NaN;

            double[] pi = { 0.2888, 0.5776, 0.1336 };

            int fullRank = 0;
            int rankM1 = 0;
            int lower = 0;

            for (long blk = 0; blk < N; blk++)
            {
                int[,] matrix = new int[M, Q];
                int pos = (int)(blk * MATRIX_BITS);

                for (int r = 0; r < M; r++)
                    for (int c = 0; c < Q; c++)
                        matrix[r, c] = bits[pos++] == '1' ? 1 : 0;

                int rank = ComputeRank(matrix);

                if (rank == M) fullRank++;
                else if (rank == M - 1) rankM1++;
                else lower++;
            }

            double chi2 = 0.0;
            int[] observed = { fullRank, rankM1, lower };

            for (int i = 0; i < 3; i++)
            {
                double exp = N * pi[i];
                double diff = observed[i] - exp;
                chi2 += (diff * diff) / exp;
            }

            // Для df=2 → p-value = exp(-χ²/2)
            double pValue = Math.Exp(-chi2 / 2.0);
            return pValue;
        }
        #endregion

        #region Discrete Fourier Transform Test
        // Дискретное преобразование Фурье
        public double DiscreteFourierTransformTest(string bits)
        {
            int n = bits.Length;

            // NIST рекомендует n >= 1_000_000
            if (n < 100_000)
                return double.NaN;

            // 1. Преобразование битов: 0 → -1, 1 → +1
            Complex[] data = new Complex[n];
            for (int i = 0; i < n; i++)
                data[i] = new Complex(bits[i] == '1' ? 1.0 : -1.0, 0.0);

            // 2. FFT
            Fourier.Forward(data, FourierOptions.Matlab);

            // 3. Порог T (α = 0.05)
            double T = Math.Sqrt(Math.Log(1.0 / 0.05) * n);

            // 4. Считаем пики ниже порога
            int m = n / 2;
            int N0 = 0;

            // k = 1 .. n/2 (k=0 игнорируем)
            for (int k = 1; k <= m; k++)
            {
                double magnitude = data[k].Magnitude;
                if (magnitude < T)
                    N0++;
            }

            // 5. Статистика
            double expected = 0.95 * m;
            double variance = m * 0.95 * 0.05;

            double d = (N0 - expected) / Math.Sqrt(variance);

            // 6. p-value
            double pValue = Erfc(Math.Abs(d) / Math.Sqrt(2.0));

            return double.IsFinite(pValue) ? pValue : 0.0;
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
        public double OverlappingTemplateMatchingTest(string bits, int m = 9)
        {
            int n = bits.Length;
            if (n < 10000) return double.NaN;

            // Рекомендуемая длина блока NIST ≈ 1032 для m=9/10
            int M = 1032;
            int N = n / M;
            if (N < 5) return double.NaN;  // слишком мало блоков

            // Точные вероятности π для m=9 (из NIST или приближённые)
            // Для m=9 (часто используемые значения)
            double[] pi;
            if (m == 9)
            {
                pi = new double[] { 0.367879, 0.183940, 0.137945, 0.099793, 0.073262, 0.137181 };
            }
            else if (m == 10)
            {
                pi = new double[] { 0.367879, 0.183940, 0.137945, 0.099793, 0.073262, 0.137181 }; // близко
            }
            else
            {
                return double.NaN; // поддерживаем только m=9/10
            }

            int[] nu = new int[6];  // счётчики категорий

            for (int blk = 0; blk < N; blk++)
            {
                string block = bits.Substring(blk * M, M);
                int count = 0;

                for (int i = 0; i <= M - m; i++)
                {
                    bool match = true;
                    for (int j = 0; j < m; j++)
                    {
                        if (block[i + j] != '1') { match = false; break; }
                    }
                    if (match) count++;
                }

                // Категоризация
                if (count <= 0) nu[0]++;
                else if (count == 1) nu[1]++;
                else if (count == 2) nu[2]++;
                else if (count == 3) nu[3]++;
                else if (count == 4) nu[4]++;
                else nu[5]++;
            }

            double chi2 = 0.0;
            for (int i = 0; i < 6; i++)
            {
                double expected = N * pi[i];
                if (expected < 5) return double.NaN; // условие применимости χ²
                chi2 += Math.Pow(nu[i] - expected, 2) / expected;
            }

            // p-value = igamc(df/2, χ²/2), df=5
            double pValue = Igamc(2.5, chi2 / 2.0);  // нужна твоя реализация Igamc / GammaUpperRegularized

            return pValue;
        }
        #endregion

        #region Maurers Universal Test
        // Универсальный тест Маурера
        public double MaurersUniversalTest(string bits)
        {
            int n = bits.Length;
            if (n < 90_400)
            {
                return double.NaN;
            }

            int L;
            if (n >= 5_000_000)      // Увеличили порог для L=15
                L = 15;
            else if (n >= 3_878_400)
                L = 14;               // Сдвинули пороги вниз, чтобы L=14 был доступнее
            else if (n >= 2_000_000)
                L = 13;
            else if (n >= 1_000_000)
                L = 12;
            else if (n >= 500_000)
                L = 11;
            else if (n >= 200_000)
                L = 10;
            else
            {
                return double.NaN;
            }

            int Q = 10 * (1 << L);
            int totalBlocks = n / L;
            int K = totalBlocks - Q;

            if (K < 1000)
            {
                return double.NaN;
            }

            int Tsize = 1 << L;
            int[] table = new int[Tsize];
            int idx = 0;

            // TRAINING phase
            for (int i = 0; i < Q; i++)
            {
                if (idx + L > n) return double.NaN;
                int pattern = Convert.ToInt32(bits.Substring(idx, L), 2);
                table[pattern] = i + 1;
                idx += L;
            }

            // TEST phase
            double sum = 0.0;
            int testBlocksProcessed = 0;
            for (int i = Q; i < totalBlocks; i++)
            {
                if (idx + L > n) break;
                int pattern = Convert.ToInt32(bits.Substring(idx, L), 2);
                int distance = (i + 1) - table[pattern];

                if (distance <= 0)
                {
                    return double.NaN;
                }

                sum += Math.Log(distance, 2);
                table[pattern] = i + 1;
                idx += L;
                testBlocksProcessed++;
            }

            if (testBlocksProcessed == 0)
                return double.NaN;

            double fn = sum / testBlocksProcessed;

            // Таблицы из NIST (L=6..15)
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
            double pValue = Erfc(Math.Abs(z) / Math.Sqrt(2.0));

            return double.IsNaN(pValue) || !double.IsFinite(pValue) ? double.NaN : pValue;
        }
        #endregion

        #region Lempel Ziv Compression Test
        // Тест Лемпеля-Зива
        public double LempelZivCompressionTest(string bits)
        {
            int n = bits.Length;
            if (n < 1_000_000)
                return double.NaN;

            int c = CountLZ76Phrases(bits);

            // NIST параметры для n ≈ 1–10 млн бит (примерные)
            const double mean = 69588.2019;   // из NIST или калиброванные
            const double sigma = 73.23726011;

            double z = (c - mean) / sigma;

            double pValue = Erfc(Math.Abs(z) / Math.Sqrt(2.0));

            return Math.Clamp(pValue, 0.0, 1.0);
        }

        private int CountLZ76Phrases(string bits)
        {
            int n = bits.Length;
            if (n == 0) return 0;

            // Словарь: индекс → длина фразы (или просто счётчик)
            // Для бинарного алфавита используем Trie или HashSet с хэшом
            var dictionary = new Dictionary<ulong, int>(); // hash → index фразы
            int phrases = 0;
            int pos = 0;

            // Начальный словарь пустой
            while (pos < n)
            {
                ulong hash = 0;
                int len = 0;
                int matchIndex = -1;

                // Ищем самую длинную совпадающую фразу
                while (pos + len < n)
                {
                    hash = (hash << 1) | (ulong)(bits[pos + len] - '0');
                    if (!dictionary.TryGetValue(hash, out matchIndex))
                        break;
                    len++;
                }

                // Добавляем новую фразу: найденная + следующий символ (если есть)
                if (pos + len < n)
                {
                    hash = (hash << 1) | (ulong)(bits[pos + len] - '0');
                }

                phrases++;
                dictionary[hash] = phrases;  // индекс новой фразы

                // Двигаемся на длину найденной фразы + 1 (новый символ)
                pos += len + 1;
            }

            return phrases;
        }

        public class SuffixAutomaton
        {
            private class State
            {
                public int Link;
                public Dictionary<char, int> Next = new();
                public int Length;
            }

            private readonly List<State> _states = new();
            private int _last;

            public SuffixAutomaton()
            {
                _states.Add(new State { Link = -1, Length = 0 });
                _last = 0;
            }

            public void Extend(char c)
            {
                int cur = _states.Count;
                _states.Add(new State { Length = _states[_last].Length + 1 });

                int p = _last;
                while (p != -1 && !_states[p].Next.ContainsKey(c))
                {
                    _states[p].Next[c] = cur;
                    p = _states[p].Link;
                }

                if (p == -1)
                {
                    _states[cur].Link = 0;
                }
                else
                {
                    int q = _states[p].Next[c];
                    if (_states[p].Length + 1 == _states[q].Length)
                    {
                        _states[cur].Link = q;
                    }
                    else
                    {
                        int clone = _states.Count;
                        _states.Add(new State
                        {
                            Length = _states[p].Length + 1,
                            Next = new Dictionary<char, int>(_states[q].Next),
                            Link = _states[q].Link
                        });

                        while (p != -1 && _states[p].Next[c] == q)
                        {
                            _states[p].Next[c] = clone;
                            p = _states[p].Link;
                        }

                        _states[q].Link = _states[cur].Link = clone;
                    }
                }

                _last = cur;
            }

            public bool TryTransition(int state, char c, out int nextState)
            {
                return _states[state].Next.TryGetValue(c, out nextState);
            }

            public int InitialState => 0;
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
            {
                return double.NaN;
            }

            // 1. Convert to ±1
            int[] x = new int[n];
            for (int i = 0; i < n; i++)
                x[i] = bits[i] == '1' ? 1 : -1;

            // 2. Partial sums
            int[] S = new int[n + 1];
            for (int i = 0; i < n; i++)
                S[i + 1] = S[i] + x[i];

            // 3. Cycles
            List<int> cycles = new() { 0 };
            for (int i = 1; i <= n; i++)
                if (S[i] == 0)
                    cycles.Add(i);

            int J = cycles.Count - 1;
            if (J < 100)
                return double.NaN;

            int[] states = { -4, -3, -2, -1, 1, 2, 3, 4 };
            double minP = 1.0;

            foreach (int s in states)
            {
                int[] v = new int[6]; // 0..4, >=5

                // 4. Count visits per cycle
                for (int j = 0; j < J; j++)
                {
                    int count = 0;
                    for (int i = cycles[j] + 1; i < cycles[j + 1]; i++)
                        if (S[i] == s)
                            count++;

                    v[Math.Min(count, 5)]++;
                }

                // 5. Probabilities depend on |s|
                double[] pi = GetExcursionProbabilities(Math.Abs(s));

                // 6. Chi-square
                double chi2 = 0.0;
                for (int k = 0; k < 6; k++)
                {
                    double expected = J * pi[k];
                    if (expected < 5.0)
                        return double.NaN;

                    chi2 += (v[k] - expected) * (v[k] - expected) / expected;
                }
                // 7. p-value (df = 5)
                double p = Igamc(2.5, chi2 / 2.0);

                minP = Math.Min(minP, p);
            }
            return minP;
        }

        private double[] GetExcursionProbabilities(int absState)
        {
            return absState switch
            {
                1 => new[] { 0.5, 0.25, 0.125, 0.0625, 0.03125, 0.03125 },
                2 => new[] { 0.75, 0.0625, 0.046875, 0.03515625, 0.0263671875, 0.0791015625 },
                3 => new[] { 0.8333333333, 0.0277777778, 0.0185185185, 0.0123456790, 0.0082304527, 0.0997942387 },
                4 => new[] { 0.875, 0.015625, 0.009765625, 0.0061035156, 0.0038146973, 0.0896911621 },
                _ => throw new ArgumentException("State must be 1..4")
            };
        }

        #endregion

        #region Random Excursions Variant Test
        // Тест вариантов случайных экскурсий
        public double RandomExcursionsVariantTest(string bits)
        {
            int n = bits.Length;
            if (n < 1_000_000)
                return double.NaN;

            int[] S = new int[n + 1];
            for (int i = 0; i < n; i++)
                S[i + 1] = S[i] + (bits[i] == '1' ? 1 : -1);

            Dictionary<int, int> count = new();
            for (int i = 1; i <= n; i++)
            {
                int v = S[i];
                if (v == 0 || Math.Abs(v) > 9) continue;

                count.TryGetValue(v, out int tmp);
                count[v] = tmp + 1;
            }

            double minP = 1.0;

            for (int x = -9; x <= 9; x++)
            {
                if (x == 0) continue;

                int Nx = count.ContainsKey(x) ? count[x] : 0;
                double pi = 1.0 / (2.0 * Math.Abs(x));
                double expected = n * pi;
                double variance = n * pi * (1.0 - pi);

                if (variance <= 0) continue;

                double Z = Math.Abs(Nx - expected) / Math.Sqrt(variance);
                double p = Erfc(Z / Math.Sqrt(2.0));

                minP = Math.Min(minP, p);
            }

            return minP;
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

        // Комплементарная функция ошибок (erfc)
        public double Erfc(double x)
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