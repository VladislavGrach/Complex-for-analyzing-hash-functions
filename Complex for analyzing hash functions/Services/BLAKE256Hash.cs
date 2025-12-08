using Complex_for_analyzing_hash_functions.Interfaces;
using System;

namespace Complex_for_analyzing_hash_functions.Services
{
    public class Blake256Hash : IHashFunction
    {
        private static readonly uint[] C = new uint[]
        {
            0x243F6A88, 0x85A308D3, 0x13198A2E, 0x03707344,
            0xA4093822, 0x299F31D0, 0x082EFA98, 0xEC4E6C89,
            0x452821E6, 0x38D01377, 0xBE5466CF, 0x34E90C6C,
            0xC0AC29B7, 0xC97C50DD, 0x3F84D5B5, 0xB5470917
        };

        private static readonly byte[,] Sigma = new byte[,]
        {
            {  0, 1,  2, 3,  4, 5,  6, 7,  8, 9, 10,11, 12,13, 14,15 },
            { 14,10,  4, 8,  9,15, 13, 6,  1,12,  0, 2, 11, 7,  5, 3 },
            { 11, 8, 12, 0,  5, 2, 15,13, 10,14,  3, 6,  7, 1,  9, 4 },
            {  7, 9,  3, 1, 13,12, 11,14,  2, 6,  5,10,  4, 0, 15, 8 },
            {  9, 0,  5, 7,  2, 4, 10,15, 14, 1, 11,12,  6, 8,  3,13 },
            {  2,12,  6,10,  0,11,  8, 3,  4,13,  7, 5, 15,14,  1, 9 },
            { 12, 5,  1,15, 14,13,  4,10,  0, 7,  6, 3,  9, 2,  8,11 },
            { 13,11,  7,14, 12, 1,  3, 9,  5, 0, 15, 4,  8, 6,  2,10 },
            {  6,15, 14, 9, 11, 3,  0, 8, 12, 2, 13, 7,  1, 4, 10, 5 },
            { 10, 2,  8, 4,  7, 6,  1, 5, 15,11,  9,14,  3,12, 13, 0 }
        };

        private readonly uint[] h = new uint[8];
        private readonly uint[] s = new uint[4]; // salt
        private ulong t; // counter

        private readonly int digestSizeBytes;

        public Blake256Hash(int digestSizeBits = 256)
        {
            digestSizeBytes = digestSizeBits / 8;
        }

        public byte[] ComputeHash(byte[] input, int rounds)
        {
            if (rounds < 1 || rounds > 14)
            {
                throw new ArgumentOutOfRangeException(nameof(rounds), "BLAKE-256 supports 1..14 rounds.");
            }
            InitializeState();

            int blockSize = 64; // 512-bit block
            int offset = 0;

            while (offset + blockSize <= input.Length)
            {
                ProcessBlock(input, offset, rounds);
                offset += blockSize;
            }

            // last block + padding
            int remaining = input.Length - offset;
            byte[] finalBlock = new byte[blockSize];
            if (remaining > 0)
                Array.Copy(input, offset, finalBlock, 0, remaining);

            // padding
            finalBlock[remaining] = 0x80;

            if (remaining >= 56)
            {
                ProcessBlock(finalBlock, 0, rounds);
                Array.Clear(finalBlock, 0, blockSize);
            }

            ulong bitLen = (ulong)input.Length * 8;
            var lenBytes = BitConverter.GetBytes(bitLen);
            if (BitConverter.IsLittleEndian == false)
                Array.Reverse(lenBytes);

            Array.Copy(lenBytes, 0, finalBlock, 56, 8);

            ProcessBlock(finalBlock, 0, rounds);

            // output
            byte[] output = new byte[digestSizeBytes];
            for (int i = 0; i < digestSizeBytes / 4; i++)
            {
                byte[] tmp = BitConverter.GetBytes(h[i]);
                Array.Copy(tmp, 0, output, i * 4, 4);
            }

            return output;
        }


        private void InitializeState()
        {
            uint[] IV =
            {
                0x6A09E667, 0xBB67AE85,
                0x3C6EF372, 0xA54FF53A,
                0x510E527F, 0x9B05688C,
                0x1F83D9AB, 0x5BE0CD19
            };

            Array.Copy(IV, h, 8);
            Array.Clear(s, 0, 4);
            t = 0;
        }

        private void ProcessBlock(byte[] block, int offset, int rounds)
        {
            uint[] m = new uint[16];
            for (int i = 0; i < 16; i++)
            {
                m[i] = BitConverter.ToUInt32(block, offset + i * 4);
            }

            t += 512;

            uint[] v = new uint[16];
            Array.Copy(h, v, 8);

            v[8] = s[0] ^ 0x243F6A88;
            v[9] = s[1] ^ 0x85A308D3;
            v[10] = s[2] ^ 0x13198A2E;
            v[11] = s[3] ^ 0x03707344;
            v[12] = 0 ^ (uint)(t & 0xFFFFFFFF);
            v[13] = 0 ^ (uint)(t >> 32);
            v[14] = v[12];
            v[15] = v[13];

            for (int r = 0; r < rounds; r++)
            {
                for (int i = 0; i < 8; i++)
                {
                    G(ref v[0], ref v[4], ref v[8], ref v[12], m[Sigma[r % 10, 2 * i]], m[Sigma[r % 10, 2 * i + 1]]);
                    G(ref v[1], ref v[5], ref v[9], ref v[13], m[Sigma[r % 10, 2 * i]], m[Sigma[r % 10, 2 * i + 1]]);
                    G(ref v[2], ref v[6], ref v[10], ref v[14], m[Sigma[r % 10, 2 * i]], m[Sigma[r % 10, 2 * i + 1]]);
                    G(ref v[3], ref v[7], ref v[11], ref v[15], m[Sigma[r % 10, 2 * i]], m[Sigma[r % 10, 2 * i + 1]]);
                }
            }

            for (int i = 0; i < 8; i++)
                h[i] ^= v[i] ^ v[i + 8];
        }

        private static void G(ref uint a, ref uint b, ref uint c, ref uint d, uint x, uint y)
        {
            a = a + b + (x ^ y);
            d ^= a;
            d = RotateRight(d, 16);

            c += d;
            b ^= c;
            b = RotateRight(b, 12);

            a += b + (y ^ x);
            d ^= a;
            d = RotateRight(d, 8);

            c += d;
            b ^= c;
            b = RotateRight(b, 7);
        }

        private static uint RotateRight(uint x, int r)
        {
            return (x >> r) | (x << (32 - r));
        }
    }
}
