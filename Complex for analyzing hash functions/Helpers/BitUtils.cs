using System;
using System.Text;

namespace Complex_for_analyzing_hash_functions.Helpers
{
    public static class BitUtils
    {
        // Преобразует массив байтов в строку '0'/'1'
        public static string BytesToBitString(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return string.Empty;
            StringBuilder sb = new StringBuilder(bytes.Length * 8);
            foreach (var b in bytes)
            {
                sb.Append(Convert.ToString(b, 2).PadLeft(8, '0'));
            }
            return sb.ToString();
        }
    }
}
