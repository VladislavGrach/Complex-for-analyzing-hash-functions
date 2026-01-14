namespace Complex_for_analyzing_hash_functions.Models
{
    public class RoundStatisticAggregatedPoint
    {
        public int Rounds { get; set; }

        public double AvgHammingMean { get; set; }
        public double AvgHammingStd { get; set; }
        public double AvgHammingCi { get; set; }

        public double MeanFlipRateMean { get; set; }
        public double MeanFlipRateStd { get; set; }
        public double MeanFlipRateCi { get; set; }

        public double BicMaxCorrelationMean { get; set; }
        public double BicMaxCorrelationStd { get; set; }
        public double BicMaxCorrelationCi { get; set; }
    }
}
