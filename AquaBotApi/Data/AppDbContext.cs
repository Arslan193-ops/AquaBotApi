// Data/AppDbContext.cs - Updated
using AquaBotApi.Models;
using AquaBotApi.Services;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AquaBotApi.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<SoilData> SoilDatas { get; set; }
        public DbSet<ImageAnalysisResult> ImageAnalysisResults { get; set; } // ✅ New table

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure relationships
            builder.Entity<SoilData>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ImageAnalysisResult>()
                .HasOne(i => i.User)
                .WithMany()
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}

// Program.cs - Updated (add after your existing services)
// Add this line after your existing service registrations:


// You'll also need to create wwwroot/uploads/images folder for image storage
// Create these folders in your project root:
// - wwwroot/
// - wwwroot/uploads/
// - wwwroot/uploads/images/