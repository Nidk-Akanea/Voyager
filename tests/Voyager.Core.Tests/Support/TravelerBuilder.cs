using NodaTime;

using Voyager.Core.Shared;
using Voyager.Core.Trips;

namespace Voyager.Core.Tests.Support;

/// <summary>
/// Builds a traveler that already exists, with made-up data; nothing is checked, so a test can start from any state.
/// </summary>
internal sealed class TravelerBuilder
{
    #region Fields

    private TravelerId _id = TravelerId.New();
    private UserId _owner = UserId.New();
    private bool _isProfile;
    private string _name = "Traveler A";
    private string? _nickname;
    private LocalDate? _birthDate;
    private string? _description;
    private Avatar? _avatar;

    #endregion Fields

    #region Public Methods

    public Traveler Build() =>
        Traveler.Restore(_id, _owner, _isProfile, _name, _nickname, _birthDate, _avatar, _description);

    public TravelerBuilder AsProfile() => Set(() => _isProfile = true);

    public TravelerBuilder WithAvatar(byte[] image) => Set(() => _avatar = Avatar.Create(image).Value);

    public TravelerBuilder WithBirthDate(LocalDate birthDate) => Set(() => _birthDate = birthDate);

    public TravelerBuilder WithDescription(string description) => Set(() => _description = description);

    public TravelerBuilder WithId(TravelerId id) => Set(() => _id = id);

    public TravelerBuilder WithName(string name) => Set(() => _name = name);

    public TravelerBuilder WithNickname(string nickname) => Set(() => _nickname = nickname);

    public TravelerBuilder WithOwner(UserId owner) => Set(() => _owner = owner);

    #endregion Public Methods

    #region Private Methods

    private TravelerBuilder Set(Action change)
    {
        change();
        return this;
    }

    #endregion Private Methods
}
