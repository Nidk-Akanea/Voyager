using Voyager.Core.Shared;
using Voyager.Core.Trips;

namespace Voyager.Core.Tests.Support;

/// <summary>
/// The groups of the test: who belongs to which.
/// </summary>
internal sealed class FakeGroupMembership : IGroupMembership
{
    #region Fields

    private readonly HashSet<(UserId User, GroupId Group)> _members = [];

    #endregion Fields

    #region Public Methods

    public void Add(UserId user, GroupId group) => _members.Add((user, group));

    public Task<bool> IsMemberAsync(UserId user, GroupId group, CancellationToken cancellationToken) =>
        Task.FromResult(_members.Contains((user, group)));

    public void Remove(UserId user, GroupId group) => _members.Remove((user, group));

    #endregion Public Methods
}
