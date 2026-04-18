using Microsoft.EntityFrameworkCore;
using ScanCheckSakura.Models;

namespace ScanCheckSakura.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<SM_CBCPLog> CBCPLogs { get; set; }

        public DbSet<SM_Defect_Cookie> SM_Defect_Cookie { get; set; }

         // *********** FQCBP ************
        public DbSet<SM_FQCBP>   SM_FQCBP   { get; set; }
        public DbSet<SM_FQCBP_H> SM_FQCBP_H { get; set; }

        public DbSet<SVN_quality_reason> SVN_quality_reason { get; set; }

        // *********** FQCBP Dev ************
        public DbSet<SM_FQCBP_Dev> SM_FQCBP_Dev { get; set; }
        public DbSet<SM_FQCBP_H_Dev> SM_FQCBP_H_Dev { get; set; }
        
    
    }
}