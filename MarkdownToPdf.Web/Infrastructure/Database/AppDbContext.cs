using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MarkdownToPdf.Web.Infrastructure.Database;

// Inheriting from IdentityDbContext provides all the tables for Users, Roles, Claims, etc.
public sealed class AppDbContext : IdentityDbContext<IdentityUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // We will configure our custom feature tables (like Products/Orders) here later
        // builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}