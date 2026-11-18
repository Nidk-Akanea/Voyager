using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Voyager.Persistence.PostgreSql;

/// <summary>
/// What the EF Core tools use to make a migration (<c>dotnet ef migrations add</c>): a context on a database that is never
/// opened, since a migration is made from the model, not from a database.
/// </summary>
internal sealed class VoyagerDbContextFactory : IDesignTimeDbContextFactory<VoyagerDbContext>
{
    #region Public Methods

    /// <inheritdoc />
    public VoyagerDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<VoyagerDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=voyager-design-time",
                npgsql => npgsql.MigrationsAssembly(typeof(VoyagerDbContextFactory).Assembly.GetName().Name))
            .Options);

    #endregion Public Methods
}
