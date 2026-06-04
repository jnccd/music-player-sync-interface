namespace MusicPlayerSyncInterface.DTOs.Composites;

public record SyncInitRequest(User[] Users, UpvotedSong[] Songs, SongHistoryEntry[] HistoryEntries);