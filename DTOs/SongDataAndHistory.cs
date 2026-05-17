using MusicPlayerSyncInterface.DTOs;

public record SongDataAndHistory(UpvotedSong[] songs, SongHistoryEntry[] historyEntries);