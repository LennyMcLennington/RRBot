﻿#pragma warning disable RCS1163, IDE0060 // both warnings fire for events, which they shouldn't
namespace RRBot.Systems;
public sealed class AudioSystem
{
    private readonly LavaRestClient lavaRestClient;
    private readonly LavaSocketClient lavaSocketClient;

    public AudioSystem(LavaRestClient rest, LavaSocketClient socket)
    {
        lavaRestClient = rest;
        lavaSocketClient = socket;
    }

    public async Task<RuntimeResult> GetCurrentlyPlayingAsync(SocketCommandContext context)
    {
        LavaPlayer player = lavaSocketClient.GetPlayer(context.Guild.Id);
        if (player?.IsPlaying == true)
        {
            LavaTrack track = player.CurrentTrack;

            StringBuilder builder = new($"By: {track.Author}\n");
            if (!track.IsStream)
            {
                TimeSpan pos = new(track.Position.Hours, track.Position.Minutes, track.Position.Seconds);
                builder.AppendLine($"Length: {track.Length}\nPosition: {pos}");
            }

            EmbedBuilder embed = new EmbedBuilder()
                .WithColor(Color.Red)
                .WithTitle(track.Title)
                .WithDescription(builder.ToString());
            await context.Channel.SendMessageAsync(embed: embed.Build());
            return CommandResult.FromSuccess();
        }

        return CommandResult.FromError("There is no currently playing track.");
    }

    public async Task<RuntimeResult> PlayAsync(SocketCommandContext context, string query)
    {
        SocketGuildUser user = context.User as SocketGuildUser;
        if (user.VoiceChannel is null) return CommandResult.FromError("You must be in a voice channel.");

        await lavaSocketClient.ConnectAsync(user.VoiceChannel);
        LavaPlayer player = lavaSocketClient.GetPlayer(context.Guild.Id);

        if (player is null)
        {
            await lavaSocketClient.ConnectAsync(player.VoiceChannel);
            player = lavaSocketClient.GetPlayer(context.Guild.Id);
        }

        Victoria.Entities.SearchResult search = await lavaRestClient.SearchYouTubeAsync(query);
        if (search.LoadType == LoadType.NoMatches || search.LoadType == LoadType.LoadFailed)
            return CommandResult.FromError("No results were found for your query.");
        LavaTrack track = search.Tracks.FirstOrDefault();

        if (!track.IsStream && track.Length.TotalSeconds > 7200)
            return CommandResult.FromError("This is too long for me to play! It must be 2 hours or shorter in length.");

        if (player.CurrentTrack != null && player.IsPlaying)
        {
            await context.Channel.SendMessageAsync($"**{track.Title}** has been added to the queue.");
            player.Queue.Enqueue(track);
            return CommandResult.FromSuccess();
        }

        await player.PlayAsync(track);

        StringBuilder message = new($"Now playing: {track.Title}\nBy: {track.Author}\n");
        if (!track.IsStream) message.AppendLine($"Length: {track.Length}");
        message.AppendLine("*Tip: if the track instantly doesn't play, it's probably age restricted.*");

        await context.Channel.SendMessageAsync(message.ToString());

        await LoggingSystem.Custom_TrackStarted(user, track.Uri);

        return CommandResult.FromSuccess();
    }

    public async Task<RuntimeResult> ListAsync(SocketCommandContext context)
    {
        LavaPlayer player = lavaSocketClient.GetPlayer(context.Guild.Id);
        if (player?.IsPlaying == true)
        {
            if (player.Queue.Count < 1 && player.CurrentTrack != null)
            {
                await context.Channel.SendMessageAsync($"Now playing: {player.CurrentTrack.Title}. Nothing else is queued.");
                return CommandResult.FromSuccess();
            }

            StringBuilder playlist = new();
            for (int i = 0; i < player.Queue.Items.Count(); i++)
            {
                LavaTrack track = player.Queue.Items.ElementAt(i) as LavaTrack;
                playlist.AppendLine($"**{i + 1}**: {track.Title} by {track.Author}");
                if (!track.IsStream) playlist.AppendLine($" ({track.Length})");
            }

            EmbedBuilder embed = new EmbedBuilder()
                .WithColor(Color.Red)
                .WithTitle("Playlist")
                .WithDescription(playlist.ToString());
            await context.Channel.SendMessageAsync(embed: embed.Build());
            return CommandResult.FromSuccess();
        }

        return CommandResult.FromError("There are no tracks to list.");
    }

