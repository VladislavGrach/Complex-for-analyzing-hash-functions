using System;
using Complex_for_analyzing_hash_functions.Interfaces;

namespace Complex_for_analyzing_hash_functions.Services
{
    public class Blake2bHash : IHashFunction
    {
        private static readonly ulong[] IV = new ulong[]
        {
            0x6A09E667F3BCC908UL, 0xBB67AE8584CAA73BUL,
            0x3C6EF372FE94F82BUL, 0xA54FF53A5F1D36F1UL,
            0x510E527FADE682D1UL, 0x9B05688C2B3E6C1FUL,
            0x1F83D9ABFB41BD6BUL, 0x5BE0CD19137E2179UL
        };

        // Sigma: permutations (12 rounds x 16)
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
            {10, 2, 8, 4, 7, 6, 1, 5,15,11, 9,14, 3,12,13, 0 },
            { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9,10,11,12,13,14,15 },
            {14,10, 4, 8, 9,15,13, 6, 1,12, 0, 2,11, 7, 5, 3 }
        };

        private readonly int digestSizeBytes;

        public Blake2bHash(int digestSizeBits = 256)
        {
            if (digestSizeBits % 8 != 0) throw new ArgumentException("digestSizeBits must be multiple of 8");
            digestSizeBytes = digestSizeBits / 8;
            if (digestSizeBytes <= 0 || digestSizeBytes > 64) throw new ArgumentOutOfRangeException(nameof(digestSizeBits));
        }

        public byte[] ComputeHash(byte[] input, int rounds = 12)
        {
            if (rounds < 1 || rounds > 12) throw new ArgumentOutOfRangeException(nameof(rounds), "BLAKE2b supports 1..12 rounds.");

            // initialize state h = IV
            ulong[] h = new ulong[8];
            Array.Copy(IV, h, 8);

            // parameter block (unkeyed): simplest common initialization:
            // h[0] ^= 0x01010000 ^ digest_length
            // (this mirrors many reference implementations for unkeyed mode)
            h[0] ^= 0x01010000UL ^ (ulong)digestSizeBytes;

            int blockSize = 128; // BLAKE2b block = 128 bytes
            int offset = 0;
            int remaining = input?.Length ?? 0;
            ulong t0 = 0; // low 64-bit of byte counter
            ulong t1 = 0; // high 64-bit of byte counter

            // process full blocks
            while (remaining > blockSize)
            {
                // increase counter by blockSize
                t0 += (ulong)blockSize;
                if (t0 < (ulong)blockSize) t1++; // carry

                var block = new byte[blockSize];
                Array.Copy(input, offset, block, 0, blockSize);
                Compress(h, block, t0, t1, false, rounds);

                offset += blockSize;
                remaining -= blockSize;
            }

            // last block (remaining <= blockSize)
            byte[] last = new byte[blockSize];
            if (remaining > 0)
                Array.Copy(input, offset, last, 0, remaining);

            // counter add remaining
            t0 += (ulong)remaining;
            if (t0 < (ulong)remaining) t1++;

            Compress(h, last, t0, t1, true, rounds);

            // produce digest: little-endian concatenation of h[0..]
            byte[] outBuf = new byte[digestSizeBytes];
            int toCopy = digestSizeBytes;
            int dst = 0;
            for (int i = 0; i < 8 && toCopy > 0; i++)
            {
                byte[] w = BitConverter.GetBytes(h[i]); // little-endian
                int cnt = Math.Min(8, toCopy);
                Array.Copy(w, 0, outBuf, dst, cnt);
                dst += cnt;
                toCopy -= cnt;
            }

            return outBuf;
        }

        // Compression function: modifies chaining value h in place
        private static void Compress(ulong[] h, byte[] block, ulong t0, ulong t1, bool isLast, int rounds)
        {
            // m[0..15] from block (little-endian)
            ulong[] m = new ulong[16];
            for (int i = 0; i < 16; i++)
            {
                int idx = i * 8;
                if (idx + 8 <= block.Length)
                    m[i] = BitConverter.ToUInt64(block, idx);
                else
                {
                    // if last partial block, pad with zeros (we already made block 128 bytes)
                    m[i] = 0UL;
                }
            }

            // v = h[0..7] || IV[0..7]
            ulong[] v = new ulong[16];
            Array.Copy(h, 0, v, 0, 8);
            Array.Copy(IV, 0, v, 8, 8);

            // counters
            v[12] ^= t0;
            v[13] ^= t1;

            // final block flag
            if (isLast)
            {
                v[14] = ~v[14];
            }

            // rounds: per-round 8 G calls (4 column + 4 diagonal)
            for (int r = 0; r < rounds; r++)
            {
                int ri = r % 12;
                G(v, 0, 4, 8, 12, m[sigma[ri, 0]], m[sigma[ri, 1]]);
                G(v, 1, 5, 9, 13, m[sigma[ri, 2]], m[sigma[ri, 3]]);
                G(v, 2, 6, 10, 14, m[sigma[ri, 4]], m[sigma[ri, 5]]);
                G(v, 3, 7, 11, 15, m[sigma[ri, 6]], m[sigma[ri, 7]]);

                G(v, 0, 5, 10, 15, m[sigma[ri, 8]], m[sigma[ri, 9]]);
                G(v, 1, 6, 11, 12, m[sigma[ri, 10]], m[sigma[ri, 11]]);
                G(v, 2, 7, 8, 13, m[sigma[ri, 12]], m[sigma[ri, 13]]);
                G(v, 3, 4, 9, 14, m[sigma[ri, 14]], m[sigma[ri, 15]]);
            }

            // update chaining value: h[i] ^= v[i] ^ v[i+8]
            for (int i = 0; i < 8; i++)
                h[i] ^= v[i] ^ v[i + 8];
        }

        // G mixing function for BLAKE2b (64-bit words)
        private static void G(ulong[] v, int a, int b, int c, int d, ulong x, ulong y)
        {
            v[a] = unchecked(v[a] + v[b] + x);
            v[d] = RotR64(v[d] ^ v[a], 32);
            v[c] = unchecked(v[c] + v[d]);
            v[b] = RotR64(v[b] ^ v[c], 24);
            v[a] = unchecked(v[a] + v[b] + y);
            v[d] = RotR64(v[d] ^ v[a], 16);
            v[c] = unchecked(v[c] + v[d]);
            v[b] = RotR64(v[b] ^ v[c], 63);
        }

        private static ulong RotR64(ulong x, int r)
        {
            return (x >> r) | (x << (64 - r));
        }
    }
}
