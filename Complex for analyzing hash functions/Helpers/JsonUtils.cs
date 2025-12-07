// File: Helpers/JsonUtils.cs
using System;
using System.Text.Json;

namespace Complex_for_analyzing_hash_functions.Helpers
{
    public static class JsonUtils
    {
        /// <summary>
        /// Возвращает JsonElement-объект. Если источник - объект, возвращается он.
        /// Если источник - массив, пытаемся взять первый элемент-объект.
        /// Если в массиве нет объектов, оборачиваем массив в объект { "RawArray": [...] }.
        /// </summary>
        public static JsonElement NormalizeToObject(JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.Object)
                return json;

            if (json.ValueKind == JsonValueKind.Array)
            {
                // Найти первый элемент-объект в массиве
                foreach (var element in json.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.Object)
                        return element;
                }

                // Никаких объектов в массиве — оборачиваем массив в объект {"RawArray": [...]}
                // Для этого строим новый JsonDocument из строки
                string wrapped = "{\"RawArray\":" + json.GetRawText() + "}";
                using var doc = JsonDocument.Parse(wrapped);
                return doc.RootElement.Clone(); // возвращаем корневой объект
            }

            // Если это примитив (строка/число/булево/null), создаём объект {"RawValue": <value>}
            string primitiveWrapped = "{\"RawValue\":" + json.GetRawText() + "}";
            using var doc2 = JsonDocument.Parse(primitiveWrapped);
            return doc2.RootElement.Clone();
        }
    }
    public static class JsonSanitizer
    {
        public static double Fix(double value)
        {
            if (double.IsNaN(value)) return 0.0;
            if (double.IsPositiveInfinity(value)) return 1.0;
            if (double.IsNegativeInfinity(value)) return 0.0;
            return value;
        }
        public static double[][] ToJagged(double[,] matrix)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            double[][] jagged = new double[rows][];

            for (int i = 0; i < rows; i++)
            {
                jagged[i] = new double[cols];
                for (int j = 0; j < cols; j++)
                    jagged[i][j] = matrix[i, j];
            }

            return jagged;
        }

    }

}
