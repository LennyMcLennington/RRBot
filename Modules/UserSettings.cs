﻿using Discord;
using Discord.Commands;
using RRBot.Entities;
using RRBot.Extensions;
using System.Threading.Tasks;

namespace RRBot.Modules
{
    [Summary("Choose how you want me to bug you. I can do it in DM, I can do it when you rank up, and I can even ping you, too.")]
    public class UserSettings : ModuleBase<SocketCommandContext>
    {
        private async Task GenericSet(string property, bool status)
        {
            DbUser user = await DbUser.GetById(Context.Guild.Id, Context.User.Id);
            user[property] = status;
            await user.Write();
        }

        [Command("mysettings")]
        [Summary("List your user settings.")]
        [Remarks("$mysettings")]
        public async Task MySettings()
        {
            DbUser user = await DbUser.GetById(Context.Guild.Id, Context.User.Id);
            EmbedBuilder embed = new()
            {
                Color = Color.Red,
                Title = "Your Settings",
                Description = $"**DM Notifications**: {user.DMNotifs}\n**No Reply Pings**: {user.NoReplyPings}\n" +
                    $"**Rankup Notifications**: {user.RankupNotifs}"
            };
            await ReplyAsync(embed: embed.Build());
        }

        [Alias("setdmnotifs")]
        [Command("setdmnotifications")]
        [Summary("Set whether or not you will be DM'd by commands/general notifications that support it. *(default: false)*")]
        [Remarks("$setdmnotifications [true/false]")]
        public async Task SetDMNotifications(bool status)
        {
            await GenericSet("DMNotifs", status);
            await Context.User.NotifyAsync(Context.Channel, $"You will {(status ? "now see" : "no longer see")} DM notifications.");
        }

        [Command("setnoreplypings")]
        [Summary("Set whether or not you will be pinged in command responses. *(default: false)*")]
        [Remarks("$setnoreplypings [true/false]")]
        public async Task SetNoReplyPings(bool status)
        {
            await GenericSet("NoReplyPings", status);
            await Context.User.NotifyAsync(Context.Channel, $"You will {(status ? "no longer be" : "now be")} pinged in command responses.");
        }

        [Alias("setrankupnotifs")]
        [Command("setrankupnotifications")]
        [Summary("Set whether or not you will be notified of rank-ups/deranks. *(default: false)*")]
        [Remarks("$setrankupnotifications [true/false]")]
        public async Task SetRankupNotifications(bool status)
        {
            await GenericSet("RankupNotifs", status);
            await Context.User.NotifyAsync(Context.Channel, $"You will {(status ? "now see" : "no longer see")} rankup notifications.");
        }
    }
}
