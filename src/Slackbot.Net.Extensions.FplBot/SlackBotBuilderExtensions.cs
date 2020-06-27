using System;

using Fpl.Client.Clients;
using Fpl.Client.Infra;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Slackbot.Net.Endpoints.Hosting;
using Slackbot.Net.Extensions.FplBot;
using Slackbot.Net.Extensions.FplBot.Abstractions;
using Slackbot.Net.Extensions.FplBot.GameweekLifecycle;
using Slackbot.Net.Extensions.FplBot.GameweekLifecycle.Handlers;
using Slackbot.Net.Extensions.FplBot.Handlers;
using Slackbot.Net.Extensions.FplBot.Helpers;
using Slackbot.Net.Extensions.FplBot.RecurringActions;

// ReSharper disable once CheckNamespace
namespace Slackbot.Net.Abstractions.Hosting
{
    public static class SlackBotBuilderExtensions
    {
        public static ISlackbotWorkerBuilder AddFplBot(this ISlackbotWorkerBuilder builder, Action<FplbotOptions> configure)
        {
            builder.Services.Configure<FplbotOptions>(configure);

            var opts = new FplbotOptions();
            configure(opts);
            builder.Services.AddFplApiClient(o =>
            {
                o.Login = opts.Login;
                o.Password = opts.Password;
            });
            builder.Services.AddSingleton<IFetchFplbotSetup, ConfigFplbotSetupFetcher>();
            builder.AddCommon();

            return builder;
        }

        public static ISlackbotWorkerBuilder AddFplBot(this ISlackbotWorkerBuilder builder, IConfiguration config)
        {
            builder.Services.Configure<FplbotOptions>(config);
            builder.Services.AddFplApiClient(config);
            builder.Services.AddSingleton<IFetchFplbotSetup, ConfigFplbotSetupFetcher>();
            builder.AddCommon();
            return builder;
        }

        public static ISlackbotWorkerBuilder AddDistributedFplBot<T>(this ISlackbotWorkerBuilder builder, Action<FplApiClientOptions> clientOptions) where T: class, IFetchFplbotSetup
        {
            builder.Services.AddFplApiClient(clientOptions);
            builder.Services.AddSingleton<IFetchFplbotSetup, T>();
            builder.AddCommon();
            return builder;
        }
        
        public static ISlackbotWorkerBuilder AddDistributedFplBot<T>(this ISlackbotWorkerBuilder builder, IConfiguration config) where T: class, IFetchFplbotSetup
        {
            builder.Services.AddFplApiClient(config);
            builder.Services.AddSingleton<IFetchFplbotSetup, T>();
            builder.AddCommon();
            return builder;
        }

        public static ISlackbotWorkerBuilder AddFplBotEventHandlers<T>(this ISlackbotWorkerBuilder builder) where T : class, ITokenStore
        {
            builder.Services.AddSlackBotEvents<T>()
                .AddShortcut<HelpEventHandler>()
                .AddHandler<FplPlayerCommandHandler>()
                .AddHandler<FplStandingsCommandHandler>()
                .AddHandler<FplNextGameweekCommandHandler>()
                .AddHandler<FplInjuryCommandHandler>()
                .AddHandler<FplCaptainCommandHandler>()
                .AddHandler<FplTransfersCommandHandler>();
            return builder;
        }

        private static void AddCommon(this ISlackbotWorkerBuilder builder)
        {
            builder.Services.AddSingleton<ICaptainsByGameWeek, CaptainsByGameWeek>();
            builder.Services.AddSingleton<ITransfersByGameWeek, TransfersByGameWeek>();
            builder.Services.AddSingleton<IGoalsDuringGameweek, GoalsDuringGameweek>();
            builder.Services.AddSingleton<IChipsPlayed, ChipsPlayed>();
            builder.Services.AddSingleton<ITeamValue, TeamValue>();
            builder.Services.AddSingleton<IGameweekHelper, GameweekHelper>();
            builder.Services.AddSingleton<ISlackWorkSpacePublisher,SlackWorkSpacePublisher>();
            builder.Services.AddSingleton<IHandleGameweekStarted, GameweekStartedNotifier>();
            builder.Services.AddSingleton<IMonitorFixtureEvents, FixtureEventsMonitor>();
            builder.Services.AddSingleton<IState, State>();
            builder.Services.AddSingleton<GameweekMonitorOrchestrator>();
            builder.Services.AddSingleton<DateTimeUtils>();
            builder.AddRecurring<GameweekLifecycleRecurringAction>()
                .AddRecurring<NearDeadlineRecurringAction>();
        }
    }
}