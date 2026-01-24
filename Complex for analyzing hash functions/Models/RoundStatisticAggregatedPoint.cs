namespace Complex_for_analyzing_hash_functions.Models
{
    public class RoundStatisticAggregatedPoint
    {
        public int Rounds { get; set; }

        // Ключ = имя теста (Monobit, Runs, Serial, SAC, BIC, ...)
        public Dictionary<string, AggregatedMetric> Metrics { get; set; }
            = new Dictionary<string, AggregatedMetric>();
    }
}
