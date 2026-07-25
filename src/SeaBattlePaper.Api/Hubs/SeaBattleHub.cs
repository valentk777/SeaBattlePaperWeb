using Microsoft.AspNetCore.SignalR;
using SeaBattlePaper.Application.Common;
using SeaBattlePaper.Application.Matches;
using SeaBattlePaper.Contracts.Matches;

namespace SeaBattlePaper.Api.Hubs;

public sealed class SeaBattleHub(
    SeaBattleService seaBattleService,
    SeaBattleConnectionRegistry connectionRegistry) : Hub
{
    public async Task JoinMatch(string matchId, string? playerToken)
    {
        var result = await seaBattleService.JoinMatchAsync(matchId, playerToken, Context.ConnectionAborted);
        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("JoinRejected", ToError(result.Error!), Context.ConnectionAborted);

            return;
        }

        var access = result.Value!;
        connectionRegistry.Attach(Context.ConnectionId, access.Match.Id, access.Player.Id);
        await Groups.AddToGroupAsync(Context.ConnectionId, MatchGroup(access.Match.Id), Context.ConnectionAborted);
        await Clients.Caller.SendAsync(
            "JoinAccepted",
            new JoinMatchResponse(access.Match.Id, access.PlayerToken, access.Player.Seat),
            Context.ConnectionAborted);
        await BroadcastMatchAsync(access.Match.Id);
    }

    public async Task UpdateNickname(string nickname)
    {
        var connection = connectionRegistry.GetConnection(Context.ConnectionId);
        if (connection is null)
        {
            await SendErrorAsync("not_joined", "Join a match before changing nickname.");

            return;
        }

        var result = await seaBattleService.UpdateNicknameAsync(
            connection.MatchId,
            connection.PlayerId,
            nickname,
            Context.ConnectionAborted);
        await HandleMutationResultAsync(connection.MatchId, result);
    }

    public async Task ReadyUp(IReadOnlyCollection<FleetPlacementDto> fleet)
    {
        var connection = connectionRegistry.GetConnection(Context.ConnectionId);
        if (connection is null)
        {
            await SendErrorAsync("not_joined", "Join a match before becoming ready.");

            return;
        }

        var result = await seaBattleService.ReadyUpAsync(
            connection.MatchId,
            connection.PlayerId,
            fleet,
            Context.ConnectionAborted);
        await HandleMutationResultAsync(connection.MatchId, result);
    }

    public async Task SaveFleetDraft(IReadOnlyCollection<FleetPlacementDto> fleet)
    {
        var connection = connectionRegistry.GetConnection(Context.ConnectionId);
        if (connection is null)
        {
            await SendErrorAsync("not_joined", "Join a match before saving fleet placement.");

            return;
        }

        var result = await seaBattleService.SaveFleetDraftAsync(
            connection.MatchId,
            connection.PlayerId,
            fleet,
            Context.ConnectionAborted);
        await HandleMutationResultAsync(connection.MatchId, result);
    }

    public async Task RemoveOpponent()
    {
        var connection = connectionRegistry.GetConnection(Context.ConnectionId);
        if (connection is null)
        {
            await SendErrorAsync("not_joined", "Join a match before removing an opponent.");

            return;
        }

        var result = await seaBattleService.RemoveOpponentAsync(
            connection.MatchId,
            connection.PlayerId,
            Context.ConnectionAborted);
        if (!result.IsSuccess)
        {
            await SendErrorAsync(result.Error!.Code, result.Error.Message);

            return;
        }

        foreach (var removedPlayerId in result.Value!)
        {
            var removedConnections = connectionRegistry.GetPlayerConnections(removedPlayerId);
            foreach (var removedConnectionId in removedConnections) connectionRegistry.Detach(removedConnectionId);

            if (removedConnections.Count > 0) await Clients.Clients(removedConnections).SendAsync("PlayerRemoved", Context.ConnectionAborted);
        }

        await BroadcastMatchAsync(connection.MatchId);
    }

    public async Task Fire(int row, int column)
    {
        var connection = connectionRegistry.GetConnection(Context.ConnectionId);
        if (connection is null)
        {
            await SendErrorAsync("not_joined", "Join a match before firing.");

            return;
        }

        var result = await seaBattleService.FireAsync(
            connection.MatchId,
            connection.PlayerId,
            row,
            column,
            Context.ConnectionAborted);
        await HandleMutationResultAsync(connection.MatchId, result);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connection = connectionRegistry.Detach(Context.ConnectionId);
        if (connection is not null) await BroadcastMatchAsync(connection.MatchId);

        await base.OnDisconnectedAsync(exception);
    }

    private async Task HandleMutationResultAsync(string matchId, OperationResult result)
    {
        if (!result.IsSuccess)
        {
            await SendErrorAsync(result.Error!.Code, result.Error.Message);

            return;
        }

        await BroadcastMatchAsync(matchId);
    }

    private async Task BroadcastMatchAsync(string matchId)
    {
        var onlinePlayers = connectionRegistry.GetOnlinePlayers(matchId);
        var connections = connectionRegistry.GetMatchConnections(matchId);
        foreach (var group in connections.GroupBy(connection => connection.PlayerId))
        {
            var state = await seaBattleService.GetStateAsync(
                matchId,
                group.Key,
                onlinePlayers,
                Context.ConnectionAborted);
            if (state.IsSuccess)
                await Clients.Clients(group.Select(connection => connection.ConnectionId)).SendAsync(
                    "StateUpdated",
                    state.Value,
                    Context.ConnectionAborted);
        }

        await Clients.Group(MatchGroup(matchId)).SendAsync("PresenceUpdated", onlinePlayers, Context.ConnectionAborted);
    }

    private async Task SendErrorAsync(string code, string message) =>
        await Clients.Caller.SendAsync("Error", new HubError(code, message), Context.ConnectionAborted);

    private static HubError ToError(OperationError error) => new(error.Code, error.Message);

    private static string MatchGroup(string matchId) => $"match:{matchId}";
}
