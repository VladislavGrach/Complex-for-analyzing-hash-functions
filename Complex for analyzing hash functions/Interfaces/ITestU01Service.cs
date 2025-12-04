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

    }
}
