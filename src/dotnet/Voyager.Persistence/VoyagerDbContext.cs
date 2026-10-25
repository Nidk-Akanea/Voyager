using Microsoft.EntityFrameworkCore;

namespace Voyager.Persistence;

/// <summary>
/// The Voyager database, shared by the local service (SQLite) and the group server (PostgreSQL),
/// as decided in ADR-0004. Entities arrive with the first business features.
/// </summary>
/// <param name="options">The provider and connection chosen by the host.</param>
public sealed class VoyagerDbContext(DbContextOptions<VoyagerDbContext> options) : DbContext(options);
