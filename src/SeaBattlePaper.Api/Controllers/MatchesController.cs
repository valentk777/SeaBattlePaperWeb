using Microsoft.AspNetCore.Mvc;
using SeaBattlePaper.Api.Common;
using SeaBattlePaper.Application.Matches;
using SeaBattlePaper.Contracts.Matches;

namespace SeaBattlePaper.Api.Controllers;

[ApiController]
[Route("api/matches")]
public sealed class MatchesController(SeaBattleService seaBattleService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CreateMatchResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateMatch(
        [FromBody] CreateMatchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await seaBattleService.CreateMatchAsync(request, cancellationToken);
        if (!result.IsSuccess) return this.ToActionResult(result.Error!);

        return CreatedAtAction(nameof(ValidateMatch), new { matchId = result.Value!.MatchId }, result.Value);
    }

    [HttpGet("{matchId}/validate")]
    [ProducesResponseType<ValidateMatchResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidateMatch(string matchId, CancellationToken cancellationToken)
    {
        var result = await seaBattleService.ValidateMatchAsync(matchId, cancellationToken);

        return Ok(new ValidateMatchResponse(matchId, result.Value));
    }
}
