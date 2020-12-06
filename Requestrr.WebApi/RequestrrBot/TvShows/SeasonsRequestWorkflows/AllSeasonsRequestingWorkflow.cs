﻿using System;
using System.Linq;
using System.Threading.Tasks;

namespace Requestrr.WebApi.RequestrrBot.TvShows.SeasonsRequestWorkflows
{
    public class AllSeasonsRequestingWorkflow
    {
        private readonly TvShowUserRequester _user;
        private readonly ITvShowSearcher _searcher;
        private readonly ITvShowRequester _requester;
        private readonly ITvShowUserInterface _userInterface;
        private readonly ITvShowNotificationWorkflow _tvShowNotificationWorkflow;

        public AllSeasonsRequestingWorkflow(
            TvShowUserRequester user,
            ITvShowSearcher searcher,
            ITvShowRequester requester,
            ITvShowUserInterface userInterface,
            ITvShowNotificationWorkflow tvShowNotificationWorkflow)
        {
            _user = user;
            _searcher = searcher;
            _requester = requester;
            _userInterface = userInterface;
            _tvShowNotificationWorkflow = tvShowNotificationWorkflow;
        }

        public async Task RequestAsync(Guid categoryId, TvShow tvShow, AllTvSeasons selectedSeason)
        {
            await _userInterface.DisplayTvShowDetailsAsync(tvShow);

            var wasRequested = await _userInterface.GetTvShowRequestConfirmationAsync(selectedSeason);

            if (wasRequested)
            {
                var result = await _requester.RequestTvShowAsync(categoryId, _user, tvShow, selectedSeason);

                if (result.WasDenied)
                {
                    await _userInterface.DisplayRequestDeniedForSeasonAsync(selectedSeason);
                }
                else
                {
                    await _userInterface.DisplayRequestSuccessForSeasonAsync(selectedSeason);

                    foreach (var season in tvShow.Seasons.OfType<NormalTvSeason>().Where(x => !x.IsAvailable))
                    {
                        await _tvShowNotificationWorkflow.NotifyForNewRequestAsync(_user.UserId, tvShow, season);
                    }
                }
            }
        }
    }
}