namespace Complex_for_analyzing_hash_functions.Interfaces
{
    public interface IHashFunction
    {
        byte[] ComputeHash(byte[] input, int rounds = 0);
    }
}
