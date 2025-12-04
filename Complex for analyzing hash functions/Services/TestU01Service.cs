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
            int m = 1 << t;
            double lambda = (double)n / m;

            // Критически важная защита: если λ слишком большая — тест бессмыслен
            if (lambda > 10.0)
                return 0.5; // нейтральный результат — тест не применим

            if (bits.Length < n * t)
                return 0.5;

            int[] counts = new int[m];
            int pos = 0;

            for (int i = 0; i < n; i++)
            {
                int v = 0;
                for (int j = 0; j < t; j++)
                {
                    if (pos >= bits.Length) break;
                    v = (v << 1) | (bits[pos++] - '0');
                }
                counts[v]++;
            }

            long c0 = 0, c1 = 0, c2 = 0, c3plus = 0;
            foreach (int cnt in counts)
            {
                if (cnt == 0) c0++;
                else if (cnt == 1) c1++;
                else if (cnt == 2) c2++;
                else if (cnt >= 3) c3plus++;
            }

            // Безопасное вычисление ожиданий
            double exp_c0 = m * Math.Exp(-lambda);
            double exp_c1 = m * lambda * Math.Exp(-lambda);
            double exp_c2 = m * (lambda * lambda / 2.0) * Math.Exp(-lambda);
            double exp_c3plus = m - exp_c0 - exp_c1 - exp_c2;

            double chi2 = 0.0;

            if (exp_c0 > 1e-8) chi2 += Math.Pow(c0 - exp_c0, 2) / exp_c0;
            if (exp_c1 > 1e-8) chi2 += Math.Pow(c1 - exp_c1, 2) / exp_c1;
            if (exp_c2 > 1e-8) chi2 += Math.Pow(c2 - exp_c2, 2) / exp_c2;
            if (exp_c3plus > 1e-8) chi2 += Math.Pow(c3plus - exp_c3plus, 2) / exp_c3plus;

            // df = 3 (4 категории - 1)
            return 1.0 - ChiSquaredCDF(chi2, 3);
        }
        public double CollisionTestOnHashStream(
            Func<byte[], byte[]> hashFunction,
            int requiredBits = 4_000_000,
            int t = 2,
            int k = 200000)
        {
            if (hashFunction == null) throw new ArgumentNullException(nameof(hashFunction));

            string bits = GenerateHashStream(hashFunction, requiredBits);
            return CollisionTest(bits, t, k);
        }

        #endregion

        #region Gap Test
        public double GapTest(string bits, int t = 20, int n = 500_000)
        {
            if (bits.Length < n * t) return 0.5;

            int pos = 0;
            var u = new double[n];
            for (int i = 0; i < n; i++)
            {
                int v = 0;
                for (int j = 0; j < t; j++)
                    v = (v << 1) | (bits[pos++] - '0');
                u[i] = v / (double)(1 << t);
            }

            const double Alpha = 0.0;
            const double Beta = 0.2;
            const int MaxGap = 10;

            long[] observed = new long[MaxGap + 1]; // 0..9 + ≥10

            int currentGap = 0;
            bool waitingForSuccess = true; // изначально ждём первый успех

            foreach (double x in u)
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

            if (totalGaps < 10) return 0.5;

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

        public double GapTestOnHashStream(
            Func<byte[], byte[]> hashFunction,
            int requiredBits = 20 * 500000,
            int t = 20,
            int n = 500000)
        {
            if (hashFunction == null)
                throw new ArgumentNullException(nameof(hashFunction));

            if (requiredBits < t * n)
                requiredBits = t * n;

            string bits = GenerateHashStream(hashFunction, requiredBits);
            return GapTest(bits, t, n);
        }
        #endregion

        #region Autocorrelation Test
        public double AutocorrelationTest(string bits, int d = 1)
        {
            if (bits == null || bits.Length <= d + 1)
                return 0.0;

            int n = bits.Length;
            int m = n - d; // количество сравнения X[i] и X[i+d]

            long A = 0;

            // Считаем XOR биты
            for (int i = 0; i < m; i++)
            {
                int b1 = bits[i] == '1' ? 1 : 0;
                int b2 = bits[i + d] == '1' ? 1 : 0;

                if ((b1 ^ b2) == 1)
                    A++;
            }

            // Ожидание и дисперсия
            double expected = m / 2.0;
            double variance = m / 4.0;
            if (variance <= 0) return 0.0;

            double z = (A - expected) / Math.Sqrt(variance);

            // p-value = erfc(|z|/sqrt(2))
            double p = Erfc(Math.Abs(z) / Math.Sqrt(2));

            if (double.IsNaN(p) || double.IsInfinity(p))
                return 0.0;

            return Math.Max(0.0, Math.Min(1.0, p));
        }
        public double AutocorrelationTestOnHashStream(
            Func<byte[], byte[]> hashFunction,
            int requiredBits = 1_000_000,
            int d = 1)
        {
            if (hashFunction == null)
                throw new ArgumentNullException(nameof(hashFunction));

            if (requiredBits <= d + 10)
                requiredBits = d + 10;

            string bits = GenerateHashStream(hashFunction, requiredBits);
            return AutocorrelationTest(bits, d);
        }
        #endregion

        #region Spectral Test
        //public double SpectralTest(string bits)
        //{
        //    int n = bits.Length;
        //    if (n < 4096) return 0.5;

        //    int N = 1;
        //    while (N < n) N <<= 1;

        //    var x = new Complex[N];
        //    for (int i = 0; i < n; i++)
        //        x[i] = bits[i] == '1' ? 1.0 : -1.0;

        //    FFT(x);

        //    int N2 = N / 2;
        //    double T = Math.Log(20.0);  // ← ВОТ ЭТО — ГЛАВНАЯ ИСПРАВЛЕННАЯ СТРОКА!!!

        //    int countBelow = 0;
        //    for (int k = 1; k < N2; k++)
        //    {
        //        double S = (x[k].Real * x[k].Real + x[k].Imaginary * x[k].Imaginary) / N;
        //        if (S < T)
        //            countBelow++;
        //    }

        //    double expected = 0.95 * (N2 - 1);
        //    double variance = N * 0.95 * 0.05;
        //    double d = (countBelow - expected) / Math.Sqrt(variance);
        //    double pValue = Math.Exp(-0.5 * d * d);

        //    return Math.Clamp(pValue, 0.0, 1.0);
        //}
        public double SpectralTest(string bits)
        {
            int n = bits.Length;
            if (n < 4096) return 0.5;

            int N = 1;
            while (N < n) N <<= 1;

            var x = new Complex[N];
            for (int i = 0; i < n; i++)
                x[i] = bits[i] == '1' ? 1.0 : -1.0;

            FFT(x);

            int N2 = N / 2;
            double T = Math.Log(20.0);  // ≈ 2.9957

            int countBelow = 0;
            for (int k = 1; k < N2; k++)
            {
                double S = (x[k].Real * x[k].Real + x[k].Imaginary * x[k].Imaginary) / N;
                if (S < T)
                    countBelow++;
            }

            double expected = 0.95 * (N2 - 1);
            double variance = 0.95 * 0.05 * N / 2.0;  // ← ЭТО КРИТИЧЕСКИ ВАЖНО: / 2.0
            double d = (countBelow - expected) / Math.Sqrt(variance);

            double pValue = Math.Exp(-d * d);  // ← без 0.5!

            return Math.Clamp(pValue, 0.0, 1.0);
        }
        public double SpectralTestOnHashStream(
            Func<byte[], byte[]> hashFunction,
            int requiredBits = 1_000_000)
        {
            if (hashFunction == null)
                throw new ArgumentNullException(nameof(hashFunction));

            if (requiredBits < 4096)
                requiredBits = 4096;

            string bits = GenerateHashStream(hashFunction, requiredBits);
            return SpectralTest(bits);
        }

        #endregion

        #region Auxiliary calculation
        private string GenerateHashStream(Func<byte[], byte[]> hashFunction, int requiredBits)
        {
            var sb = new StringBuilder(requiredBits);
            byte[] counter = new byte[8];

            while (sb.Length < requiredBits)
            {
                byte[] h = hashFunction(counter);
                foreach (byte b in h)
                {
                    for (int bit = 7; bit >= 0; bit--)
                    {
                        sb.Append(((b >> bit) & 1) == 1 ? '1' : '0');
                        if (sb.Length >= requiredBits)
                            return sb.ToString();
                    }
                }

                for (int i = 0; i < 8; i++)
                    if (++counter[i] != 0) break;
            }

            return sb.ToString();
        }

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
            int levels = 0;
            for (int temp = n; temp > 1; temp >>= 1) levels++;

            // Bit reversal
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                while (j >= bit)
                {
                    j -= bit;
                    bit >>= 1;
                }
                j += bit;
                if (i < j)
                {
                    Complex temp = data[i];
                    data[i] = data[j];
                    data[j] = temp;
                }
            }
            for (int size = 2; size <= n; size *= 2)
            {
                double angle = 2 * Math.PI / size;
                Complex wlen = new Complex(Math.Cos(angle), Math.Sin(angle));

                for (int i = 0; i < n; i += size)
                {
                    Complex w = Complex.One;
                    for (int j = 0; j < size / 2; j++)
                    {
                        Complex u = data[i + j];
                        Complex v = data[i + j + size / 2] * w;

                        data[i + j] = u + v;
                        data[i + j + size / 2] = u - v;

                        w *= wlen;
                    }
                }
            }
        }
        #endregion
    }
}