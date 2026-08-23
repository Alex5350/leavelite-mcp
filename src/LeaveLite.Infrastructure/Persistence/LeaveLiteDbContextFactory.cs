using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LeaveLite.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so <c>dotnet ef</c> can create migrations from this class library without
/// having to host the web application. Uses the same default connection string as production code.
/// </summary>
public sealed class LeaveLiteDbContextFactory : IDesignTimeDbContextFactory<LeaveLiteDbContext>
{
    public const string DefaultConnectionString = "Data Source=leavelite.db";

    public LeaveLiteDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LeaveLiteDbContext>()
            .UseSqlite(DefaultConnectionString)
            .Options;

        return new LeaveLiteDbContext(options);
    }
}
