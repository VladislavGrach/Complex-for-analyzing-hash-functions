using Complex_for_analyzing_hash_functions.Models;
using Microsoft.AspNetCore.Http;

namespace Complex_for_analyzing_hash_functions.Interfaces
{
    public interface IBitIndependenceService
    {
        BicResult ComputeBIC(
            Func<byte[], byte[]> hashFunction,
            int inputBitLength,
            int rounds = 1,
            int experimentsPerBit = 200);
    }
}
