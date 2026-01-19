namespace Complex_for_analyzing_hash_functions.Models
{
    public class RoundStatisticAggregatedPoint
    {
        public int Rounds { get; set; }

        public double? MonobitMean { get; set; }
        public double MonobitStd { get; set; }
        public double MonobitCi { get; set; }

        public double? MeanFlipRateMean { get; set; }
        public double MeanFlipRateStd { get; set; }
        public double MeanFlipRateCi { get; set; }

        public double? BicMaxCorrelationMean { get; set; }
        public double BicMaxCorrelationStd { get; set; }
        public double BicMaxCorrelationCi { get; set; }
    }
}
