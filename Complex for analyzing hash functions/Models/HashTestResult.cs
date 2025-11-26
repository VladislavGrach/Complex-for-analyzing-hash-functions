using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Complex_for_analyzing_hash_functions.Models
{
    [Table("HashTestResults")]
    public class HashTestResult
    {
        public int Id { get; set; }    // EF Primary key

        public DateTime RunDate { get; set; }
        public required string Algorithm { get; set; }
        public int Rounds { get; set; }
        public int TestsCount { get; set; }

        public double AvgHamming { get; set; }
        public double StdDevHamming { get; set; }
        public double ChiSquare { get; set; }
        public int CollisionCount { get; set; }

        // Можно хранить агрегаты как JSON (требует конвертера EF)
        public required string BitFlipJson { get; set; }
    }
}
