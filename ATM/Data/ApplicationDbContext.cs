using ATM.Controllers.Enum;
using ATM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace ATM.Data
{
    public class ApplicationDbContext : IdentityDbContext<IdentityUser>
    {
        public ApplicationDbContext(DbContextOptions options) : base(options)
        {
        }
        public DbSet<Balance> Balances { get; set; }
        public DbSet<ImportExcel> importExcels { get; set; }
        public DbSet<VerifyOtpModel> VerifyOtpModels { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Balance>()
                .HasOne(b => b.User)
                .WithMany()
                .HasForeignKey(b => b.UserId)
                .IsRequired();

            builder.Entity<Balance>()
                .Property(b => b.TransactionType)
                .HasConversion<string>();

            builder.Entity<ImportExcel>(entity =>
            {
                entity.ToTable("importExcels");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd(); // Auto-increment
            });
        }

    }
}
