namespace Complex_for_analyzing_hash_functions.Models
{
    public class HashTestParameters
    {
        public int TestsCount { get; set; }
        public int InputSizeBytes { get; set; }
        public int Rounds { get; set; }
        public string Algorithm { get; set; }
    }
}
