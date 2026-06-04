namespace MusicPlayerSyncInterface.DTOs.Composites;

public record UserSongDataAndHistory(User[] users, UpvotedSong[] songs, SongHistoryEntry[] historyEntries);