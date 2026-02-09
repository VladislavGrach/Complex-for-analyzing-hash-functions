using System;
using System.Numerics;
using System.Text;
using Complex_for_analyzing_hash_functions.Interfaces;

namespace Complex_for_analyzing_hash_functions.Services
{
    public class TestU01Service : ITestU01Service
    {
        #region Collision Test
        public double CollisionTest(string bits, int t = 20, int n = 500_000)
        {
            int availableBits = bits.Length;
            int requiredBits = n * t;

            // Проверка первых 100 символов
            int checkLimit = Math.Min(100, bits.Length);
            for (int i = 0; i < checkLimit; i++)
            {
                char c = bits[i];
                if (c != '0' && c != '1')
                {
                    return double.NaN;
                }
            }

            // Инициализация
            int m = 1 << t; // 2^t
            int[] counts = new int[m];
            int processed = 0;

            // Обработка с защитой от выхода за границы
            for (int i = 0; i < n; i++)
            {
                int startPos = i * t;

                // Фиксированная проверка: гарантируем, что не выйдем за границы
                if (startPos + t > bits.Length)
                {
                    break;
                }

                int value = 0;
                bool valid = true;

                // Чтение t битов
                for (int j = 0; j < t; j++)
                {
                    int pos = startPos + j;
                    char c = bits[pos];

                    if (c == '1')
                        value = (value << 1) | 1;
                    else if (c == '0')
                        value = value << 1;
                    else
                    {
                        valid = false;
                        break;
                    }
                }

                if (!valid) continue;

                // Запись в массив
                if (value >= 0 && value < m)
                {
                    counts[value]++;
                    processed++;
                }
            }

            return AnalyzeCollisionResults(counts, m, processed);
        }

        private double AnalyzeCollisionResults(int[] counts, int m, int n)
        {
            long c0 = 0, c1 = 0, c2 = 0, c3p = 0;

            for (int i = 0; i < m; i++)
            {
                switch (counts[i])
                {
                    case 0: c0++; break;
                    case 1: c1++; break;
                    case 2: c2++; break;
                    default: c3p++; break;
                }
            }

            double lambda = (double)n / m;

            // Вычисление статистики
            double exp_c0 = m * Math.Exp(-lambda);
            double exp_c1 = m * lambda * Math.Exp(-lambda);
            double exp_c2 = m * (lambda * lambda / 2.0) * Math.Exp(-lambda);
            double exp_c3p = m * (1.0 - (1.0 + lambda + lambda * lambda / 2.0) * Math.Exp(-lambda));

            double chi2 = Math.Pow(c0 - exp_c0, 2) / exp_c0 +
                          Math.Pow(c1 - exp_c1, 2) / exp_c1 +
                          Math.Pow(c2 - exp_c2, 2) / exp_c2 +
                          Math.Pow(c3p - exp_c3p, 2) / exp_c3p;

            double pValue = Math.Exp(-chi2 / 2.0); // Аппроксимация

            return Math.Clamp(pValue, 0.0, 1.0);
        }
        #endregion

        #region Gap Test
        public double GapTest(string bits, int t = 20, int n = 500_000)
        {
            int pos = 0;
            var u = new double[n];
            int actualN = 0;

            for (int i = 0; i < n && pos + t <= bits.Length; i++)
            {
                int v = 0;
                for (int j = 0; j < t; j++)
                    v = (v << 1) | (bits[pos++] - '0');

                u[actualN++] = v / (double)(1 << t);
            }


            const double Alpha = 0.0;
            const double Beta = 0.2;
            const int MaxGap = 10;

            long[] observed = new long[MaxGap + 1]; // 0..9 + ≥10

            int currentGap = 0;
            bool waitingForSuccess = true; // изначально ждём первый успех

            foreach (double x in u.Take(actualN))
            {
                bool success = (x >= Alpha && x < Beta);

                if (success)
                {
                    // УСПЕХ!
                    if (waitingForSuccess)
                    {
                        // Первый успех — предыдущий gap не существует (или бесконечный), не считаем
                        waitingForSuccess = false;
                    }
                    else
                    {
                        // Фиксируем gap между двумя успехами
                        int bucket = Math.Min(currentGap, MaxGap);
                        observed[bucket]++;
                    }
                    currentGap = 0; // начинаем новый gap
                }
                else
                {
                    if (!waitingForSuccess)
                    {
                        currentGap++;
                    }
                    // если waitingForSuccess == true — просто пропускаем до первого успеха
                }
            }

            // Если последний gap не завершён (после последнего успеха шли только неудачи)
            if (!waitingForSuccess && currentGap > 0)
            {
                int bucket = Math.Min(currentGap, MaxGap);
                observed[bucket]++;
            }

            double p = Beta - Alpha;  // 0.2
            double q = 1.0 - p;        // 0.8

            double totalGaps = 0;
            for (int i = 0; i <= MaxGap; i++) totalGaps += observed[i];


            double chi2 = 0.0;
            double qPow = 1.0;

            for (int i = 0; i < MaxGap; i++)
            {
                double expected = totalGaps * p * qPow;
                if (expected > 1e-8)
                    chi2 += Math.Pow(observed[i] - expected, 2) / expected;
                qPow *= q;
            }

            // Хвост ≥10
            double expectedTail = totalGaps * qPow;
            if (expectedTail > 1e-8)
                chi2 += Math.Pow(observed[MaxGap] - expectedTail, 2) / expectedTail;

            return 1.0 - ChiSquaredCDF(chi2, MaxGap);
        }
        #endregion

