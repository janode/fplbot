using Fpl.Client.Abstractions;
using Fpl.Client.Models;
using FplBot.Data.Slack;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Slackbot.Net.Endpoints.Hosting;
using Slackbot.Net.SlackClients.Http;
using Slackbot.Net.SlackClients.Http.Exceptions;

namespace FplBot.WebApi.Pages.Admin.TeamDetails;

public class TeamDetailsIndex : PageModel
{
    private readonly ISlackTeamRepository _teamRepo;
    private readonly ISlackClientBuilder _builder;
    private readonly ILeagueClient _leagueClient;
    private readonly IOptions<OAuthOptions> _slackAppOptions;
    private readonly ILogger<TeamDetailsIndex> _logger;

    public TeamDetailsIndex(ISlackTeamRepository teamRepo, ILogger<TeamDetailsIndex> logger, IOptions<OAuthOptions> slackAppOptions, ISlackClientBuilder builder, ILeagueClient leagueClient)
    {
        _teamRepo = teamRepo;
        _logger = logger;
        _slackAppOptions = slackAppOptions;
        _builder = builder;
        _leagueClient = leagueClient;
    }

    public async Task OnGet(string teamId)
    {
        var teamIdToUpper = teamId.ToUpper();
        var team = await _teamRepo.GetTeam(teamIdToUpper);
        if (team != null)
        {
            Team = team;
            if (team.FplbotLeagueId.HasValue)
            {
                var league = await _leagueClient.GetClassicLeague(team.FplbotLeagueId.Value, tolerate404:true);
                League = league;
            }

            var slackClient = await CreateSlackClient(teamIdToUpper);
            try
            {
                var channels = await slackClient.ConversationsListPublicChannels(500);
                ChannelStatus = channels.Channels.FirstOrDefault(c => team.FplBotSlackChannel == $"#{c.Name}" || team.FplBotSlackChannel == c.Id) != null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, e.Message);
            }
        }
    }

    public ClassicLeague League { get; set; }
    public bool? ChannelStatus { get; set; }

    public async Task<IActionResult> OnPost(string teamId)
    {
        _logger.LogInformation($"Deleting {teamId}");
        var slackClient = await CreateSlackClient(teamId);
        try
        {
            var res = await slackClient.AppsUninstall(_slackAppOptions.Value.CLIENT_ID, _slackAppOptions.Value.CLIENT_SECRET);
            if (res.Ok)
            {
                TempData["msg"] = "Uninstall queued, and will be handled at some point";
            }
            else
            {
                TempData["msg"] = $"Uninstall failed '{res.Error}'";
            }
        }
        catch (WellKnownSlackApiException e) when (e.Message == "account_inactive")
        {
            await _teamRepo.DeleteByTeamId(teamId);
            TempData["msg"] = "Token no longer valid. Team deleted.";
        }


        return RedirectToPage("../Index");
    }

    private async Task<ISlackClient> CreateSlackClient(string teamId)
    {
        var team = await _teamRepo.GetTeam(teamId);
        var slackClient = _builder.Build(token: team.AccessToken);
        return slackClient;
    }
    public SlackTeam Team { get; set; }
}
