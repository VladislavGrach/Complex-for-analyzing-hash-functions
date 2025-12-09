using Complex_for_analyzing_hash_functions.Interfaces;
using Complex_for_analyzing_hash_functions.Services;

namespace Complex_for_analyzing_hash_functions.Helpers
{
    public static class AlgorithmSelector
    {
        public static IHashFunction Create(string name)
        {
            return name switch
            {
                "Keccak" => new KeccakHash(256),
                "Blake" => new Blake256Hash(),
                "Blake2s" => new Blake2sHash(),
                //"Blake2b" => new Blake2bHash(),
                "Blake3" => new Blake3Hash(),
                _ => throw new Exception($"Unknown hash algorithm: {name}")
            };
        }
    }
}
