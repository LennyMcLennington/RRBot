﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Google.Cloud.Firestore;
using RRBot.Preconditions;
using RRBot.Systems;

namespace RRBot.Modules
{
    public class Crime : ModuleBase<SocketCommandContext>
    {
        [Command("bully")]
        [Summary("Change the nickname of any victim you wish!")]
        [Remarks("``$bully [user] [nickname]``")]
        public async Task<RuntimeResult> Bully(IGuildUser user, [Remainder] string nickname)
        {
            if (user.IsBot) return CommandResult.FromError("Nope.");

            DocumentReference doc = Program.database.Collection($"servers/{Context.Guild.Id}/config").Document("roles");
            DocumentSnapshot snap = await doc.GetSnapshotAsync();
            if (snap.TryGetValue("houseRole", out ulong staffId))
            {
                if (user.Id == Context.User.Id) return CommandResult.FromError($"{Context.User.Mention}, no masochism here!");
                if (user.IsBot || user.RoleIds.Contains(staffId)) return CommandResult.FromError($"{Context.User.Mention}, you cannot bully someone who is a bot or staff member.");
                if (Global.niggerRegex.Matches(new string(nickname.Where(char.IsLetter).ToArray()).ToLower()).Count != 0) 
                    return CommandResult.FromError($"{Context.User.Mention}, you cannot bully someone to the funny word.");
                if (nickname.Length > 32) return CommandResult.FromError($"{Context.User.Mention}, the bully nickname is longer than the maximum accepted length.");

                await user.ModifyAsync(props => { props.Nickname = nickname; });
                await Program.logger.Custom_UserBullied(user, Context.User, nickname);
                await ReplyAsync($"**{Context.User.ToString()}** has **BULLIED** **{user.ToString()}** to ``{nickname}``!");
                return CommandResult.FromSuccess();
            }

            return CommandResult.FromError("This server's staff role has yet to be set.");
        }

        [Command("loot")]
        [Summary("Loot some locations.")]
        [Remarks("``$loot``")]
        [RequireCash]
        [RequireCooldown("lootCooldown", "you cannot loot for {0}.")]
        public async Task Loot()
        {
            DocumentReference doc = Program.database.Collection($"servers/{Context.Guild.Id}/users").Document(Context.User.Id.ToString());
            DocumentSnapshot snap = await doc.GetSnapshotAsync();
            float cash = snap.GetValue<float>("cash");
            Random random = new Random();

            if (random.Next(10) > 7)
            {
                float lostCash = (float)Math.Round(100 + (1000 - 100) * random.NextDouble(), 2); // lose between $100-1000

                await CashSystem.SetCash(Context.User as IGuildUser, cash - lostCash);
                switch (random.Next(2))
                {
                    case 0:
                        await ReplyAsync($"{Context.User.Mention}, there happened to be a cop coming out of the donut shop next door. You had to pay **${lostCash}** in fines.");
                        break;
                    case 1:
                        await ReplyAsync($"{Context.User.Mention}, the manager gave no fucks and beat the **SHIT** out of you. You lost **${lostCash}** paying for face stitches.");
                        break;
                }
            }
            else
            {
                float moneyLooted = (float)Math.Round(100 + (550 - 100) * random.NextDouble(), 2); // gain between $100-550
                switch (random.Next(3))
                {
                    case 0:
                        await ReplyAsync($"{Context.User.Mention}, you joined your local BLM protest, looted a Footlocker, and sold what you got. You earned **${moneyLooted}**.");
                        break;
                    case 1:
                        await ReplyAsync($"{Context.User.Mention}, that mall had a lot of shit! You earned **${moneyLooted}**.");
                        break;
                    case 2:
                        moneyLooted /= 10;
                        moneyLooted = (float)Math.Round(moneyLooted, 2);
                        await ReplyAsync($"{Context.User.Mention}, you stole from a gas station because you're a fucking idiot. You earned **${moneyLooted}**, basically nothing.");
                        break;
                }
                await CashSystem.SetCash(Context.User as IGuildUser, cash + moneyLooted);
            }

            await doc.SetAsync(new { lootCooldown = Global.UnixTime(3600) }, SetOptions.MergeAll);
        }

