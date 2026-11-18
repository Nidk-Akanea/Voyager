using Voyager.Core.Shared;
using Voyager.Core.Trips;

namespace Voyager.Core.Tests.Support;

/// <summary>
/// Builds a trip that already exists, with a made-up name; nothing is checked.
/// </summary>
internal sealed class TripBuilder
{
    #region Fields

    private TripId _id = TripId.New();
    private GroupId _group = GroupId.New();
    private UserId _creator = UserId.New();
    private string _name = "Summer in Brittany";
    private TravelerId[] _travelers = [];

    #endregion Fields

    #region Public Methods

    public Trip Build() => Trip.Restore(_id, _group, _creator, _name, _travelers);

    public TripBuilder WithCreator(UserId creator) => Set(() => _creator = creator);

    public TripBuilder WithGroup(GroupId group) => Set(() => _group = group);

    public TripBuilder WithId(TripId id) => Set(() => _id = id);

    public TripBuilder WithName(string name) => Set(() => _name = name);

    public TripBuilder WithTravelers(params TravelerId[] travelers) => Set(() => _travelers = travelers);

    #endregion Public Methods

    #region Private Methods

    private TripBuilder Set(Action change)
    {
        change();
        return this;
    }

    #endregion Private Methods
}
