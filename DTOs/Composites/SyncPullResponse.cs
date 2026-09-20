namespace MusicPlayerSyncInterface.DTOs.Composites;

/// <summary>
/// The data of a /sync/pull response. <see cref="Songs"/> is always the complete list of the account
/// (small, and the clients reconcile it against their local rows), while <see cref="HistoryEntries"/> is
/// either the complete history or - when the client sent its <c>historySince</c> cursor and the server
/// honored it - only the entries after that cursor (<see cref="IsIncremental"/>).
/// The cursor fields are optional so that older servers (which do not send them) and older clients
/// (which do not read them) keep working unchanged: a response without them simply looks like a full
/// pull with an unknown cursor.
/// </summary>
/// <param name="User">The authenticated user the data belongs to.</param>
/// <param name="Songs">The complete list of the account's songs.</param>
/// <param name="HistoryEntries">The complete history, or the delta when <paramref name="IsIncremental"/> is true.</param>
/// <param name="Migrations">The song library migrations of the account (clients apply them incrementally already).</param>
/// <param name="IsIncremental">True when <paramref name="HistoryEntries"/> is a delta (append it) instead of the full history (replace it).</param>
/// <param name="HistorySequence">The server's history cursor at response time; the client sends it back as <c>historySince</c>.</param>
/// <param name="TotalHistoryCount">How many history entries the account has on the server, for the client's consistency check.</param>
/// <param name="ResyncRequired">True when the server could not honor the client's cursor (e.g. its database was restored) and a full pull should be made.</param>
public record SyncPullResponse(
    User User,
    UpvotedSong[] Songs,
    SongHistoryEntry[] HistoryEntries,
    SongLibraryMigration[] Migrations,
    bool IsIncremental = false,
    long HistorySequence = 0,
    int TotalHistoryCount = 0,
    bool ResyncRequired = false);
