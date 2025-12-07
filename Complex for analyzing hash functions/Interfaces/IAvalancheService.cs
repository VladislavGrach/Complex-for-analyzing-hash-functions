using Complex_for_analyzing_hash_functions.Models;

namespace Complex_for_analyzing_hash_functions.Interfaces
{
    public enum AvalancheMode
    {
        Sampled,
        Exhaustive
    }

    // IMPORTANT: метод возвращает AvalancheResult (из Models)
    public interface IAvalancheService
    {
        AvalancheResult ComputeSAC(
            Func<byte[], byte[]> hashFunction,
            int inputSizeBytes = 16,
            int trials = 1000,
            AvalancheMode mode = AvalancheMode.Sampled,
            int seed = 0
        );
    }
}