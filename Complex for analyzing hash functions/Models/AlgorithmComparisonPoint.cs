namespace Complex_for_analyzing_hash_functions.Models
{
    public class AlgorithmComparisonPoint
    {
        public string Algorithm { get; set; } = "";

        public double SacMean { get; set; }
        public double SacStd { get; set; }

        public double BicMean { get; set; }
        public double BicStd { get; set; }

        public double HammingMean { get; set; }
        public double HammingStd { get; set; }
    }
}