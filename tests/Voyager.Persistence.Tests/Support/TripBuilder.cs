using Voyager.Core.Shared;
using Voyager.Core.Trips;

namespace Voyager.Persistence.Tests.Support;

/// <summary>Makes made-up trips: by default "Trip to Lisbon", in a new group, with no traveler.</summary>
internal sealed class TripBuilder
{
    private UserId _creator = UserId.New();
    private GroupId _group = new(Guid.CreateVersion7());
    private TripId _id = new(Guid.CreateVersion7());
    private string _name = "Trip to Lisbon";
    private TravelerId[] _travelers = [];

    public Trip Build() => Trip.Restore(_id, _group, _creator, _name, _travelers);

    public TripBuilder InGroup(GroupId group)
    {
        _group = group;
        return this;
    }

    public TripBuilder WithCreator(UserId creator)
    {
        _creator = creator;
        return this;
    }

    public TripBuilder WithId(TripId id)
    {
        _id = id;
        return this;
    }

    public TripBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public TripBuilder WithTravelers(params TravelerId[] travelers)
    {
        _travelers = travelers;
        return this;
    }
}