        #region Autocorrelation Test
        public double AutocorrelationTest(string bits, int d = 1)
        {
            if (string.IsNullOrEmpty(bits) || d <= 0)
                return double.NaN;

            const int MAX_BITS = 1_000_000;

            int n = Math.Min(bits.Length, MAX_BITS);
            int m = n - d;

            long A = 0;

            for (int i = 0; i < m; i++)
            {
                if (bits[i] != bits[i + d])
                    A++;
            }

            double expected = m / 2.0;
            double variance = m / 4.0;

            double z = (A - expected) / Math.Sqrt(variance);
            double pValue = Erfc(Math.Abs(z) / Math.Sqrt(2.0));

            return Math.Clamp(pValue, 0.0, 1.0);
        }
        #endregion

        #region Spectral Test (Discrete Fourier Transform Test)
        public double SpectralTest(string bits)
        {
            // Используем максимум 1M бит для производительности
            int maxN = Math.Min(bits.Length, 1 << 20); // 1,048,576
            int N = 1;
            while ((N << 1) <= maxN) N <<= 1;

            // Преобразование битов в ±1
            Complex[] data = new Complex[N];
            for (int i = 0; i < N; i++)
            {
                data[i] = new Complex(bits[i] == '1' ? 1.0 : -1.0, 0.0);
            }

            // FFT (in-place)
            FFT(data);

            // Подсчет точек ниже порога
            double threshold = Math.Sqrt(2.995732274 * N); // sqrt(ln(20) * N)
            int M = N / 2;
            int countBelow = 0;

            for (int k = 1; k <= M; k++)
            {
                double mag = Math.Sqrt(data[k].Real * data[k].Real + data[k].Imag * data[k].Imag);
                if (mag < threshold)
                    countBelow++;
            }

            // Статистика
            double expected = 0.95 * M;
            double variance = M * 0.95 * 0.05;
            double d = (countBelow - expected) / Math.Sqrt(variance);

            // P-value
            double pValue = 2.0 * (1.0 - NormalCDF(Math.Abs(d)));

            return Math.Clamp(pValue, 0.0, 1.0);
        }

        // Вспомогательный класс Complex
        public struct Complex
        {
            public double Real;
            public double Imag;

            public Complex(double real, double imag)
            {
                Real = real;
                Imag = imag;
            }

            public double Magnitude => Math.Sqrt(Real * Real + Imag * Imag);

            public static Complex operator *(Complex a, Complex b)
            {
                return new Complex(
                    a.Real * b.Real - a.Imag * b.Imag,
                    a.Real * b.Imag + a.Imag * b.Real);
            }

            public static Complex operator +(Complex a, Complex b)
            {
                return new Complex(a.Real + b.Real, a.Imag + b.Imag);
            }

            public static Complex operator -(Complex a, Complex b)
            {
                return new Complex(a.Real - b.Real, a.Imag - b.Imag);
            }
        }
        #endregion

