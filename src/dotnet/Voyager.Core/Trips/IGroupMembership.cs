using Voyager.Core.Shared;

namespace Voyager.Core.Trips;

/// <summary>
/// What the trips need to know about the groups: whether a user is a member of one. The groups have their own domain
/// later (ADR-0019); until then the host answers, with the development group under Aspire.
/// </summary>
public interface IGroupMembership
{
    /// <summary>Whether a user belongs to a group.</summary>
    /// <param name="user">The user.</param>
    /// <param name="group">The group.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    /// <returns>True when the user is a member of the group.</returns>
    Task<bool> IsMemberAsync(UserId user, GroupId group, CancellationToken cancellationToken);
}
