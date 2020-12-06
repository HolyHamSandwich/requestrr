﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Requestrr.WebApi.RequestrrBot.TvShows.SeasonsRequestWorkflows;

namespace Requestrr.WebApi.RequestrrBot.TvShows
{
    public class TvShowRequestingWorkflow
    {
        private readonly TvShowUserRequester _user;
        private readonly ITvShowSearcher _searcher;
        private readonly ITvShowRequester _requester;
        private readonly ITvShowUserInterface _userInterface;
        private readonly ITvShowNotificationWorkflow _tvShowNotificationWorkflow;
        private readonly TvShowsSettings _settings;

        public TvShowRequestingWorkflow(
            TvShowUserRequester user,
            ITvShowSearcher searcher,
            ITvShowRequester requester,
            ITvShowUserInterface userInterface,
            ITvShowNotificationWorkflow tvShowNotificationWorkflow,
            TvShowsSettings settings)
        {
            _user = user;
            _searcher = searcher;
            _requester = requester;
            _userInterface = userInterface;
            _tvShowNotificationWorkflow = tvShowNotificationWorkflow;
            _settings = settings;
        }

        public async Task RequestTvShowAsync(string tvShowName)
        {
            var searchedTvShows = await SearchTvShowAsync(tvShowName);

            if (searchedTvShows.Any())
            {
                if (searchedTvShows.Count > 1)
                {
                    var tvShowSelection = await _userInterface.GetTvShowSelectionAsync(searchedTvShows);

                    if (!tvShowSelection.IsCancelled && tvShowSelection.SelectedTvShow.IsSpecified)
                    {
                        var selection = tvShowSelection.SelectedTvShow.Value;
                        await HandleTvShowSelection(selection);
                    }
                    else if (!tvShowSelection.IsCancelled)
                    {
                        await _userInterface.WarnInvalidTvShowSelectionAsync();
                    }
                }
                else if (searchedTvShows.Count == 1)
                {
                    var selection = searchedTvShows.Single();
                    await HandleTvShowSelection(selection);
                }
            }
        }

        public async Task<IReadOnlyList<SearchedTvShow>> SearchTvShowAsync(string tvShowName)
        {
            IReadOnlyList<SearchedTvShow> searchedTvShows = Array.Empty<SearchedTvShow>();

            if (tvShowName.Trim().ToLower().StartsWith("tvdb"))
            {
                var tvDbIdTextValue = tvShowName.ToLower().Split("tvdb")[1]?.Trim();

                if (int.TryParse(tvDbIdTextValue, out var tvDbId))
                {
                    try
                    {
                        var searchedTvShow = await _searcher.SearchTvShowAsync(tvDbId);
                        searchedTvShows = new List<SearchedTvShow> { searchedTvShow }.Where(x => x != null).ToArray();
                    }
                    catch
                    {
                        searchedTvShows = new List<SearchedTvShow>();
                    }

                    if (!searchedTvShows.Any())
                    {
                        await _userInterface.WarnNoTvShowFoundByTvDbIdAsync(tvDbIdTextValue);
                    }
                }
                else
                {
                    await _userInterface.WarnNoTvShowFoundByTvDbIdAsync(tvDbIdTextValue);
                }
            }
            else
            {
                tvShowName = tvShowName.Replace(".", " ");
                searchedTvShows = await _searcher.SearchTvShowAsync(tvShowName);

                if (!searchedTvShows.Any())
                {
                    await _userInterface.WarnNoTvShowFoundAsync(tvShowName);
                }
            }

            return searchedTvShows;
        }

        private async Task HandleTvShowSelection(SearchedTvShow searchedTvShow)
        {
            var tvShow = await GetTvShow(searchedTvShow);

            if (!tvShow.Seasons.Any() && tvShow.HasEnded)
            {
                await _userInterface.DisplayTvShowDetailsAsync(tvShow);
                await _userInterface.WarnShowCannotBeRequestedAsync(tvShow);
            }
            else if (tvShow.AllSeasonsFullyRequested())
            {
                if (tvShow.Seasons.OfType<FutureTvSeasons>().Any())
                {
                    await new FutureSeasonsRequestingWorkflow(_user, _searcher, _requester, _userInterface, _tvShowNotificationWorkflow)
                        .RequestAsync(_settings.Categories[0], tvShow, tvShow.Seasons.OfType<FutureTvSeasons>().FirstOrDefault());
                }
                else
                {
                    await _userInterface.DisplayTvShowDetailsAsync(tvShow);
                    await _userInterface.WarnShowHasEndedAsync(tvShow);
                }
            }
            else if (!tvShow.IsMultiSeasons() && tvShow.Seasons.OfType<NormalTvSeason>().Any())
            {
                await new NormalTvSeasonRequestingWorkflow(_user, _searcher, _requester, _userInterface, _tvShowNotificationWorkflow)
                    .RequestAsync(_settings.Categories[0], tvShow, tvShow.Seasons.OfType<NormalTvSeason>().Single());
            }
            else
            {
                await RequestTvShowSeason(tvShow);
            }
        }

