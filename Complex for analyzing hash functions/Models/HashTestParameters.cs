using System.ComponentModel.DataAnnotations;

namespace Complex_for_analyzing_hash_functions.Models
{
    public class HashTestParameters
    {
        [Range(1, int.MaxValue, ErrorMessage = "Количество тестов должно быть положительным.")]
        public int TestsCount { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Размер входных данных должен быть положительным.")]
        public int InputSizeBytes { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Число раундов должно быть положительным.")]
        public int Rounds { get; set; }

        public string Algorithm { get; set; } = "Keccak";
    }
}