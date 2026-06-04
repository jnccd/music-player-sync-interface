namespace MusicPlayerSyncInterface.DTOs.Composites;

public record SyncInitRequest(UpvotedSong[] Songs, SongHistoryEntry[] HistoryEntries);