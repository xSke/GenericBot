﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using GenericBot.Entities;

namespace GenericBot
{
    public static class UserEventHandler
    {
        public static async Task UserUpdated(SocketGuildUser beforeUser, SocketGuildUser afterUser)
        {
            if (beforeUser.Username != afterUser.Username || beforeUser.Nickname != afterUser.Nickname)
            {
                var guildDb = new DBGuild(afterUser.Guild.Id);
                if (guildDb.Users.Any(u => u.ID.Equals(afterUser.Id))) // if already exists
                {
                    guildDb.Users.Find(u => u.ID.Equals(afterUser.Id)).AddUsername(beforeUser.Username);
                    guildDb.Users.Find(u => u.ID.Equals(afterUser.Id)).AddNickname(beforeUser);
                    guildDb.Users.Find(u => u.ID.Equals(afterUser.Id)).AddUsername(afterUser.Username);
                    guildDb.Users.Find(u => u.ID.Equals(afterUser.Id)).AddNickname(afterUser);
                }
                else
                {
                    guildDb.Users.Add(new DBUser(afterUser));
                }
                guildDb.Save();
            }
        }

        public static async Task UserJoined(SocketGuildUser user)
        {
            #region Database

            var guildDb = new DBGuild(user.Guild.Id);
            if (guildDb.Users.Any(u => u.ID.Equals(user.Id))) // if already exists
            {
                guildDb.Users.Find(u => u.ID.Equals(user.Id)).AddUsername(user.Username);
            }
            else
            {
                guildDb.Users.Add(new DBUser(user));
            }
            guildDb.Save();

            #endregion Databasae

            #region Logging

            var guildConfig = GenericBot.GuildConfigs[user.Guild.Id];

            if (guildConfig.VerifiedRole == 0 || (string.IsNullOrEmpty(guildConfig.VerifiedMessage) || guildConfig.VerifiedMessage.Split().Length < 32 || !user.Guild.Roles.Any(r => r.Id == guildConfig.VerifiedRole)))
            {
                return;
            }
            string vMessage = $"Hey {user.Username}! To get verified on **{user.Guild.Name}** reply to this message with the hidden code in the message below\n\n"
                             + GenericBot.GuildConfigs[user.Guild.Id].VerifiedMessage;

            string verificationMessage =
                VerificationEngine.InsertCodeInMessage(vMessage, VerificationEngine.GetVerificationCode(user.Id, user.Guild.Id));

            try
            {
                await user.GetOrCreateDMChannelAsync().Result.SendMessageAsync(verificationMessage);
            }
            catch (Exception ex)
            {
            }


            if (guildConfig.UserLogChannelId == 0) return;

            EmbedBuilder log = new EmbedBuilder()
                .WithTitle("User Joined")
                .WithAuthor(new EmbedAuthorBuilder().WithName($"{user} ({user.Id})")
                    .WithIconUrl(user.GetAvatarUrl()))
                .WithColor(114, 137, 218)
                .WithDescription(guildConfig.UserJoinedMessage.Replace("{id}", user.Id.ToString()).Replace("{username}", user.ToString()).Replace("{mention}", user.Mention));

            if (guildConfig.UserLogTimestamp == true)
            {
                log.AddField(new EmbedFieldBuilder().WithName("Time")
                    .WithValue($"{DateTime.UtcNow.ToString(@"yyyy-MM-dd HH:mm tt")} GMT").WithIsInline(true));
            }

            if (guildConfig.UserJoinedShowModNotes == true && (DateTimeOffset.Now - user.CreatedAt).TotalDays < 7)
            {
                if (Math.Floor((DateTimeOffset.Now - user.CreatedAt).TotalDays) > 0)
                {
                    log.AddField(new EmbedFieldBuilder().WithName("New User")
                        .WithValue($"Account made `{Math.Floor((DateTimeOffset.Now - user.CreatedAt).TotalDays)}` days `{Math.Floor((double) (DateTimeOffset.Now - user.CreatedAt).Hours)}` hours ago").WithIsInline(true));
                }
                else if (Math.Floor((DateTimeOffset.Now - user.CreatedAt).TotalHours) > 0) //Days = 0
                {
                    log.AddField(new EmbedFieldBuilder().WithName("New User")
                        .WithValue($"Account made `{Math.Floor((DateTimeOffset.Now - user.CreatedAt).TotalHours)}` hours `{Math.Floor((double) (DateTimeOffset.Now - user.CreatedAt).Minutes)}` minutes ago").WithIsInline(true));
                }
                else //Days = 0 && Hours = 0
                {
                    log.AddField(new EmbedFieldBuilder().WithName("New User")
                        .WithValue($"Account made `{Math.Floor((DateTimeOffset.Now - user.CreatedAt).TotalMinutes)}` minutes `{Math.Floor((double) (DateTimeOffset.Now - user.CreatedAt).Seconds)}` seconds ago").WithIsInline(true));
                }
            }

            try
            {
                DBUser usr = guildDb.Users.First(u => u.ID.Equals(user.Id));

                if (guildConfig.UserJoinedShowModNotes && !usr.Warnings.Empty())
                {
                    log.AddField(new EmbedFieldBuilder().WithName($"{usr.Warnings.Count} Warnings")
                        .WithValue(usr.Warnings.SumAnd()));
                }
            }
            catch
            {
            }

            await user.Guild.GetTextChannel(guildConfig.UserLogChannelId).SendMessageAsync("", embed: log.Build());

            #endregion Logging
        }

