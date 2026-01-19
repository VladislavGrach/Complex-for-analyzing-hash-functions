namespace Complex_for_analyzing_hash_functions.Models
{
    public class AlgorithmComparisonPoint
    {
        public string Algorithm { get; set; } = "";

        public double Mean { get; set; }
        public double Std { get; set; }

        public double Upper => Mean + Std;
        public double Lower => Mean - Std;
    }
}