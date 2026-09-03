namespace MusicPlayerSyncInterface.DTOs.Composites;

public record SyncPullResponse(User User, UpvotedSong[] Songs, SongHistoryEntry[] HistoryEntries, SongLibraryMigration[] Migrations);