        #region Hamming Weight Test
        public double HammingWeightTest(string bits)
        {
            // Стандартные параметры как в TestU01
            const int L = 32;     // Размер блока (32 бита)
            const int N = 100000; // Количество блоков

            int[] weightDistribution = new int[L + 1]; // Частоты весов 0..L

            // Обработка блоков с фиксированными позициями (безопасно!)
            for (int block = 0; block < N; block++)
            {
                int startPos = block * L;

                // Абсолютно безопасное чтение
                if (startPos + L > bits.Length)
                {
                    break;
                }

                // Подсчет веса Хэмминга
                int weight = 0;
                for (int i = 0; i < L; i++)
                {
                    if (bits[startPos + i] == '1')
                        weight++;
                }

                if (weight >= 0 && weight <= L)
                    weightDistribution[weight]++;
            }

            // Теоритическое биномиальное распределение B(L, 0.5)
            double[] expected = new double[L + 1];
            double totalBlocks = weightDistribution.Sum();

            // Вычисляем биномиальные коэффициенты и вероятности
            double logBinom = 0;
            for (int w = 0; w <= L; w++)
            {
                // log(C(L,w) * 0.5^L)
                if (w == 0)
                    logBinom = L * Math.Log(0.5);
                else
                    logBinom += Math.Log(L - w + 1) - Math.Log(w);

                expected[w] = totalBlocks * Math.Exp(logBinom);
            }

            // χ² тест
            double chi2 = 0;
            int df = 0;

            for (int w = 0; w <= L; w++)
            {
                if (expected[w] >= 5.0) // Правило Кохрана
                {
                    chi2 += Math.Pow(weightDistribution[w] - expected[w], 2) / expected[w];
                    df++;
                }
            }

            df--; // Минус один параметр

            if (df <= 0)
            {
                return double.NaN;
            }

            double pValue = 1.0 - ChiSquaredCDF(chi2, df);

            return Math.Clamp(pValue, 0.0, 1.0);
        }
        #endregion

        #region Serial Test
        public double SerialTest(string bits, int t = 2, int k = 2, int n = 500000)
        {
            if (t <= 0 || t > 10) throw new ArgumentOutOfRangeException(nameof(t));
            if (k < 2 || k > 5) throw new ArgumentOutOfRangeException(nameof(k));

            int M = 1 << t;           // алфавит
            int K = (int)Math.Pow(M, k);      // кол-во k-tuple
            int K1 = (int)Math.Pow(M, k - 1);  // кол-во (k-1)-tuple

            int requiredBits = t * (n + k);  // запас на k-шаг
            if (bits.Length < requiredBits)
            {
                return double.NaN;
            }

            // 1. Читаем X_i
            int[] X = new int[n + k];
            int pos = 0;

            for (int i = 0; i < X.Length; i++)
            {
                int v = 0;
                for (int j = 0; j < t; j++)
                    v = (v << 1) | (bits[pos++] - '0');
                X[i] = v;
            }

            // 2. Считаем k-tuple
            long[] freqK = new long[K];
            long[] freqK1 = new long[K1];

            int tuples = n;

            for (int i = 0; i < tuples; i++)
            {
                // k-tuple ID
                int id = 0;
                for (int j = 0; j < k; j++)
                    id = id * M + X[i + j];
                freqK[id]++;

                // (k-1)-tuple
                int id1 = 0;
                for (int j = 0; j < k - 1; j++)
                    id1 = id1 * M + X[i + j];
                freqK1[id1]++;
            }

            // 3. Ожидания
            double EK = (double)tuples / K;
            double EK1 = (double)tuples / K1;

            // 4. χ²_k
            double chiK = 0.0;
            for (int i = 0; i < K; i++)
            {
                double diff = freqK[i] - EK;
                chiK += diff * diff / EK;
            }

            // 5. χ²_{k-1}
            double chiK1val = 0.0;
            for (int i = 0; i < K1; i++)
            {
                double diff = freqK1[i] - EK1;
                chiK1val += diff * diff / EK1;
            }

            // 6. Финальная статистика TestU01:
            double stat = chiK - chiK1val;

            int df = K - K1;

            double pValue = 1.0 - ChiSquaredCDF(stat, df);
            if (double.IsNaN(pValue) || double.IsInfinity(pValue)) pValue = 0.0;

            return Math.Clamp(pValue, 0.0, 1.0);
        }
        #endregion

