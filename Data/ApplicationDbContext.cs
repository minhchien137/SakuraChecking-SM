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

        // *********** FPY (First Pass Yield) ************
        public DbSet<SM_FQCFPY>   SM_FQCFPY   { get; set; }
        public DbSet<SM_FQCFPY_H> SM_FQCFPY_H { get; set; }

        public DbSet<SVN_Defect_Record> SVN_Defect_Record { get; set; }
        public DbSet<SVN_target>        SVN_target        { get; set; }

        public DbSet<SVN_quality_reason> SVN_quality_reason { get; set; }

        // *********** FQCBP Dev ************
        public DbSet<SVN_ProductionInputLogs> SVN_ProductionInputLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Cả hai bảng ngoài không có PK được quản lý bởi EF
            // → dùng HasNoKey, upsert qua raw SQL MERGE
            modelBuilder.Entity<SVN_Defect_Record>().HasNoKey().ToTable("SVN_Defect_Record");
            modelBuilder.Entity<SVN_target>().HasNoKey().ToTable("SVN_target");
        }
    }
}