        private async Task RequestTvShowSeason(TvShow tvShow)
        {
            var seasonSelection = await GetTvShowSelectionBasedOnRestrictions(tvShow);

            if (!seasonSelection.IsCancelled && seasonSelection.SelectedSeason.IsSpecified)
            {
                var selectedSeason = seasonSelection.SelectedSeason.Value;

                switch (selectedSeason)
                {
                    case FutureTvSeasons futureTvSeasons:
                        await new FutureSeasonsRequestingWorkflow(_user, _searcher, _requester, _userInterface, _tvShowNotificationWorkflow)
                            .RequestAsync(_settings.Categories[0], tvShow, futureTvSeasons);
                        break;
                    case AllTvSeasons allTvSeasons:
                        await new AllSeasonsRequestingWorkflow(_user, _searcher, _requester, _userInterface, _tvShowNotificationWorkflow)
                            .RequestAsync(_settings.Categories[0], tvShow, allTvSeasons);
                        break;
                    case NormalTvSeason normalTvSeason:
                        await new NormalTvSeasonRequestingWorkflow(_user, _searcher, _requester, _userInterface, _tvShowNotificationWorkflow)
                            .RequestAsync(_settings.Categories[0], tvShow, normalTvSeason);
                        break;
                    default:
                        throw new Exception($"Could not handle season of type \"{selectedSeason.GetType().Name}\"");
                }
            }
            else if (!seasonSelection.IsCancelled)
            {
                await _userInterface.WarnInvalidSeasonSelectionAsync();
            }
        }

        private async Task<TvSeasonsSelection> GetTvShowSelectionBasedOnRestrictions(TvShow tvShow)
        {
            TvSeasonsSelection seasonSelection = null;

            if (_settings.Restrictions == TvShowsRestrictions.AllSeasons)
            {
                seasonSelection = new TvSeasonsSelection
                {
                    SelectedSeason = tvShow.Seasons.OfType<AllTvSeasons>().Single()
                };
            }
            else if (_settings.Restrictions == TvShowsRestrictions.SingleSeason)
            {
                tvShow.Seasons = tvShow.Seasons.Where(x => !(x is AllTvSeasons)).ToArray();
                seasonSelection = await _userInterface.GetTvShowSeasonSelectionAsync(tvShow);
            }
            else if (_settings.Restrictions == TvShowsRestrictions.None)
            {
                seasonSelection = await _userInterface.GetTvShowSeasonSelectionAsync(tvShow);
            }
            else
            {
                throw new NotImplementedException($"Tv shows restriction of type {_settings.Restrictions} has not been implemented.");
            }

            return seasonSelection;
        }

        private async Task<TvShow> GetTvShow(SearchedTvShow searchedTvShow)
        {
            var tvShow = await _searcher.GetTvShowDetailsAsync(searchedTvShow);

            if (!tvShow.HasEnded)
            {
                tvShow.Seasons = tvShow.Seasons.Append(new FutureTvSeasons
                {
                    SeasonNumber = tvShow.Seasons?.Any() == true ? tvShow.Seasons.Max(x => x.SeasonNumber) + 1 : 1,
                    IsAvailable = false,
                    IsRequested = tvShow.IsRequested ? RequestedState.Full : RequestedState.None,
                }).ToArray();
            }

            if (tvShow.IsMultiSeasons())
            {
                tvShow.Seasons = tvShow.Seasons.Prepend(new AllTvSeasons
                {
                    SeasonNumber = 0,
                    IsAvailable = false,
                    IsRequested = RequestedState.None,
                }).ToArray();
            }

            return tvShow;
        }
    }
}