using Complex_for_analyzing_hash_functions.Interfaces;
using Complex_for_analyzing_hash_functions.Models;
using System;

namespace Complex_for_analyzing_hash_functions.Services
{
    public class BitIndependenceService : IBitIndependenceService
    {
        public BicResult ComputeBIC(
            Func<byte[], byte[]> hashFunction,
            int inputBitLength = 512,
            int rounds = 1,
            int experimentsPerBit = 300)
        {
            if (hashFunction == null)
                throw new ArgumentNullException(nameof(hashFunction));

            // Определяем размер выхода в битах
            byte[] testHash = hashFunction(new byte[(inputBitLength + 7) / 8]);
            int outputBits = testHash.Length * 8;

            // Накопление корреляций
            double[,] correlationSum = new double[outputBits, outputBits];

            var rnd = new Random(12345);

            // === Основной цикл ===
            for (int flippedInputBit = 0; flippedInputBit < inputBitLength; flippedInputBit++)
            {
                double[,] delta = new double[experimentsPerBit, outputBits];

                int anyDiff = 0;

                for (int exp = 0; exp < experimentsPerBit; exp++)
                {
                    // каждый эксперимент — новый случайный вход
                    byte[] baseInput = new byte[(inputBitLength + 7) / 8];
                    rnd.NextBytes(baseInput);

                    byte[] baseHash = hashFunction(baseInput);

                    byte[] flippedInput = (byte[])baseInput.Clone();

                    // LSB-first флип входного бита
                    int byteIdx = flippedInputBit / 8;
                    int bitIdx = flippedInputBit % 8;

                    flippedInput[byteIdx] ^= (byte)(1 << bitIdx);

                    byte[] flippedHash = hashFunction(flippedInput);

                    bool anyChanged = false;

                    // Δf_j = 1 если бит изменился, 0 если нет
                    for (int outBit = 0; outBit < outputBits; outBit++)
                    {
                        int outByte = outBit / 8;
                        int outBitIdx = outBit % 8;

                        int orig = (baseHash[outByte] >> outBitIdx) & 1;
                        int flip = (flippedHash[outByte] >> outBitIdx) & 1;

                        int diff = (flip != orig) ? 1 : 0;
                        delta[exp, outBit] = diff;

                        if (diff == 1) anyChanged = true;
                    }

                    if (anyChanged) anyDiff++;
                }

                if (anyDiff == 0)
                {
                    Console.WriteLine($"[BIC WARNING] Input bit {flippedInputBit} never changes output bits.");
                }

                // накапливаем корреляции
                for (int i = 0; i < outputBits; i++)
                {
                    for (int j = i; j < outputBits; j++)
                    {
                        double r = PearsonCorrelation(delta, i, j, experimentsPerBit);
                        correlationSum[i, j] += r;
                        if (i != j) correlationSum[j, i] += r;
                    }
                }
            }

            // усреднение по входным битам
            for (int i = 0; i < outputBits; i++)
                for (int j = 0; j < outputBits; j++)
                    correlationSum[i, j] /= inputBitLength;

            // === Финальные метрики ===
            double sum = 0;
            double sumSq = 0;
            double maxAbs = 0;
            double minCorr = double.MaxValue;

            int total = outputBits * outputBits;

            for (int i = 0; i < outputBits; i++)
            {
                for (int j = 0; j < outputBits; j++)
                {
                    if (i == j) continue;   // исключаем диагональ

                    double v = correlationSum[i, j];

                    sum += v;
                    sumSq += v * v;

                    double abs = Math.Abs(v);
                    if (abs > maxAbs) maxAbs = abs;
                    if (v < minCorr) minCorr = v;

                    total++;
                }
            }


            double mean = sum / total;
            double std = Math.Sqrt(Math.Max(0.0, sumSq / total - mean * mean));

            return new BicResult
            {
                MeanCorrelation = mean,
                StdCorrelation = std,
                MaxCorrelationAbs = Math.Abs(maxAbs),
                MinCorrelation = Math.Abs(minCorr),
                Notes = $"BIC: experimentsPerBit={experimentsPerBit}, inputBits={inputBitLength}, outputBits={outputBits}. LSB-first bit ordering used."
            };
        }

        private double PearsonCorrelation(double[,] data, int colX, int colY, int rows)
        {
            double sumX = 0, sumY = 0;

            for (int r = 0; r < rows; r++)
            {
                sumX += data[r, colX];
                sumY += data[r, colY];
            }

            double meanX = sumX / rows;
            double meanY = sumY / rows;

            double cov = 0, varX = 0, varY = 0;

            for (int r = 0; r < rows; r++)
            {
                double dx = data[r, colX] - meanX;
                double dy = data[r, colY] - meanY;
                cov += dx * dy;
                varX += dx * dx;
                varY += dy * dy;
            }

            if (varX <= 0 || varY <= 0)
                return 0.0;

            return cov / Math.Sqrt(varX * varY);
        }
    }
}
