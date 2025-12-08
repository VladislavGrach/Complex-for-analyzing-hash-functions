using System;
using System.Buffers.Binary;
using Complex_for_analyzing_hash_functions.Interfaces;

namespace Complex_for_analyzing_hash_functions.Services
{
    public class Blake2sHash : IHashFunction
    {
        // IV constants (32-bit words) — из RFC 7693 / BLAKE2 spec
        private static readonly uint[] IV = new uint[] {
            0x6A09E667u, 0xBB67AE85u, 0x3C6EF372u, 0xA54FF53Au,
            0x510E527Fu, 0x9B05688Cu, 0x1F83D9ABu, 0x5BE0CD19u
        };

        // sigma: message word permutations per round (10 rounds in spec)
        private static readonly byte[,] sigma = new byte[,]
        {
            { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9,10,11,12,13,14,15 },
            {14,10, 4, 8, 9,15,13, 6, 1,12, 0, 2,11, 7, 5, 3 },
            {11, 8,12, 0, 5, 2,15,13,10,14, 3, 6, 7, 1, 9, 4 },
            { 7, 9, 3, 1,13,12,11,14, 2, 6, 5,10, 4, 0,15, 8 },
            { 9, 0, 5, 7, 2, 4,10,15,14, 1,11,12, 6, 8, 3,13 },
            { 2,12, 6,10, 0,11, 8, 3, 4,13, 7, 5,15,14, 1, 9 },
            {12, 5, 1,15,14,13, 4,10, 0, 7, 6, 3, 9, 2, 8,11 },
            {13,11, 7,14,12, 1, 3, 9, 5, 0,15, 4, 8, 6, 2,10 },
            { 6,15,14, 9,11, 3, 0, 8,12, 2,13, 7, 1, 4,10, 5 },
            {10, 2, 8, 4, 7, 6, 1, 5,15,11, 9,14, 3,12,13, 0 }
        };

        // rotate-right helpers
        private static uint Ror(uint x, int r) => (x >> r) | (x << (32 - r));

        // G function (mix)
        private static void G(ref uint a, ref uint b, ref uint c, ref uint d, uint x, uint y, int r0 = 16, int r1 = 12, int r2 = 8, int r3 = 7)
        {
            a = a + b + x;
            d = Ror(d ^ a, r0);
            c = c + d;
            b = Ror(b ^ c, r1);
            a = a + b + y;
            d = Ror(d ^ a, r2);
            c = c + d;
            b = Ror(b ^ c, r3);
        }

        public byte[] ComputeHash(byte[] input, int rounds = 10)
        {
            if (rounds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rounds));
            }

            // BLAKE2s parameters for 32-byte output
            const int outLen = 32;
            const int blockSize = 64; // 64 bytes

            // Parameter block: here we use default key-length=0, no salt, no personalization
            // Param word 0: outlen | (keylen << 8) | (fanout<<16) | (depth<<24)
            uint param0 = (uint)outLen | (0u << 8) | (1u << 16) | (1u << 24);

            // Initialize state h with IV ^ param words
            uint[] h = new uint[8];
            for (int i = 0; i < 8; i++) h[i] = IV[i];
            h[0] ^= param0;

            // Process full blocks
            int offset = 0;
            ulong t0 = 0; // low 64-bit byte counter (we use 64-bit counter split later)
            while (offset + blockSize <= input.Length)
            {
                byte[] block = new byte[blockSize];
                Array.Copy(input, offset, block, 0, blockSize);
                t0 += (ulong)blockSize;
                Compress(h, block, t0, false, rounds);
                offset += blockSize;
            }

            // Final block
            int rem = input.Length - offset;
            byte[] last = new byte[blockSize];
            if (rem > 0) Array.Copy(input, offset, last, 0, rem);
            t0 += (ulong)rem;
            // Finalize flag = true
            Compress(h, last, t0, true, rounds);

            // Produce digest (little-endian)
            byte[] digest = new byte[outLen];
            for (int i = 0; i < 8 && i * 4 < outLen; i++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(digest.AsSpan(i * 4), h[i]);
            }
            return digest;
        }

        // Compression function: updates h (state) in place
        // t: total bytes compressed so far (as ulong)
        // last: finalization flag
        private static void Compress(uint[] h, byte[] block, ulong t, bool last, int rounds)
        {
            // m[0..15] little-endian 32-bit words
            uint[] m = new uint[16];
            for (int i = 0; i < 16; i++)
            {
                m[i] = BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(i * 4));
            }

            // v[0..15] initialize
            uint[] v = new uint[16];
            for (int i = 0; i < 8; i++) v[i] = h[i];
            for (int i = 0; i < 8; i++) v[i + 8] = IV[i];

            // counter t: split into two 32-bit words (low, high)
            v[12] ^= (uint)(t & 0xFFFFFFFFu);
            v[13] ^= (uint)(t >> 32);

            if (last)
            {
                // invert all bits of v[14] (finalization flag)
                v[14] = ~v[14];
            }

            // Rounds: apply G in schedule
            int roundsToUse = Math.Max(1, rounds);
            for (int r = 0; r < roundsToUse; r++)
            {
                int s = r % 10; // sigma schedule repeats every 10
                // column step
                G(ref v[0], ref v[4], ref v[8], ref v[12], m[sigma[s, 0]], m[sigma[s, 1]]);
                G(ref v[1], ref v[5], ref v[9], ref v[13], m[sigma[s, 2]], m[sigma[s, 3]]);
                G(ref v[2], ref v[6], ref v[10], ref v[14], m[sigma[s, 4]], m[sigma[s, 5]]);
                G(ref v[3], ref v[7], ref v[11], ref v[15], m[sigma[s, 6]], m[sigma[s, 7]]);
                // diagonal step
                G(ref v[0], ref v[5], ref v[10], ref v[15], m[sigma[s, 8]], m[sigma[s, 9]]);
                G(ref v[1], ref v[6], ref v[11], ref v[12], m[sigma[s, 10]], m[sigma[s, 11]]);
                G(ref v[2], ref v[7], ref v[8], ref v[13], m[sigma[s, 12]], m[sigma[s, 13]]);
                G(ref v[3], ref v[4], ref v[9], ref v[14], m[sigma[s, 14]], m[sigma[s, 15]]);
            }

            // Finalization: h[i] = h[i] ^ v[i] ^ v[i+8]
            for (int i = 0; i < 8; i++)
            {
                h[i] ^= v[i] ^ v[i + 8];
            }
        }
    }
}
