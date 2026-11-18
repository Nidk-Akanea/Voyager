using NodaTime;

using Voyager.Core.Shared;
using Voyager.Core.Trips;

namespace Voyager.Persistence.Tests.Support;

/// <summary>Makes made-up travelers: by default "Traveler A", not a profile, of a new owner, with nothing else.</summary>
internal sealed class TravelerBuilder
{
    private Avatar? _avatar;
    private LocalDate? _birthDate;
    private string? _description;
    private TravelerId _id = TravelerId.New();
    private bool _isProfile;
    private string _name = "Traveler A";
    private string? _nickname;
    private UserId _owner = UserId.New();

    public TravelerBuilder AsProfile()
    {
        _isProfile = true;
        return this;
    }

    public Traveler Build() => Traveler.Restore(_id, _owner, _isProfile, _name, _nickname, _birthDate, _avatar, _description);

    public TravelerBuilder WithAvatar(byte[] content, AvatarFormat format)
    {
        _avatar = Avatar.Restore(content, format);
        return this;
    }

    public TravelerBuilder WithBirthDate(LocalDate birthDate)
    {
        _birthDate = birthDate;
        return this;
    }

    public TravelerBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    public TravelerBuilder WithId(TravelerId id)
    {
        _id = id;
        return this;
    }

    public TravelerBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public TravelerBuilder WithNickname(string? nickname)
    {
        _nickname = nickname;
        return this;
    }

    public TravelerBuilder WithOwner(UserId owner)
    {
        _owner = owner;
        return this;
    }
}
