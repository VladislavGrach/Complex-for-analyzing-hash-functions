namespace Complex_for_analyzing_hash_functions.Services
{
    public interface IHashFunction
    {
        byte[] ComputeHash(byte[] input, int rounds = 0);
    }
}
