namespace RRBot.Systems;
public class MonitorSystem
{
    private readonly DiscordSocketClient client;
    private readonly FirestoreDb database;

    public MonitorSystem(DiscordSocketClient client, FirestoreDb database)
    {
        this.client = client;
        this.database = database;
    }

    public async Task Initialise()
    {
        await Task.Factory.StartNew(async () => await StartBanMonitorAsync());
        await Task.Factory.StartNew(async () => await StartChillMonitorAsync());
        await Task.Factory.StartNew(async () => await StartMuteMonitorAsync());
        await Task.Factory.StartNew(async () => await StartPerkMonitorAsync());
    }

    private async Task StartBanMonitorAsync()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(30));
            foreach (SocketGuild guild in client.Guilds)
            {
                QuerySnapshot bans = await database.Collection($"servers/{guild.Id}/bans").GetSnapshotAsync();
                foreach (DocumentSnapshot ban in bans.Documents)
                {
                    long timestamp = ban.GetValue<long>("Time");
                    ulong userId = Convert.ToUInt64(ban.Id);

                    if (!(await guild.GetBansAsync()).Any(ban => ban.User.Id == userId))
                    {
                        await ban.Reference.DeleteAsync();
                        continue;
                    }

                    if (timestamp <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                    {
                        await guild.RemoveBanAsync(userId);
                        await ban.Reference.DeleteAsync();
                    }
                }
            }
        }
    }

    private async Task StartChillMonitorAsync()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(30));
            foreach (SocketGuild guild in client.Guilds)
            {
                QuerySnapshot chills = await database.Collection($"servers/{guild.Id}/chills").GetSnapshotAsync();
                foreach (DocumentSnapshot chill in chills.Documents)
                {
                    long timestamp = chill.GetValue<long>("Time");
                    SocketTextChannel channel = guild.GetTextChannel(Convert.ToUInt64(chill.Id));
                    OverwritePermissions perms = channel.GetPermissionOverwrite(guild.EveryoneRole) ?? OverwritePermissions.InheritAll;

                    if (perms.SendMessages != PermValue.Deny)
                    {
                        await chill.Reference.DeleteAsync();
                        continue;
                    }

                    if (timestamp <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                    {
                        await channel.AddPermissionOverwriteAsync(guild.EveryoneRole, perms.Modify(sendMessages: PermValue.Inherit));
                        await channel.SendMessageAsync("This channel has thawed out! Continue the chaos!");
                        await chill.Reference.DeleteAsync();
                    }
                }
            }
        }
    }

    private async Task StartMuteMonitorAsync()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(30));
            foreach (SocketGuild guild in client.Guilds)
            {
                QuerySnapshot mutes = await database.Collection($"servers/{guild.Id}/mutes").GetSnapshotAsync();
                if (mutes.Count > 0)
                {
                    DbConfigRoles roles = await DbConfigRoles.GetById(guild.Id);
                    foreach (DocumentSnapshot mute in mutes.Documents)
                    {
                        long timestamp = mute.GetValue<long>("Time");
                        SocketGuildUser user = guild.GetUser(Convert.ToUInt64(mute.Id));

                        if (timestamp <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                        {
                            if (user != null) await user.RemoveRoleAsync(roles.MutedRole);
                            await mute.Reference.DeleteAsync();
                        }
                    }
                }
            }
        }
    }

    private async Task StartPerkMonitorAsync()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(30));
            foreach (SocketGuild guild in client.Guilds)
            {
                QuerySnapshot usersWPerks = await database.Collection($"servers/{guild.Id}/users")
                    .WhereNotEqualTo("Perks", null).WhereNotEqualTo("Perks", new Dictionary<string, long>()).GetSnapshotAsync();
                foreach (DocumentSnapshot snap in usersWPerks.Documents)
                {
                    ulong userId = Convert.ToUInt64(snap.Id);
                    DbUser user = await DbUser.GetById(guild.Id, userId);
                    foreach (KeyValuePair<string, long> kvp in user.Perks)
                    {
                        if (kvp.Value <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() && kvp.Key != "Pacifist")
                        {
                            user.Perks.Remove(kvp.Key);
                            if (kvp.Key == "Multiperk" && user.Perks.Count >= 2)
                            {
                                string lastPerk = user.Perks.Last().Key;
                                Perk perk = Array.Find(ItemSystem.perks, p => p.name == lastPerk);
                                SocketUser socketUser = guild.GetUser(userId);
                                await user.SetCash(socketUser, user.Cash + perk.price);
                                user.Perks.Remove(lastPerk);
                            }
                        }
                    }
                }
            }
        }
    }
}