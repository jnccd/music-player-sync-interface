namespace MusicPlayerSyncInterface.DTOs.Composites;

public record SyncPullResult(User[] users, UpvotedSong[] songs, SongHistoryEntry[] historyEntries);