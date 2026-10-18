using VoresFamilie.Domain;

namespace VoresFamilie.Application;

public sealed record MemberDto(Guid Id, string DisplayName, Role Role, Dictionary<Area, AccessLevel> Permissions, bool CanCheckOwnItems);

public sealed record InvitationDto(Guid Id, Role Role, string? Email, DateTimeOffset ExpiresAt, bool Used);

public sealed record FamilyDto(Guid Id, string Name, MemberDto Me);

public sealed record CreatedInvitationDto(Guid Id, string Token);

public sealed record CreateFamilyRequest(string Name, string DisplayName);

public sealed record CreateInvitationRequest(Role Role, string? Email);

public sealed record AcceptInvitationRequest(string DisplayName);

public sealed record ChangeRoleRequest(Role Role);

public sealed record SetPermissionRequest(Area Area, AccessLevel Level);

public sealed record SetCanCheckOwnItemsRequest(bool Value);
