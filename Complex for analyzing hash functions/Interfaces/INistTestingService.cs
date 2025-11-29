namespace Complex_for_analyzing_hash_functions.Interfaces
{
    public interface INistTestingService
    {
        double MonobitTest(string bits);
        double FrequencyTestWithinBlock(string bits, int blockSize = 128);
        double RunsTest(string bits);
        double LongestRunOfOnesTest(string bits, int blockSize = 128);
        double BinaryMatrixRankTest(string bits);
        double DiscreteFourierTransformTest(string bits);
        double NonOverlappingTemplateMatchingTest(string bits, string template = "000111");
        double OverlappingTemplateMatchingTest(string bits, int m = 10);
        double MaurersUniversalTest(string bits);
        double LempelZivCompressionTest(string bits);
        double LinearComplexityTest(string bits, int M = 32);
        double SerialTest(string bits, int m = 2);
        double ApproximateEntropyTest(string bits, int m = 2);
        double CusumTest(string bits);
        double RandomExcursionsTest(string bits);
        double RandomExcursionsVariantTest(string bits);
        double RandomExcursionsTestOnHashStream(Func<byte[], byte[]> hashFunction, int requiredBits = 1_500_000);
        double RandomExcursionsVariantTestOnHashStream(Func<byte[], byte[]> hashFunction, int requiredBits = 1_500_000);
        // New methods for generating hash-stream and running Maurer on it
        string GenerateHashStream(Func<byte[], byte[]> hashFunction, int requiredBits);
        double MaurersUniversalTestOnHashStream(Func<byte[], byte[]> hashFunction, int requiredBits = 1_500_000);
        double LempelZivCompressionTestOnHashStream(
            Func<byte[], byte[]> hashFunction,
            int requiredBits = 1_500_000
        );

    }
}
