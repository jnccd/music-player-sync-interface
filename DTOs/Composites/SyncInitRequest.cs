namespace MusicPlayerSyncInterface.DTOs.Composites;

public record SyncInitRequest(User[] users, UpvotedSong[] songs, SongHistoryEntry[] historyEntries);