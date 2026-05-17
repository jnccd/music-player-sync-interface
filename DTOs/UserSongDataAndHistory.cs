using MusicPlayerSyncInterface.DTOs;

public record UserSongDataAndHistory(User[] users, UpvotedSong[] songs, SongHistoryEntry[] historyEntries);