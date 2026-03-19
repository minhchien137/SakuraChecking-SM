using Microsoft.EntityFrameworkCore;
using ScanCheckSakura.Models;

namespace ScanCheckSakura.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<SM_CBCPLog> CBCPLogs { get; set; }
    }
}