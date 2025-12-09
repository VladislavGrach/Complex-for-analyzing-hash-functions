using Complex_for_analyzing_hash_functions.Interfaces;

namespace Complex_for_analyzing_hash_functions.Services
{
    public class Blake3Hash : IHashFunction
    {
        private const int BLOCK_LEN = 64;   // 64 bytes per compression input
        private const int OUT_LEN = 32;     // we output 32 bytes (256 bits)

        // IV (32-bit words) — стандартное значение
        private static readonly uint[] IV = new uint[]
        {
            0x6A09E667u, 0xBB67AE85u,
            0x3C6EF372u, 0xA54FF53Au,
            0x510E527Fu, 0x9B05688Cu,
            0x1F83D9ABu, 0x5BE0CD19u
        };

        // Используем одну 16-элементную перестановку
        // Это упрощённая поставка индексов; главное — индексы [0..15].
        private static readonly int[] MSG_PERM = new int[]
        {
            0, 1, 2, 3, 4, 5, 6, 7,
            8, 9, 10,11,12,13,14,15
        };

        // Флаги (мы не используем ключевой или derive modes)
        private const uint CHUNK_START = 1u << 0;
        private const uint CHUNK_END = 1u << 1;
        private const uint PARENT = 1u << 2;
        private const uint ROOT = 1u << 3;

        public byte[] ComputeHash(byte[] input, int rounds = 7)
        {
            if (rounds < 1 || rounds > 14)
            {
                throw new ArgumentOutOfRangeException(nameof(rounds), "BLAKE3 supports 1..14 rounds.");
            }

            uint[] chaining = (uint[])IV.Clone();

            // Для линейного режима обработаем данные блоками по 64 байта
            int offset = 0;
            int remaining = input?.Length ?? 0;
            ulong chunkCounter = 0;
            // Флаг chunk-start выставим только при первом блоке чанка
            uint flags = CHUNK_START;

            while (remaining > 0)
            {
                int blockLen = Math.Min(BLOCK_LEN, remaining);
                bool isStart = (offset == 0);
                bool isEnd = (remaining <= BLOCK_LEN);

                // корректируем флаги
                uint blockFlags = flags;
                if (isStart) blockFlags |= CHUNK_START;
                if (isEnd) blockFlags |= CHUNK_END;

                // Compress: chaining ^= compress(chaining, block, blockLen, chunkCounter, flags)
                Compress(chaining, input, offset, (uint)blockLen, chunkCounter, blockFlags, rounds);

                // advance
                offset += blockLen;
                remaining -= blockLen;

                // После первого блока снимаем CHUNK_START (для последующих блоков чанка)
                flags &= ~CHUNK_START;
            }

            // Финальная стадия — получить 32 байта из chaining value
            return Output256(chaining, rounds);
        }

        private static void Compress(uint[] cv, byte[] blockBuffer, int offset, uint blockLen, ulong chunkCounter, uint flags, int rounds)
        {
            // v — рабочее состояние (16 слов)
            uint[] v = new uint[16];
            // m — 16 слов сообщения
            uint[] m = new uint[16];

            // Загрузка m: читаем little-endian слова из blockBuffer (padding нулями при нехватке)
            for (int i = 0; i < 16; i++)
            {
                int idx = offset + i * 4;
                if (blockBuffer != null && idx + 4 <= offset + (int)blockLen)
                {
                    m[i] = BitConverter.ToUInt32(blockBuffer, idx);
                }
                else
                {
                    m[i] = 0u;
                }
            }

            // Инициализация v: первые 8 слов — cv, следующие 8 — IV
            Array.Copy(cv, 0, v, 0, 8);
            Array.Copy(IV, 0, v, 8, 8);

            // XOR counters / length / flags (как в спецификации BLAKE3)
            v[12] ^= (uint)(chunkCounter & 0xFFFFFFFFu);
            v[13] ^= (uint)((chunkCounter >> 32) & 0xFFFFFFFFu);
            v[14] ^= blockLen;
            v[15] ^= flags;

            // Выполняем rounds раз (ChaCha-like G-mix)
            for (int r = 0; r < rounds; r++)
            {
                // Колонные G
                G(v, m, 0, 4, 8, 12, MSG_PERM[0], MSG_PERM[1]);
                G(v, m, 1, 5, 9, 13, MSG_PERM[2], MSG_PERM[3]);
                G(v, m, 2, 6, 10, 14, MSG_PERM[4], MSG_PERM[5]);
                G(v, m, 3, 7, 11, 15, MSG_PERM[6], MSG_PERM[7]);

                // Диагональные G
                G(v, m, 0, 5, 10, 15, MSG_PERM[8], MSG_PERM[9]);
                G(v, m, 1, 6, 11, 12, MSG_PERM[10], MSG_PERM[11]);
                G(v, m, 2, 7, 8, 13, MSG_PERM[12], MSG_PERM[13]);
                G(v, m, 3, 4, 9, 14, MSG_PERM[14], MSG_PERM[15]);
            }

            // Наконец: обновляем chaining value: cv[i] ^= v[i] ^ v[i+8]
            for (int i = 0; i < 8; i++)
            {
                cv[i] ^= v[i] ^ v[i + 8];
            }
        }

        private static void G(uint[] v, uint[] m, int a, int b, int c, int d, int ix, int iy)
        {
            // гарантируем, что m имеет длину >= 16 (мы так и делаем в Compress)
            v[a] = unchecked(v[a] + v[b] + m[ix]);
            v[d] = RotR32(v[d] ^ v[a], 16);
            v[c] = unchecked(v[c] + v[d]);
            v[b] = RotR32(v[b] ^ v[c], 12);
            v[a] = unchecked(v[a] + v[b] + m[iy]);
            v[d] = RotR32(v[d] ^ v[a], 8);
            v[c] = unchecked(v[c] + v[d]);
            v[b] = RotR32(v[b] ^ v[c], 7);
        }

        private static uint RotR32(uint x, int n)
        {
            return (x >> n) | (x << (32 - n));
        }

        private static byte[] Output256(uint[] cv, int rounds)
        {
            // v — рабочее состояние
            uint[] v = new uint[16];
            // mZeros — нулевой message block (16 слов)
            uint[] mZeros = new uint[16];

            // init v = cv || IV
            Array.Copy(cv, 0, v, 0, 8);
            Array.Copy(IV, 0, v, 8, 8);

            // пометим как ROOT (это финальная стадия)
            v[15] ^= ROOT;

            // Выполним rounds над v с нулевым m (симметрично compress)
            for (int r = 0; r < rounds; r++)
            {
                G(v, mZeros, 0, 4, 8, 12, 0, 1);
                G(v, mZeros, 1, 5, 9, 13, 2, 3);
                G(v, mZeros, 2, 6, 10, 14, 4, 5);
                G(v, mZeros, 3, 7, 11, 15, 6, 7);

                G(v, mZeros, 0, 5, 10, 15, 8, 9);
                G(v, mZeros, 1, 6, 11, 12, 10, 11);
                G(v, mZeros, 2, 7, 8, 13, 12, 13);
                G(v, mZeros, 3, 4, 9, 14, 14, 15);
            }

            // out = little-endian bytes of (cv[i] ^ v[i]) for i=0..7
            byte[] out32 = new byte[OUT_LEN];
            for (int i = 0; i < 8; i++)
            {
                uint w = cv[i] ^ v[i];
                byte[] bs = BitConverter.GetBytes(w); // little-endian
                Array.Copy(bs, 0, out32, i * 4, 4);
            }

            return out32;
        }
    }
}