    public async Task<RuntimeResult> SkipTrackAsync(SocketCommandContext context)
    {
        LavaPlayer player = lavaSocketClient.GetPlayer(context.Guild.Id);
        if (player != null)
        {
            if (player.Queue.Count >= 1)
            {
                LavaTrack track = player.Queue.Items.FirstOrDefault() as LavaTrack;
                await player.PlayAsync(track);

                StringBuilder message = new($"Now playing: {track.Title}\nBy: {track.Author}\n");
                if (!track.IsStream) message.Append($"Length: {track.Length}");

                await context.Channel.SendMessageAsync(message.ToString());
            }
            else
            {
                await context.Channel.SendMessageAsync("Current track skipped!");
                await lavaSocketClient.DisconnectAsync(player.VoiceChannel);
                await player.StopAsync();
            }

            return CommandResult.FromSuccess();
        }

        return CommandResult.FromError("There are no tracks to skip.");
    }

    public async Task<RuntimeResult> StopAsync(SocketCommandContext context)
    {
        LavaPlayer player = lavaSocketClient.GetPlayer(context.Guild.Id);
        if (player is null) return CommandResult.FromError("The bot is not currently being used.");

        if (player.IsPlaying) await player.StopAsync();
        player.Queue.Clear();
        await lavaSocketClient.DisconnectAsync(player.VoiceChannel);

        await context.Channel.SendMessageAsync("Stopped playing the current track and removed any existing tracks in the queue.");
        return CommandResult.FromSuccess();
    }

    public async Task<RuntimeResult> ChangeVolumeAsync(SocketCommandContext context, int volume)
    {
        if (volume < Constants.MIN_VOLUME || volume > Constants.MAX_VOLUME)
            return CommandResult.FromError($"Volume must be between {Constants.MIN_VOLUME}% and {Constants.MAX_VOLUME}%.");

        LavaPlayer player = lavaSocketClient.GetPlayer(context.Guild.Id);
        if (player is null) return CommandResult.FromError("The bot is not currently being used.");

        await player.SetVolumeAsync(volume);
        await context.Channel.SendMessageAsync($"Set volume to {volume}%.");
        return CommandResult.FromSuccess();
    }

    // this is a fix for the player breaking if the bot is manually disconnected
    public async Task OnPlayerUpdated(LavaPlayer player, LavaTrack track, TimeSpan duration)
    {
        if (!track.IsStream)
        {
            IEnumerable<IGuildUser> members = await player.VoiceChannel.GetUsersAsync().FlattenAsync();
            if (!members.Any(member => member.IsBot) && track.Position.TotalSeconds > 5)
            {
                await lavaSocketClient.DisconnectAsync(player.VoiceChannel);
                await player.StopAsync();
            }
        }
    }

    public async Task OnTrackFinished(LavaPlayer player, LavaTrack track, TrackEndReason reason)
    {
        if (player.Queue.Count > 0 && !reason.ShouldPlayNext())
        {
            player.Queue.Dequeue();
            return;
        }

        if (!player.Queue.TryDequeue(out IQueueObject item) || item is not LavaTrack nextTrack || !reason.ShouldPlayNext())
        {
            await lavaSocketClient.DisconnectAsync(player.VoiceChannel);
            await player.StopAsync();
        }
        else
        {
            await player.PlayAsync(nextTrack);

            StringBuilder message = new($"Now playing: {track.Title}\nBy: {track.Author}\n");
            if (!track.IsStream) message.Append($"Length: {track.Length}");
            await player.TextChannel.SendMessageAsync(message.ToString());
        }
    }
}