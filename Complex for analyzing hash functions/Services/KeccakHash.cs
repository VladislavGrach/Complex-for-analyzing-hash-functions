using Complex_for_analyzing_hash_functions.Interfaces;
using System;

namespace Complex_for_analyzing_hash_functions.Services
{
    public class KeccakHash : IHashFunction
    {
        private static readonly ulong[] RoundConstants = new ulong[]
        {
            0x0000000000000001UL,0x0000000000008082UL,0x800000000000808aUL,0x8000000080008000UL,
            0x000000000000808bUL,0x0000000080000001UL,0x8000000080008081UL,0x8000000000008009UL,
            0x000000000000008aUL,0x0000000000000088UL,0x0000000080008009UL,0x000000008000000aUL,
            0x000000008000808bUL,0x800000000000008bUL,0x8000000000008089UL,0x8000000000008003UL,
            0x8000000000008002UL,0x8000000000000080UL,0x000000000000800aUL,0x800000008000000aUL,
            0x8000000080008081UL,0x8000000000008080UL,0x0000000080000001UL,0x8000000080008008UL
        };

        private static readonly int[] RhoOffsets = {
             0,  1, 62, 28, 27,
            36, 44,  6, 55, 20,
             3, 10, 43, 25, 39,
            41, 45, 15, 21,  8,
            18,  2, 61, 56, 14
        };

        private readonly int rate;
        private readonly int capacity;
        private readonly int digestSize;

        public KeccakHash(int digestSizeBits = 256)
        {
            digestSize = digestSizeBits / 8;
            capacity = 512;
            rate = (1600 - capacity) / 8; // 1088 bits = 136 bytes
        }

        public byte[] ComputeHash(byte[] input, int rounds = 24)
        {
            if (rounds < 1 || rounds > 24)
            {
                throw new ArgumentOutOfRangeException(nameof(rounds));
            }

            var state = new ulong[25];
            byte[] block = new byte[rate];
            int offset = 0;

            while (offset + rate <= input.Length)
            {
                Array.Copy(input, offset, block, 0, rate);
                AbsorbBlock(state, block);
                KeccakF1600(state, rounds);
                offset += rate;
            }

            int rem = input.Length - offset;
            Array.Clear(block, 0, block.Length);
            if (rem > 0) Array.Copy(input, offset, block, 0, rem);

            block[rem] = 0x06;
            block[rate - 1] |= 0x80;

            AbsorbBlock(state, block);
            KeccakF1600(state, rounds);

            return Squeeze(state, rounds);
        }

        private void AbsorbBlock(ulong[] state, byte[] block)
        {
            for (int i = 0; i < rate / 8; i++)
            {
                ulong lane = BitConverter.ToUInt64(block, i * 8);
                state[i] ^= lane;
            }
        }

        private byte[] Squeeze(ulong[] state, int rounds)
        {
            byte[] hash = new byte[digestSize];
            int outOffset = 0;
            int rateLanes = rate / 8;

            while (outOffset < digestSize)
            {
                for (int i = 0; i < rateLanes && outOffset < digestSize; i++)
                {
                    byte[] laneBytes = BitConverter.GetBytes(state[i]);
                    int toCopy = Math.Min(8, digestSize - outOffset);
                    Array.Copy(laneBytes, 0, hash, outOffset, toCopy);
                    outOffset += toCopy;
                }
                if (outOffset < digestSize)
                    KeccakF1600(state, rounds);
            }

            return hash;
        }

        private void KeccakF1600(ulong[] A, int rounds)
        {
            ulong[] C = new ulong[5];
            ulong[] D = new ulong[5];

            for (int round = 0; round < rounds; round++)
            {
                for (int x = 0; x < 5; x++)
                    C[x] = A[x] ^ A[x + 5] ^ A[x + 10] ^ A[x + 15] ^ A[x + 20];

                for (int x = 0; x < 5; x++)
                    D[x] = C[(x + 4) % 5] ^ Rol(C[(x + 1) % 5], 1);

                for (int x = 0; x < 5; x++)
                    for (int y = 0; y < 5; y++)
                        A[x + 5 * y] ^= D[x];

                ulong[] B = new ulong[25];
                for (int x = 0; x < 5; x++)
                    for (int y = 0; y < 5; y++)
                        B[y + 5 * ((2 * x + 3 * y) % 5)] = Rol(A[x + 5 * y], RhoOffsets[x + 5 * y]);

                for (int x = 0; x < 5; x++)
                    for (int y = 0; y < 5; y++)
                        A[x + 5 * y] = B[x + 5 * y] ^ ((~B[((x + 1) % 5) + 5 * y]) & B[((x + 2) % 5) + 5 * y]);

                A[0] ^= RoundConstants[round];
            }
        }

        private static ulong Rol(ulong value, int offset)
        {
            offset &= 63;
            return (value << offset) | (value >> (64 - offset));
        }
    }
}