        [Alias("strugglesnuggle")]
        [Command("rape")]
        [Summary("Go out on the prowl for some ass!")]
        [Remarks("``$rape [user]``")]
        [RequireCash(500f)]
        [RequireCooldown("rapeCooldown", "you cannot rape for {0}.")]
        public async Task<RuntimeResult> Rape(IGuildUser user)
        {
            if (user.IsBot) return CommandResult.FromError("Nope.");
            if (user.Id == Context.User.Id) return CommandResult.FromError($"{Context.User.Mention}, how are you supposed to rape yourself?");

            DocumentReference aDoc = Program.database.Collection($"servers/{Context.Guild.Id}/users").Document(Context.User.Id.ToString());
            DocumentSnapshot aSnap = await aDoc.GetSnapshotAsync();
            float aCash = aSnap.GetValue<float>("cash");

            DocumentReference tDoc = Program.database.Collection($"servers/{Context.Guild.Id}/users").Document(user.Id.ToString());
            DocumentSnapshot tSnap = await tDoc.GetSnapshotAsync();
            float tCash = tSnap.GetValue<float>("cash");
            if (tCash > 0)
            {
                Random random = new Random();
                double rapePercent = 5 + (8 - 5) * random.NextDouble(); // lose/gain between 5-8% depending on outcome
                if (random.Next(10) > 4)
                {
                    float repairs = (float)Math.Round(tCash / 100.0 * rapePercent, 2);
                    await CashSystem.SetCash(user, tCash - repairs);
                    await ReplyAsync($"{Context.User.Mention}, you fucking **DEMOLISHED** **{user.ToString()}**'s asshole! They just paid **${repairs}** in asshole repairs.");
                }
                else
                {
                    float repairs = (float)Math.Round(aCash / 100.0 * rapePercent, 2);
                    await CashSystem.SetCash(Context.User as IGuildUser, aCash - repairs);
                    await ReplyAsync($"{Context.User.Mention}, you just got **COUNTER-RAPED** by **{user.ToString()}**! YOU just paid **${repairs}** in asshole repairs.");
                }

                await aDoc.SetAsync(new { rapeCooldown = Global.UnixTime(3600) }, SetOptions.MergeAll);
                return CommandResult.FromSuccess();
            }

            return CommandResult.FromError($"{Context.User.Mention} Jesus man, talk about kicking them while they're down! **{user.ToString()}** is broke! Have some decency.");
        }

        /*
        [Command("rob")]
        [Summary("Rob another user for some money.. perhaps.")]
        [Remarks("``$rob [user] [money]")]
        [RequireCash]
        [RequireCooldown("robCooldown", "you cannot rob someone for {0}.")]
        public async Task<RuntimeResult> Rob(IGuildUser user, float amount)
        {
            if (user.IsBot) return CommandResult.FromError("Nope.");
            if (amount <= 0) return CommandResult.FromError($"{Context.User.Mention}, you can't rob for negative or no money!");

            DocumentReference aDoc = Program.database.Collection($"servers/{Context.Guild.Id}/users").Document(Context.User.Id.ToString());
            DocumentSnapshot aSnap = await aDoc.GetSnapshotAsync();
            float aCash = aSnap.GetValue<float>("cash");

            DocumentReference tDoc = Program.database.Collection($"servers/{Context.Guild.Id}/users").Document(user.Id.ToString());
            DocumentSnapshot tSnap = await tDoc.GetSnapshotAsync();
            if (tSnap.TryGetValue("cash", out float tCash))
            {
                if (tCash < amount) return CommandResult.FromError($"{Context.User.Mention}, they don't have ${amount} or more!");

                Random random = new Random();

            }

            return CommandResult.FromError($"{Context.User.Mention}, they're broke!");        
        }
        */

        [Command("whore")]
        [Summary("Sell your body for quick cash.")]
        [Remarks("``$whore``")]
        [RequireCash]
        [RequireCooldown("whoreCooldown", "you cannot whore yourself out for {0}.")]
        public async Task Whore()
        {
            DocumentReference doc = Program.database.Collection($"servers/{Context.Guild.Id}/users").Document(Context.User.Id.ToString());
            DocumentSnapshot snap = await doc.GetSnapshotAsync();
            float cash = snap.GetValue<float>("cash");
            Random random = new Random();

            if (random.Next(10) > 7)
            {
                float lostCash = (float)Math.Round(69 + (690 - 69) * random.NextDouble(), 2); // lose between $69-690

                await CashSystem.SetCash(Context.User as IGuildUser, cash - lostCash);
                switch (random.Next(2))
                {
                    case 0:
                        await ReplyAsync($"{Context.User.Mention}, you were too ugly and nobody wanted you. You lost **${lostCash}** buying clothes for the night.");
                        break;
                    case 1:
                        await ReplyAsync($"{Context.User.Mention}, you didn't give good enough head to the cop! You had to pay **${lostCash}** in fines.");
                        break;
                }
            }
            else
            {
                float moneyWhored = (float)Math.Round(69 + (550 - 69) * random.NextDouble(), 2); // gain between $69-550
                switch (random.Next(3))
                {
                    case 0:
                        await ReplyAsync($"{Context.User.Mention}, you went to the club and some weird fat dude sauced you **${moneyWhored}**.");
                        break;
                    case 1:
                        await ReplyAsync($"{Context.User.Mention}, the dude you fucked looked super shady, but he did pay up. You earned **${moneyWhored}**.");
                        break;
                    case 2:
                        await ReplyAsync($"{Context.User.Mention}, you found the Chad Thundercock himself! **${moneyWhored}** and some amazing sex. What a great night.");
                        break;
                }
                await CashSystem.SetCash(Context.User as IGuildUser, cash + moneyWhored);
            }

            await doc.SetAsync(new { whoreCooldown = Global.UnixTime(3600) }, SetOptions.MergeAll);
        }
    }
}
