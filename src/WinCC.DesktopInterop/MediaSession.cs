using Windows.Media.Control;
using Windows.Media;
using Windows.Storage.Streams;

namespace WinCC.DesktopInterop;

public sealed record MediaReading(string Title, string Artist, bool Playing, bool CanToggle, bool CanNext,
    bool CanPrevious, double Position, double Duration, bool CanSeek, bool CanShuffle, bool CanRepeat,
    bool Shuffle, string Repeat, byte[]? Artwork);

/// <summary>Opt-in local Spotify session access. No Spotify login, token or Web API.</summary>
public sealed class MediaSession
{
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private GlobalSystemMediaTransportControlsSession? session;
    private string? artworkIdentity;
    private byte[]? artwork;
    private TimeSpan timelineStart;
    public async Task<MediaReading?> Read()
    {
        manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        session = manager.GetSessions().FirstOrDefault(s => s.SourceAppUserModelId.Contains("spotify", StringComparison.OrdinalIgnoreCase));
        if (session is null) return null;
        var properties = await session.TryGetMediaPropertiesAsync();
        var playback = session.GetPlaybackInfo(); var timeline = session.GetTimelineProperties();
        timelineStart = timeline.StartTime;
        string identity = properties.Title + "\n" + properties.Artist + "\n" + properties.AlbumTitle;
        if (identity != artworkIdentity)
        {
            artworkIdentity = identity; artwork = null;
            if (properties.Thumbnail is not null)
            {
                try
                {
                    using var stream = await properties.Thumbnail.OpenReadAsync();
                    if (stream.Size is > 0 and <= 2 * 1024 * 1024)
                    {
                        using var reader = new DataReader(stream);
                        uint count = await reader.LoadAsync((uint)stream.Size);
                        var bytes = new byte[count]; reader.ReadBytes(bytes); artwork = bytes;
                    }
                }
                catch (Exception e) when (e is System.Runtime.InteropServices.COMException or IOException or UnauthorizedAccessException)
                { artwork = null; }
            }
        }
        return new(properties.Title, properties.Artist,
            playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
            playback.Controls.IsPlayPauseToggleEnabled, playback.Controls.IsNextEnabled, playback.Controls.IsPreviousEnabled,
            Math.Max(0, (timeline.Position - timeline.StartTime).TotalSeconds), Math.Max(0, timeline.EndTime.TotalSeconds - timeline.StartTime.TotalSeconds),
            playback.Controls.IsPlaybackPositionEnabled, playback.Controls.IsShuffleEnabled, playback.Controls.IsRepeatEnabled,
            playback.IsShuffleActive ?? false, (playback.AutoRepeatMode ?? MediaPlaybackAutoRepeatMode.None).ToString(), artwork);
    }
    public async Task<bool> Toggle() => session is not null && await session.TryTogglePlayPauseAsync();
    public async Task<bool> Next() => session is not null && await session.TrySkipNextAsync();
    public async Task<bool> Previous() => session is not null && await session.TrySkipPreviousAsync();
    public async Task<bool> Seek(double seconds) => session is not null && await session.TryChangePlaybackPositionAsync((timelineStart + TimeSpan.FromSeconds(seconds)).Ticks);
    public async Task<bool> ToggleShuffle() => session is not null &&
        await session.TryChangeShuffleActiveAsync(!(session.GetPlaybackInfo().IsShuffleActive ?? false));
    public async Task<bool> CycleRepeat()
    {
        if (session is null) return false;
        var next = session.GetPlaybackInfo().AutoRepeatMode switch
        {
            MediaPlaybackAutoRepeatMode.None => MediaPlaybackAutoRepeatMode.List,
            MediaPlaybackAutoRepeatMode.List => MediaPlaybackAutoRepeatMode.Track,
            _ => MediaPlaybackAutoRepeatMode.None
        };
        return await session.TryChangeAutoRepeatModeAsync(next);
    }
}
