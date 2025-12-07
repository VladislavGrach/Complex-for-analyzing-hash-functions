namespace Complex_for_analyzing_hash_functions.Models
{
    public class BicResult
    {
        public double MeanCorrelation { get; set; }
        public double StdCorrelation { get; set; }
        public double MaxCorrelationAbs { get; set; }
        public double MinCorrelation { get; set; }
        public string Notes { get; set; }
    }
}
