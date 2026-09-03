namespace MusicPlayerSyncInterface.DTOs;

public enum SongLibraryMigrationType
{
    Rename,
    Delete
}

/// <summary>
/// A song library migration describes a change that was done to a song library of a client.
/// The upvotedSong table only references songs by name (and artist/album metadata), while the actual
/// song file lives in the song library folder of the client. Because multiple clients can share the same
/// song library (e.g. via a mounted NAS folder), a rename or deletion of a song file has to be tracked
/// so other clients can apply it to their local library as well.
/// Migrations are numbered per user in ascending order. The number is assigned by the server when the
/// migration is created, which is why a client should only apply a migration locally (e.g. rename the
/// actual file) if it has a working connection to the server and the migration POST went through.
/// A clients song library contains a ".song-library.music-player-config" file that stores the number of
/// the last migration that was applied to that library, so only newer migrations need to be applied.
/// </summary>
public class SongLibraryMigration(string OldName, string NewName, SongLibraryMigrationType MigrationType, string UserId = "")
{
    /// <summary>
    /// Unique id of the migration. Created by the client that initiates the migration so the server can
    /// deduplicate retried requests.
    /// </summary>
    public Guid MigrationId { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = UserId;

    /// <summary>
    /// The id of the UpvotedSong entry this migration refers to. A migration always describes a change to
    /// one specific song entry (and the song file(s) that belong to it), even if other entries share the
    /// same file name. Set by the client when the migration is created; must not be Guid.Empty.
    /// </summary>
    public Guid SongId { get; set; } = Guid.Empty;

    /// <summary>
    /// Assigned by the server. Numbers ascend per user, so a client can keep track of which migrations it
    /// already applied to its song library with a single integer (see the .song-library.music-player-config file).
    /// </summary>
    public int MigrationNumber { get; set; } = 0;

    // Serialized as a plain number so both System.Text.Json and Newtonsoft.Json clients can read it.
    public SongLibraryMigrationType MigrationType { get; set; } = MigrationType;

    /// <summary>
    /// The old file name of the song in the song library, including the extension, like "Cool Artist - Nice Song.mp3".
    /// For Delete migrations this is the file name that should be deleted.
    /// </summary>
    public string OldName { get; set; } = OldName;

    /// <summary>
    /// The new file name of the song in the song library, including the extension, like "Cool Artist - Even Nicer Song.mp3".
    /// Empty for Delete migrations.
    /// </summary>
    public string NewName { get; set; } = NewName;

    /// <summary>
    /// Snapshot of the album of the referenced upvotedSong entry at the time the migration was created.
    /// Filled in by the server. Since a file rename or delete does not change the tags of the song file,
    /// clients can use it to identify which files in the song library belong to this entry (a file with the
    /// same name but different album/artist tags is a different song). Empty when the entry had no album.
    /// </summary>
    public string Album { get; set; } = "";

    /// <summary>
    /// Snapshot of the artist(s) of the referenced upvotedSong entry at the time the migration was created
    /// (same " + "-joined convention as the database rows). Filled in by the server. Empty when the entry
    /// had no artist; in that case files can only be identified by their file name.
    /// </summary>
    public string Artist { get; set; } = "";
}
