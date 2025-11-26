using Complex_for_analyzing_hash_functions.Models;
using Microsoft.EntityFrameworkCore;

namespace Complex_for_analyzing_hash_functions.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<HashTestResult> HashTestResults { get; set; }
    }
}
