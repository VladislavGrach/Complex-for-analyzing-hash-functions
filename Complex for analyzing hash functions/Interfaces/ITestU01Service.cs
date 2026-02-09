namespace Complex_for_analyzing_hash_functions.Interfaces
{
    public interface ITestU01Service
    {
        double CollisionTest(string bits, int t = 20, int n = 500_000);
        double GapTest(string bits, int t = 20, int n = 500000);
        double AutocorrelationTest(string bits, int d = 1);
        double SpectralTest(string bits);
        double HammingWeightTest(string bits);
        double SerialTest(string bits, int t = 2, int k = 2, int n = 500000);
        double MultinomialTest(string bits, int t = 2, int k = 3, int n = 200000);
        double ClosePairsTest(string bits, int t = 20, int n = 200000, int k = 256);
        double CouponCollectorTest(string bits, int t = 8, int S = 10000);
    }
}
