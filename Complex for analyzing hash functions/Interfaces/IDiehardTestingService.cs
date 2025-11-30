namespace Complex_for_analyzing_hash_functions.Interfaces
{
    public interface IDiehardTestingService : INistTestingService
    {
        double BirthdaySpacingsTest(string bits);
        double CountOnesTest(string bits);
        double RanksOfMatricesTest(string bits, Func<byte[], byte[]> hashFunction = null);
        double OverlappingPermutationsTest(string bits);
        double RunsTest(string bits); // наследуется

        double GcdTest(string bits,
                       Func<byte[], byte[]> hashFunction = null,
                       int requiredWordsDefault = 100_000);
        double SqueezeTest(string bits);
        double CrapsTest(string bits);
        double CrapsTestOnHashStream(
            Func<byte[], byte[]> hashFunction,
            int requiredBits = 6_400_000);

    }
}