        public static async Task UserChangedVc(SocketUser user, SocketVoiceState before, SocketVoiceState after)
        {
            var config = GenericBot.GuildConfigs[(user as SocketGuildUser).Guild.Id];
            KeyValuePair<ulong, ulong> chanrole;
            if (before.VoiceChannel != null)
            {
                if (GenericBot.GuildConfigs[(user as SocketGuildUser).Guild.Id].VoiceChannelRoles
                    .HasElement(kvp => kvp.Key.Equals(before.VoiceChannel.Id), out chanrole))
                {
                    try
                    {
                        (user as IGuildUser).RemoveRoleAsync((user as SocketGuildUser).Guild.Roles.
                            FirstOrDefault(r => r.Id == chanrole.Value));
                    }
                    catch (Exception e)
                    {
                        GenericBot.Logger.LogErrorMessage(e.Message +"\n"  +e.StackTrace);
                    }
                }
            }
            if (after.VoiceChannel != null)
            {
              if (GenericBot.GuildConfigs[(user as SocketGuildUser).Guild.Id].VoiceChannelRoles
                    .HasElement(kvp => kvp.Key.Equals(after.VoiceChannel.Id), out chanrole))
                {
                    try
                    {
                        (user as IGuildUser).AddRoleAsync((user as SocketGuildUser).Guild.Roles.
                            FirstOrDefault(r => r.Id == chanrole.Value));
                    }
                    catch (Exception e)
                    {
                        GenericBot.Logger.LogErrorMessage(e.Message +"\n"  +e.StackTrace);
                    }
                }
            }
        }

        public static async Task UserLeft(SocketGuildUser user)
        {
            #region Logging

            var guildConfig = GenericBot.GuildConfigs[user.Guild.Id];


            if (guildConfig.UserLogChannelId == 0) return;

            EmbedBuilder log = new EmbedBuilder()
                .WithTitle("User Left")
                .WithAuthor(new EmbedAuthorBuilder().WithName($"{user} ({user.Id})")
                    .WithIconUrl(user.GetAvatarUrl()))
                .WithColor(156, 39, 176)
                .WithDescription(guildConfig.UserLeftMessage.Replace("{id}", user.Id.ToString()).Replace("{username}", user.ToString()).Replace("{mention}", user.Mention));

            if (guildConfig.UserLogTimestamp == true)
            {
                log.AddField(new EmbedFieldBuilder().WithName("Time")
                    .WithValue($"{DateTime.UtcNow.ToString(@"yyyy-MM-dd HH:mm tt")} GMT").WithIsInline(true));
            }

            #endregion Logging
        }
    }
}
