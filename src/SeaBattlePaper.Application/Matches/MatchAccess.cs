using SeaBattlePaper.Domain.Matches;

namespace SeaBattlePaper.Application.Matches;

public sealed record MatchAccess(Match Match, Player Player, string PlayerToken);
