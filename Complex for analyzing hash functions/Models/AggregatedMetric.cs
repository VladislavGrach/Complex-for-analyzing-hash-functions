public class AggregatedMetric
{
    public double Mean { get; set; }
    public double Std { get; set; }
    public double Ci { get; set; }
    public double Lower { get; set; } // нижняя граница (p-шкала)
    public double Upper { get; set; } // верхняя граница (p-шкала)
}
