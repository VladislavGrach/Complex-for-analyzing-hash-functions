namespace Complex_for_analyzing_hash_functions.Interfaces
{
    public interface IDiehardTestingService : INistTestingService
    {
        double BirthdaySpacingsTest(string bits);
        double CountOnesTest(string bits);
        double OverlappingPermutationsTest(string bits);
        double RunsTest(string bits); // уже есть в NIST
        double SqueezeTest(string bits);
        double RanksOfMatricesTest(string streamBits);
        double GcdTest(string streamBits);
        double CrapsTest(string streamBits);

    }
}
