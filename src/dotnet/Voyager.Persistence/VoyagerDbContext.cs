using Microsoft.EntityFrameworkCore;

namespace Voyager.Persistence;

/// <summary>
/// The Voyager database of the group server (PostgreSQL, ADR-0018). Entities arrive with the first business
/// features.
/// </summary>
/// <param name="options">The provider and connection chosen by the host.</param>
public sealed class VoyagerDbContext(DbContextOptions<VoyagerDbContext> options) : DbContext(options);
