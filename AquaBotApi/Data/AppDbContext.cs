using Microsoft.EntityFrameworkCore;
using AquaBotApi.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace AquaBotApi.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

     
    }
}