        #region Multinomia lTest
        public double MultinomialTest(string bits, int t = 2, int k = 3, int n = 200_000)
        {
            // 1. Валидация параметров
            if (t <= 0 || t > 12) throw new ArgumentOutOfRangeException(nameof(t), "t должно быть 1..12");
            if (k < 2 || k > 6) throw new ArgumentOutOfRangeException(nameof(k), "k должно быть 2..6");
            if (n < 1000) throw new ArgumentOutOfRangeException(nameof(n), "n должно быть ≥1000");

            int M = 1 << t;               // размер алфавита (2^t)
            int K = (int)Math.Pow(M, k);  // кол-во k-кортежей

            int requiredBlocks = n + k - 1; // нужно n + k - 1 символов
            int requiredBits = requiredBlocks * t;

            if (bits == null || bits.Length < requiredBits)
            {
                return double.NaN;
            }

            // 2. Чтение последовательности X_i
            int[] X = new int[requiredBlocks];
            int pos = 0;
            for (int i = 0; i < requiredBlocks; i++)
            {
                int v = 0;
                for (int j = 0; j < t; j++)
                {
                    char c = bits[pos++];
                    if (c == '1') v = (v << 1) | 1;
                    else if (c == '0') v = v << 1;
                    else throw new ArgumentException($"Invalid bit '{c}' at position {pos - 1}");
                }
                X[i] = v;
            }

            // 3. Подсчёт частот k-кортежей
            long[] freq = new long[K];
            for (int i = 0; i < n; i++)
            {
                int id = 0;
                for (int j = 0; j < k; j++)
                    id = id * M + X[i + j];
                freq[id]++;
            }

            // 4. Ожидаемое значение
            double E = (double)n / K;
            if (E < 5.0)
            {
                return double.NaN;
            }

            // 5. χ²-статистика
            double chi2 = 0.0;
            for (int i = 0; i < K; i++)
            {
                double diff = freq[i] - E;
                chi2 += diff * diff / E;
            }

            int df = K - 1;
            double pValue = 1.0 - ChiSquaredCDF(chi2, df);

            return Math.Clamp(pValue, 0.0, 1.0);
        }
        #endregion

        #region Close Pairs Test
        public double ClosePairsTest(string bits, int t = 20, int n = 200000, int k = 256)
        {
            if (bits.Length < n * t)
                return 0.0;

            double[] u = new double[n];
            int pos = 0;

            // bits → U(0,1)
            for (int i = 0; i < n; i++)
            {
                int v = 0;
                for (int j = 0; j < t; j++)
                    v = (v << 1) | (bits[pos++] - '0');

                u[i] = v / (double)(1 << t);
            }

            int[] counts = new int[k];

            // распределяем по корзинам
            for (int i = 0; i < n; i++)
            {
                int cell = (int)(u[i] * k);
                if (cell == k) cell = k - 1;
                counts[cell]++;
            }

            double expected = (double)n / k;

            // χ²
            double chi2 = 0.0;
            for (int i = 0; i < k; i++)
            {
                double diff = counts[i] - expected;
                chi2 += diff * diff / expected;
            }

            int df = k - 1;
            double pValue = 1.0 - ChiSquaredCDF(chi2, df);

            if (double.IsNaN(pValue) || double.IsInfinity(pValue))
                return 0.0;

            return Math.Clamp(pValue, 0.0, 1.0);
        }
        #endregion

        #region Coupon Collector Test
        public double CouponCollectorTest(string bits, int t = 8, int S = 500)
        {
            int d = 1 << t;
            int bitPos = 0;

            int ReadBit()
            {
                int b = bits[bitPos] == '1' ? 1 : 0;
                bitPos++;
                if (bitPos >= bits.Length)
                    bitPos = 0;
                return b;
            }

            // --- теоретические моменты ---
            double H1 = 0.0;
            double H2 = 0.0;
            for (int k = 1; k <= d; k++)
            {
                H1 += 1.0 / k;
                H2 += 1.0 / (k * (double)k);
            }

            double expected = d * H1;
            double variance = d * d * H2;

            // --- эксперимент ---
            double sum = 0.0;
            int maxDraws = 0;

            for (int s = 0; s < S; s++)
            {
                bool[] seen = new bool[d];
                int collected = 0;
                int draws = 0;

                while (collected < d)
                {
                    int v = 0;
                    for (int i = 0; i < t; i++)
                        v = (v << 1) | ReadBit();

                    draws++;

                    if (!seen[v])
                    {
                        seen[v] = true;
                        collected++;
                    }
                }

                sum += draws;
                if (draws > maxDraws)
                    maxDraws = draws;
            }

            double mean = sum / S;
            double z = (mean - expected) / Math.Sqrt(variance / S);
            double pValue = 2.0 * (1.0 - NormalCDF(Math.Abs(z)));

            return Math.Clamp(pValue, 0.0, 1.0);
        }


