using System;
using System.Linq;

namespace Complex_for_analyzing_hash_functions.Statistics
{
    public static class StatisticsAnalyzer
    {
        // Частотный критерий Пирсона
        public static double ChiSquareBits(string bits)
        {
            int n = bits.Length;
            if (n == 0) return 0;

            int ones = bits.Count(c => c == '1');
            int zeros = n - ones;

            double expected = n / 2.0;

            double chi2 =
                Math.Pow(zeros - expected, 2) / expected +
                Math.Pow(ones - expected, 2) / expected;

            return chi2;
        }

        // Энтропия Шеннона
        public static double ShannonEntropyBits(string bits)
        {
            int n = bits.Length;
            if (n == 0) return 0;

            int ones = bits.Count(c => c == '1');
            int zeros = n - ones;

            double p0 = (double)zeros / n;
            double p1 = (double)ones / n;

            double entropy = 0;

            if (p0 > 0)
                entropy -= p0 * Math.Log(p0, 2);

            if (p1 > 0)
                entropy -= p1 * Math.Log(p1, 2);

            return entropy;
        }

        // Автокорреляция
        public static double AutocorrelationBits(string bits, int lag)
        {
            int n = bits.Length;
            if (n <= lag) return 0;

            double mean = bits.Count(c => c == '1') / (double)n;

            double sum = 0;

            for (int i = 0; i < n - lag; i++)
            {
                double xi = bits[i] == '1' ? 1 : 0;
                double xk = bits[i + lag] == '1' ? 1 : 0;

                sum += (xi - mean) * (xk - mean);
            }

            return sum / (n - lag);
        }

        // Взаимная информация
        public static double MutualInformationBits(string bits, int lag)
        {
            int n = bits.Length;
            if (n <= lag) return 0;

            int[,] joint = new int[2, 2];
            int[] freq = new int[2];

            for (int i = 0; i < n - lag; i++)
            {
                int x = bits[i] == '1' ? 1 : 0;
                int y = bits[i + lag] == '1' ? 1 : 0;

                joint[x, y]++;
                freq[x]++;
            }

            double total = n - lag;
            double mi = 0;

            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    if (joint[x, y] == 0) continue;

                    double pxy = joint[x, y] / total;
                    double px = freq[x] / total;
                    double py = (joint[0, y] + joint[1, y]) / total;

                    mi += pxy * Math.Log(pxy / (px * py), 2);
                }
            }

            return mi;
        }
    }
}