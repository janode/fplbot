﻿using Fpl.Client.Abstractions;
using Fpl.Search.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Linq;
using System.Threading.Tasks;
using Nest;

namespace Fpl.Search.Indexing
{
    public class LeagueIndexProvider : IndexProviderBase, IIndexProvider<LeagueItem>
    {
        private readonly IEntryClient _entryClient;
        private readonly IIndexBookmarkProvider _indexBookmarkProvider;
        private readonly SearchOptions _options;
        private int _currentConsecutiveCountOfMissingLeagues;
        private int _bookmarkCounter;

        public LeagueIndexProvider(
            ILeagueClient leagueClient,
            IEntryClient entryClient,
            IIndexBookmarkProvider indexBookmarkProvider,
            ILogger<IndexProviderBase> logger,
            IOptions<SearchOptions> options) : base(leagueClient, logger)
        {
            _entryClient = entryClient;
            _indexBookmarkProvider = indexBookmarkProvider;
            _options = options.Value;
        }

        public string IndexName => _options.LeaguesIndex;
        public Task<int> StartIndexingFrom => _indexBookmarkProvider.GetBookmark();

        public async Task<(LeagueItem[], bool)> GetBatchToIndex(int i, int batchSize)
        {
            var batch = await GetBatchOfLeagues(i, batchSize, (client, x) => client.GetClassicLeague(x, tolerate404: true));
            var items = batch
                .Where(x => x != null && x.Exists)
                .Select(x => new LeagueItem { Id = x.Properties.Id, Name = x.Properties.Name, AdminEntry = x.Properties.AdminEntry })
                .ToArray();

            var admins = await Task.WhenAll(items.Where(x => x.AdminEntry != null).Select(x => x.AdminEntry.Value).Distinct().Select(x => _entryClient.Get(x)));
            foreach (var leagueItem in items)
            {
                var admin = admins.SingleOrDefault(a => a.Id == leagueItem.AdminEntry);
                if (admin != null)
                {
                    leagueItem.AdminName = admin.PlayerFullName;
                    leagueItem.AdminTeamName = admin.TeamName;
                    leagueItem.AdminCountry = admin.PlayerRegionShortIso;
                }
            }

            if (!items.Any())
            {
                _currentConsecutiveCountOfMissingLeagues += batchSize;
            }
            else
            {
                _currentConsecutiveCountOfMissingLeagues = 0;
            }
            
            // There are large "gaps" of missing leagues (deleted ones, perhaps). The indexing job needs to work its way past these gaps, but still stop when 
            // we think that there are none left to index
            var couldBeMore = _currentConsecutiveCountOfMissingLeagues < _options.ConsecutiveCountOfMissingLeaguesBeforeStoppingIndexJob;
            
            if (!couldBeMore)
            {
                await _indexBookmarkProvider.SetBookmark(1);
            }
            else if (_bookmarkCounter > 1000) // Set a bookmark at every 1000nth batch
            {
                await _indexBookmarkProvider.SetBookmark(i + batchSize);
                _bookmarkCounter = 0;
            }
            else
            {
                _bookmarkCounter++;
            }

            return (items, couldBeMore);
        }
    }

    public class LeagueIndexBookmarkProvider : IIndexBookmarkProvider
    {
        private readonly ILogger<LeagueIndexBookmarkProvider> _logger;
        private readonly IDatabase _db;
        private const string BookmarkKey = "leagueIndexBookmark";

        public LeagueIndexBookmarkProvider(ConnectionMultiplexer redis, ILogger<LeagueIndexBookmarkProvider> logger)
        {
            _logger = logger;
            _db = redis.GetDatabase();
        }

        public async Task<int> GetBookmark()
        {
            var valid = (await _db.StringGetAsync(BookmarkKey)).TryParse(out int bookmark);
            _logger.LogError($"Unable to parse {BookmarkKey} from db");

            return valid ? bookmark : 1;
        }

        public async Task SetBookmark(int bookmark)
        {
            var success = await _db.StringSetAsync(BookmarkKey, bookmark);
            if (!success)
            {
                _logger.LogError($"Unable to set {BookmarkKey} in db");
            }
        }
    }

    public interface IIndexBookmarkProvider
    {
        Task<int> GetBookmark();
        Task SetBookmark(int bookmark);
    }
}