        #endregion

        #region Auxiliary calculation
        private double ChiSquaredCDF(double x, int k)
        {
            if (x < 0.0) return 0.0;
            if (k <= 0) return 0.0;
            double a = k / 2.0;
            double xs = x / 2.0;
            return RegularizedGammaP(a, xs);
        }

        private double RegularizedGammaP(double a, double x)
        {
            const int MAX_ITERS = 10000;
            const double EPS = 1e-14;

            if (x < 0.0 || a <= 0.0) return double.NaN;
            if (x == 0.0) return 0.0;

            if (x < a + 1.0)
            {
                double sum = 1.0 / a;
                double del = sum;
                int n = 1;
                while (Math.Abs(del) > Math.Abs(sum) * EPS && n < MAX_ITERS)
                {
                    del *= x / (a + n);
                    sum += del;
                    n++;
                }
                double result = sum * Math.Exp(-x + a * Math.Log(x) - LogGamma(a));
                return Math.Max(0.0, Math.Min(1.0, result));
            }
            else
            {
                double q = IncompleteGammaContinuedFractionQ(a, x, EPS, MAX_ITERS);
                return Math.Max(0.0, Math.Min(1.0, 1.0 - q));
            }
        }

        private double IncompleteGammaContinuedFractionQ(double a, double x, double eps, int maxIters)
        {
            double gln = LogGamma(a);
            const double FPMIN = 1e-300;

            double b = x + 1.0 - a;
            double c = 1.0 / FPMIN;
            double d = 1.0 / b;
            double h = d;

            int i = 1;
            while (i < maxIters)
            {
                double an = -i * (i - a);
                b += 2.0;
                d = an * d + b;
                if (Math.Abs(d) < FPMIN) d = FPMIN;
                c = b + an / c;
                if (Math.Abs(c) < FPMIN) c = FPMIN;
                d = 1.0 / d;
                double delta = d * c;
                h *= delta;
                if (Math.Abs(delta - 1.0) < eps) break;
                i++;
            }

            double result = Math.Exp(-x + a * Math.Log(x) - gln) * h;
            return Math.Max(0.0, Math.Min(1.0, result));
        }

        private double LogGamma(double z)
        {
            // Lanczos coefficients for g=7, n=9
            double[] pLanczos = {
                0.99999999999980993,
                676.5203681218851,
               -1259.1392167224028,
                771.32342877765313,
               -176.61502916214059,
                12.507343278686905,
               -0.13857109526572012,
                9.9843695780195716e-6,
                1.5056327351493116e-7
            };

            if (z < 0.5)
            {
                // Reflection formula for better accuracy on (0,0.5)
                return Math.Log(Math.PI) - Math.Log(Math.Sin(Math.PI * z)) - LogGamma(1.0 - z);
            }

            z -= 1.0;
            double x = pLanczos[0];
            for (int i = 1; i < pLanczos.Length; i++)
                x += pLanczos[i] / (z + i);

            double t = z + pLanczos.Length - 0.5;
            return 0.5 * Math.Log(2.0 * Math.PI) + (z + 0.5) * Math.Log(t) - t + Math.Log(x);
        }

        private double Erfc(double x)
        {
            // численная аппроксимация
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

        private void FFT(Complex[] data)
        {
            int n = data.Length;
            if (n <= 1) return;

            // Разделение на четные и нечетные
            Complex[] even = new Complex[n / 2];
            Complex[] odd = new Complex[n / 2];

            for (int i = 0; i < n / 2; i++)
            {
                even[i] = data[2 * i];
                odd[i] = data[2 * i + 1];
            }

            // Рекурсия
            FFT(even);
            FFT(odd);

            // Объединение
            for (int k = 0; k < n / 2; k++)
            {
                double angle = -2.0 * Math.PI * k / n;
                Complex t = new Complex(Math.Cos(angle), Math.Sin(angle)) * odd[k];

                data[k] = even[k] + t;
                data[k + n / 2] = even[k] - t;
            }
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

            return 1.0 - 0.5 * Erfc(x / Math.Sqrt(2));
        }
        #endregion
    }
}