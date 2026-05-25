namespace Complex_for_analyzing_hash_functions.Services
{
    public static class TestLocalization
    {
        public static readonly Dictionary<string, string> Nist = new()
        {
            ["Monobit"] = "Частотный побитовый тест",
            ["FrequencyWithinBlock"] = "Частотный блочный тест",
            ["Runs"] = "Тест на последовательность одинаковых бит",
            ["LongestRunOfOnes"] = "Тест на самую длинную последовательность единиц в блоке",
            ["BinaryMatrixRank"] = "Тест рангов бинарных матриц",
            ["DiscreteFourier"] = "Спектральный тест",
            ["NonOverlappingTemplate"] = "Тест на совпадение непересекающихся шаблонов",
            ["OverlappingTemplate"] = "Тест на совпадение пересекающихся шаблонов",
            ["MaurerUniversal"] = "Универсальный статистический тест Маурера",
            ["LempelZiv"] = "Тест сжатия Лемпеля-Зива",
            ["LinearComplexity"] = "Тест на линейную сложность",
            ["Serial"] = "Тест на периодичность",
            ["ApproximateEntropy"] = "Тест приближённой энтропии",
            ["Cusum"] = "Тест кумулятивных сумм",
            ["RandomExcursions"] = "Тест случайных блужданий",
            ["RandomExcursionsVariant"] = "Модифицированный тест случайных блужданий"
        };

        public static readonly Dictionary<string, string> Diehard = new()
        {
            ["BirthdaySpacings"] = "Тест распределения дней рождения",
            ["CountOnes"] = "Тест подсчёта единиц",
            ["MatrixRanks"] = "Тест рангов матриц",
            ["OverlappingPermutations"] = "Тест на пересекающиеся перестановки",
            ["RunsDiehard"] = "Тест на серии",
            ["Gcd"] = "Тест наибольшего общего делителя",
            ["Squeeze"] = "Тест сжатия",
            ["Craps"] = "Тест игры в крэпс"
        };

        public static readonly Dictionary<string, string> TestU01 = new()
        {
            ["Collision"] = "Тест на коллизии",
            ["Gap"] = "Тест на промежутки",
            ["Autocorrelation"] = "Тест автокорреляции",
            ["Spectral"] = "Спектральный тест",
            ["HammingWeight"] = "Тест веса Хэмминга",
            ["SerialTest"] = "Серийный тест",
            ["MultinomialTest"] = "Мультиномиальный тест",
            ["ClosePairs"] = "Тест близких пар",
            ["CouponCollector"] = "Тест коллекционера купонов"
        };

        public static readonly Dictionary<string, string> SAC = new()
        {
            ["MeanFlipRate"] = "Средняя частота изменений",
            ["StdDevFlipRate"] = "Стандартное отклонение частоты изменений",
            ["MinPValue"] = "Минимальное p-value",
            ["MaxPValue"] = "Максимальное p-value"
        };

        public static readonly Dictionary<string, string> BIC = new()
        {
            ["MeanCorrelation"] = "Средняя корреляция",
            ["StdCorrelation"] = "Стандартное отклонение корреляции",
            ["MaxCorrelationAbs"] = "Максимальная абсолютная корреляция",
            ["MinCorrelationAbs"] = "Минимальная абсолютная корреляция"
        };

        public static readonly Dictionary<string, string> AdditionalStats = new()
        {
            ["ChiSquare"] = "Критерий хи-квадрат",
            ["ShannonEntropy"] = "Энтропия Шеннона",
            ["Autocorrelation"] = "Автокорреляция",
            ["MutualInformation"] = "Взаимная информация"
        };

        // Обратные словари
        public static readonly Dictionary<string, string> NistReverse = Nist.ToDictionary(x => x.Value, x => x.Key);
        public static readonly Dictionary<string, string> DiehardReverse = Diehard.ToDictionary(x => x.Value, x => x.Key);
        public static readonly Dictionary<string, string> TestU01Reverse = TestU01.ToDictionary(x => x.Value, x => x.Key);
        public static readonly Dictionary<string, string> SACReverse = SAC.ToDictionary(x => x.Value, x => x.Key);
        public static readonly Dictionary<string, string> BICReverse = BIC.ToDictionary(x => x.Value, x => x.Key);
        public static readonly Dictionary<string, string> AdditionalStatsReverse = AdditionalStats.ToDictionary(x => x.Value, x => x.Key);

    }
}
