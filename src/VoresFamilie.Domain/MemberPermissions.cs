namespace VoresFamilie.Domain;

public sealed class MemberPermissions
{
    private readonly Dictionary<Area, AccessLevel> _levels;

    public MemberPermissions(IDictionary<Area, AccessLevel> levels, bool canCheckOwnItems)
    {
        _levels = Enum.GetValues<Area>().ToDictionary(a => a, a => levels.TryGetValue(a, out var l) ? l : AccessLevel.None);
        CanCheckOwnItems = canCheckOwnItems;
    }

    /// <summary>Whether the member may tick off their own routines and homework.</summary>
    public bool CanCheckOwnItems { get; }

    public IReadOnlyDictionary<Area, AccessLevel> Levels => _levels;

    public AccessLevel For(Area area) => _levels[area];

    public bool Allows(Area area, AccessLevel required) => _levels[area] >= required;

    public MemberPermissions With(Area area, AccessLevel level)
    {
        var copy = new Dictionary<Area, AccessLevel>(_levels) { [area] = level };
        return new MemberPermissions(copy, CanCheckOwnItems);
    }

    public MemberPermissions WithCanCheckOwnItems(bool value) => new(_levels, value);

    public static MemberPermissions DefaultsFor(Role role)
    {
        var level = role == Role.Barn ? AccessLevel.View : AccessLevel.Edit;
        var all = Enum.GetValues<Area>().ToDictionary(a => a, _ => level);
        return new MemberPermissions(all, canCheckOwnItems: true);
    }
}
