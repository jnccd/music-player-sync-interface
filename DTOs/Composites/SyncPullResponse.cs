namespace MusicPlayerSyncInterface.DTOs.Composites;

public record SyncPullResponse(User[] Users, UpvotedSong[] Songs, SongHistoryEntry[] HistoryEntries);