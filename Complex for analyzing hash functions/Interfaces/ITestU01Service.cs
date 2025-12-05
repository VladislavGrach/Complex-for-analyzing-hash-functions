namespace Complex_for_analyzing_hash_functions.Interfaces
{
    public interface ITestU01Service
    {
        double CollisionTest(string bits, int t = 20, int n = 500_000);
        double CollisionTestOnHashStream(
            Func<byte[], byte[]> hashFunction,
            int requiredBits = 20 * 500_000,
            int t = 20,
            int n = 500_000);

        double GapTest(string bits, int t = 20, int n = 500000);
        double GapTestOnHashStream(
            Func<byte[], byte[]> hashFunction,
            int requiredBits = 20 * 500000,
            int t = 20,
            int n = 500000);

        double AutocorrelationTest(string bits, int d = 1);
        double AutocorrelationTestOnHashStream(
            Func<byte[], byte[]> hashFunction,
            int requiredBits = 1_000_000,
            int d = 1);

        double SpectralTest(string bits);
        double SpectralTestOnHashStream(
            Func<byte[], byte[]> hashFunction,
            int requiredBits = 1_000_000);

        double HammingWeightTest(string bits, int L = 32, int K = 256, int S = 10000);
        double HammingWeightTestOnHashStream(
            Func<byte[], byte[]> hashFunction,
            int L = 32,
            int K = 256,
            int S = 10000);

        double SerialTest(string bits, int t = 2, int k = 2, int n = 500000);
        double SerialTestOnHashStream(
            Func<byte[], byte[]> hashFunction,
            int t = 2,
            int k = 2,
            int n = 500000);

        double MultinomialTest(string bits, int t = 2, int k = 3, int n = 200000);
        double MultinomialTestOnHashStream(
            Func<byte[], byte[]> hashFunction,
            int t = 2,
            int k = 3,
            int n = 200000);

        double ClosePairsTest(string bits, int t = 20, int n = 200000, int k = 256);
        double ClosePairsTestOnHashStream(
            Func<byte[], byte[]> hashFunction,
            int requiredBits = 20 * 200000,
            int t = 20,
            int n = 200000,
            int k = 256);

        double CouponCollectorTest(string bits, int t = 5, int S = 200);
        double CouponCollectorTestOnHashStream(
            Func<byte[], byte[]> hashFunction,
            int t = 5,
            int S = 200,
            int requiredBits = 2_000_000);

    }
}
