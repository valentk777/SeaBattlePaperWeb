namespace SeaBattlePaper.Contracts.Matches;

public sealed record JoinMatchRequest(string MatchId, string? PlayerToken);

public sealed record UpdateNicknameRequest(string Nickname);

public sealed record ReadyUpRequest(IReadOnlyCollection<FleetPlacementDto> Fleet);

public sealed record FireRequest(int Row, int Column);

public sealed record HubError(string Code, string Message);
