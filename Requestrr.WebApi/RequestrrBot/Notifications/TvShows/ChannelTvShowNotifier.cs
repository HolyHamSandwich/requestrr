﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Requestrr.WebApi.RequestrrBot.ChatClients.Discord;
using Requestrr.WebApi.RequestrrBot.DownloadClients;
using Requestrr.WebApi.RequestrrBot.TvShows;

namespace Requestrr.WebApi.RequestrrBot.Notifications.TvShows
{
    public class ChannelTvShowNotifier : ITvShowNotifier
    {
        private readonly DiscordSocketClient _discordClient;
        private readonly DiscordSettingsProvider _discordSettingsProvider;
        private readonly string[] _channelNames;
        private readonly ILogger<ChatBot> _logger;

        public ChannelTvShowNotifier(
            DiscordSocketClient discordClient,
            DiscordSettingsProvider discordSettingsProvider,
            string[] channelNames,
            ILogger<ChatBot> logger)
        {
            _discordClient = discordClient;
            _discordSettingsProvider = discordSettingsProvider;
            _channelNames = channelNames;
            _logger = logger;
        }

        public async Task<HashSet<string>> NotifyAsync(IReadOnlyCollection<string> userIds, TvShow tvShow, int seasonNumber, CancellationToken token)
        {
            var discordUserIds = new HashSet<ulong>(userIds.Select(x => ulong.Parse(x)));
            var userNotified = new HashSet<string>();

            var channels = GetNotificationChannels();
            HandleRemovedUsers(discordUserIds, userNotified, token);

            foreach (var channel in channels)
            {
                if (token.IsCancellationRequested)
                    return userNotified;

                try
                {
                    await NotifyUsersInChannel(tvShow, seasonNumber, discordUserIds, userNotified, channel);
                }
                catch (System.Exception ex)
                {
                    _logger.LogError(ex, $"An error occurred while sending a tv show notification to channel \"{channel?.Name}\" in server \"{channel?.Guild?.Name}\": " + ex.Message);
                }
            }

            return userNotified;
        }

        private SocketTextChannel[] GetNotificationChannels()
        {
            return _discordClient.Guilds
                .SelectMany(x => x.Channels)
                .Where(x => _channelNames.Any(n => n.Equals(x.Name, StringComparison.InvariantCultureIgnoreCase)))
                .OfType<SocketTextChannel>()
                .ToArray();
        }

        private void HandleRemovedUsers(HashSet<ulong> discordUserIds, HashSet<string> userNotified, CancellationToken token)
        {
            foreach (var userId in discordUserIds.Where(x => _discordClient.GetUser(x) == null))
            {
                if (token.IsCancellationRequested)
                    return;

                if (_discordClient.ConnectionState == ConnectionState.Connected)
                {
                    userNotified.Add(userId.ToString());
                    _logger.LogWarning($"An issue occurred while sending a tv show notification to a specific user, could not find user with ID {userId}. {System.Environment.NewLine} Make sure [Presence Intent] and [Server Members Intent] are enabled in the bots configuration.");
                }
            }
        }

        private async Task NotifyUsersInChannel(TvShow tvShow, int seasonNumber, HashSet<ulong> discordUserIds, HashSet<string> userNotified, SocketTextChannel channel)
        {
            var usersToMention = channel.Users
                .Where(x => discordUserIds.Contains(x.Id))
                .Where(x => !userNotified.Contains(x.Id.ToString()));

            if (usersToMention.Any())
            {
                var messageBuilder = new StringBuilder();

                if(_discordSettingsProvider.Provide().TvShowDownloadClient == DownloadClient.Overseerr)
                    messageBuilder.AppendLine($"The **season {seasonNumber}** of **{tvShow.Title}** has finished downloading!");
                else
                    messageBuilder.AppendLine($"The first episode of **season {seasonNumber}** of **{tvShow.Title}** has finished downloading!");

                foreach (var user in usersToMention)
                {
                    var userMentionText = $"{user.Mention} ";

                    if (messageBuilder.Length + userMentionText.Length < DiscordConstants.MaxMessageLength)
                        messageBuilder.Append(userMentionText);
                }

                await channel.SendMessageAsync(messageBuilder.ToString(), false, DiscordTvShowsRequestingWorkFlow.GenerateTvShowDetailsAsync(tvShow));

                foreach (var user in usersToMention)
                {
                    userNotified.Add(user.Id.ToString());
                }
            }
        }
    }
}