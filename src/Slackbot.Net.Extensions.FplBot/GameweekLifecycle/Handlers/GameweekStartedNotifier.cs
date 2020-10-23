using Microsoft.Extensions.Logging;
using Slackbot.Net.Extensions.FplBot.Abstractions;
using Slackbot.Net.Extensions.FplBot.Extensions;
using Slackbot.Net.Extensions.FplBot.RecurringActions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Slackbot.Net.Extensions.FplBot.GameweekLifecycle.Handlers
{
    internal class GameweekStartedNotifier : IHandleGameweekStarted
    {
        private readonly ICaptainsByGameWeek _captainsByGameweek;
        private readonly ITransfersByGameWeek _transfersByGameweek;
        private readonly ISlackWorkSpacePublisher _publisher;
        private readonly ISlackTeamRepository _teamRepo;
        private readonly ILogger<GameweekStartedNotifier> _logger;

        public GameweekStartedNotifier(ICaptainsByGameWeek captainsByGameweek,
            ITransfersByGameWeek transfersByGameweek,
            ISlackWorkSpacePublisher publisher,
            ISlackTeamRepository teamsRepo,
            ILogger<GameweekStartedNotifier> logger)
        {
            _captainsByGameweek = captainsByGameweek;
            _transfersByGameweek = transfersByGameweek;
            _publisher = publisher;
            _teamRepo = teamsRepo;
            _logger = logger;
        }

        public async Task HandleGameweekStarted(int newGameweek)
        {
            await _publisher.PublishToAllWorkspaceChannels($"Gameweek {newGameweek}!");
            var teams = await _teamRepo.GetAllTeams();

            foreach (var team in teams)
            {
                try
                {
                    var messages = new List<string>();

                    if (team.Subscriptions.ContainsSubscriptionFor(EventSubscription.Captains))
                    {
                        messages.Add(await _captainsByGameweek.GetCaptainsByGameWeek(newGameweek, (int)team.FplbotLeagueId));
                        messages.Add(await _captainsByGameweek.GetCaptainsChartByGameWeek(newGameweek, (int)team.FplbotLeagueId));
                        _logger.LogInformation("Team {team} hasn't subscribed for gw start captains, so bypassing it", team.TeamId);
                    }

                    if (team.Subscriptions.ContainsSubscriptionFor(EventSubscription.Transfers))
                    {
                        messages.Add(await _transfersByGameweek.GetTransfersByGameweekTexts(newGameweek, (int)team.FplbotLeagueId));
                        _logger.LogInformation("Team {team} hasn't subscribed for gw start transfers, so bypassing it", team.TeamId);
                    }

                    await _publisher.PublishToWorkspace(team.TeamId, team.FplBotSlackChannel, messages.ToArray());

                }
                catch (Exception e)
                {
                    _logger.LogError(e, e.Message);
                }

            }
        }
    }
}