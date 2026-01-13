namespace Complex_for_analyzing_hash_functions.Models
{
    public class RoundStatisticPoint
    {
        public int Rounds { get; set; }
        public double AvgHamming { get; set; }
        public double MeanFlipRate { get; set; }
        public double StdDevFlipRate { get; set; }
        public double BicMaxCorrelation { get; set; }
        public double BicStdCorrelation { get; set; }
    }
}
