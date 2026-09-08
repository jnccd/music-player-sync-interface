using MusicPlayerSyncInterface.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicPlayerSyncInterface;

/// <summary>
/// Matching between song files in a song library and upvotedSong entries.
/// A songs identity is its file name plus its album/artist tags (album, album artists joined with " + ").
/// Because multiple clients and the migration appliers all need these rules, they live here in the
/// interface project instead of being reimplemented in every client.
/// Note: reading the tags of a file needs TagLib, which this project deliberately does not reference;
/// the callers read the tags and pass them in (see <see cref="ResolveUpvotedSongEntry"/>).
/// </summary>
public static class SongFileMatching
{
    /// <summary>
    /// True when the song carries no album/artist metadata (empty strings). Such entries can only be
    /// identified by their file name.
    /// </summary>
    public static bool HasNoAlbumOrArtist(string artist, string album) =>
        string.IsNullOrWhiteSpace(artist) && string.IsNullOrWhiteSpace(album);

    /// <summary>
    /// True when the tags of a song file are equal to the given album/artist (same " + "-joined artists
    /// convention everywhere in this codebase).
    /// </summary>
    public static bool TagsEqual(string artist, string album, string fileArtist, string fileAlbum) =>
        artist == fileArtist && album == fileAlbum;

    /// <summary>
    /// True when the tags of a song file are equal to the album/artist of an entry, using the same
    /// convention everywhere in this codebase (album string, artists joined with " + ").
    /// An entry without album/artist metadata matches any file with the same name.
    /// </summary>
    public static bool EntryMatchesFileTags(UpvotedSong entry, string fileArtist, string fileAlbum) =>
        HasNoAlbumOrArtist(entry.Artist, entry.Album) ||
        TagsEqual(entry.Artist, entry.Album, fileArtist, fileAlbum);

    /// <summary>
    /// True when both entries denote the very same song: same file name and same album/artist tags.
    /// Entries without album/artist metadata are only equal to other entries without metadata of the
    /// same name - a metadata-less entry is deliberately never considered the same song as a tagged one,
    /// since the tags cannot prove that (a file name alone is not unique in a library).
    /// This is the identity the server unique index and the duplicate merging work with.
    /// </summary>
    public static bool EntriesAreDuplicates(UpvotedSong a, UpvotedSong b) =>
        a.Name == b.Name && a.Artist == b.Artist && a.Album == b.Album;

    /// <summary>
    /// True when the row carries user-built song data that is expensive (or impossible) to recreate:
    /// a score history, likes/dislikes, a streak, or an analyzed volume. Such rows must never be the
    /// row dropped by a merge - mp3 metadata can be copied onto them later, the data cannot.
    /// </summary>
    public static bool CarriesSongData(UpvotedSong entry) =>
        entry.TotalLikes != 0 || entry.TotalDislikes != 0 || entry.Streak != 0 || entry.Score != 0f || entry.Volume > 0f;

    /// <summary>
    /// Chooses the entry of a group of entries that all belong to one and the same song that should be
    /// kept (and returned when the group is resolved as one song). Deterministic so the server, the
    /// clients and the file matching all agree on the same row:
    /// 1. Entries carrying user-built data (score, likes/dislikes, streak, analyzed volume) win. Such
    ///    data is accumulated from user input over time and cannot be recreated, while the album/artist
    ///    metadata of the arbitrating file can be copied onto the winner afterwards.
    /// 2. Among data-carrying entries: more votes first, then a bigger streak, then an analyzed volume.
    /// 3. Among entries without data: those carrying exactly the album/artist of the file the song was
    ///    matched against win (only when fileAlbum/fileArtists are given), so fresh duplicates keep the
    ///    properly tagged entry.
    /// 4. Entries of a synced account (UserId != "") win over purely local entries (UserId == "").
    /// 5. Higher score wins.
    /// 6. Older DateAdded wins (ties and nulls: an entry with a date beats one without, then oldest).
    /// 7. Smallest SongId wins as a last resort.
    /// Returns null when no entries are given.
    /// </summary>
    public static UpvotedSong? ChooseCanonicalEntry(IEnumerable<UpvotedSong> sameSongEntries, string? fileAlbum = null, string? fileArtists = null)
    {
        UpvotedSong? canonical = null;
        foreach (var entry in sameSongEntries)
        {
            if (canonical == null || CompareCanonical(entry, canonical, fileAlbum, fileArtists) < 0)
                canonical = entry;
        }
        return canonical;
    }

    static int CompareCanonical(UpvotedSong a, UpvotedSong b, string? fileAlbum = null, string? fileArtists = null)
    {
        // 1. Rows carrying user data always win over rows without it.
        bool aData = CarriesSongData(a);
        bool bData = CarriesSongData(b);
        if (aData != bData)
            return aData ? -1 : 1;

        if (aData)
        {
            // 2. Both carry data: more votes, then the bigger streak, then an analyzed volume.
            int activityComparison = (b.TotalLikes + b.TotalDislikes).CompareTo(a.TotalLikes + a.TotalDislikes);
            if (activityComparison != 0)
                return activityComparison;
            int streakComparison = Math.Abs(b.Streak).CompareTo(Math.Abs(a.Streak));
            if (streakComparison != 0)
                return streakComparison;
            int volumeComparison = (b.Volume > 0f).CompareTo(a.Volume > 0f);
            if (volumeComparison != 0)
                return volumeComparison;
        }
        else if (fileAlbum != null && fileArtists != null)
        {
            // 3. Neither carries data: entries whose tags are exactly the tags of the arbitrating file
            //    win (a metadata-less entry cannot prove it is the file, an entry with the exact tags
            //    can) - fresh duplicates keep the properly tagged entry.
            bool aExact = TagsEqual(a.Artist, a.Album, fileArtists, fileAlbum);
            bool bExact = TagsEqual(b.Artist, b.Album, fileArtists, fileAlbum);
            int exactComparison = bExact.CompareTo(aExact);
            if (exactComparison != 0)
                return exactComparison;
        }

        // 4. Synced account rows first (UserId == "" marks purely local, not yet synced rows).
        int syncedComparison = string.IsNullOrEmpty(a.UserId).CompareTo(string.IsNullOrEmpty(b.UserId));
        if (syncedComparison != 0)
            return syncedComparison;
        // 5. Higher score first.
        int scoreComparison = b.Score.CompareTo(a.Score);
        if (scoreComparison != 0)
            return scoreComparison;
        // 6. Entries with a DateAdded before entries without one, then the oldest first.
        int dateComparison = CompareNullableDateAdded(a.DateAdded, b.DateAdded);
        if (dateComparison != 0)
            return dateComparison;
        // 7. Smallest SongId first (fully deterministic).
        return a.SongId.CompareTo(b.SongId);
    }

    static int CompareNullableDateAdded(DateTimeOffset? a, DateTimeOffset? b)
    {
        bool aHasDate = a.HasValue;
        bool bHasDate = b.HasValue;
        if (aHasDate != bHasDate)
            return aHasDate ? -1 : 1;
        if (!aHasDate)
            return 0;
        return a!.Value.CompareTo(b!.Value);
    }

    /// <summary>
    /// Merges a group of entries that all belong to one and the same song into one: the canonical entry
    /// (see <see cref="ChooseCanonicalEntry"/> - data-carrying rows win, so score/history is never lost)
    /// is kept and returned together with the entries that should be removed from the database. Only the
    /// registration date is blended into the kept entry: DateAdded becomes the oldest date of the group
    /// (null stays null). The volume keeps the value of the kept (canonical) row - it is a per-file
    /// measurement of the very song, not cumulative user data, so nothing is merged for it.
    /// When the kept entry carries less metadata than another row of the group (it won because it holds
    /// the song data, which cannot be recreated), the caller should fill its EMPTY tag fields from the
    /// combined tags of the group AFTER removing the other rows (see <see cref="TryFillMissingTags"/>),
    /// so the metadata ends up "where it belongs" without ever colliding with another row's identity.
    /// The caller is responsible for actually removing the returned entries (and their history rows) in
    /// the database. Throws when no entries are given.
    /// </summary>
    public static (UpvotedSong Keep, UpvotedSong[] Remove) MergeSameSongEntries(IEnumerable<UpvotedSong> sameSongEntries, string? fileAlbum = null, string? fileArtists = null)
    {
        UpvotedSong[] entries = (sameSongEntries ?? throw new ArgumentNullException(nameof(sameSongEntries))).ToArray();
        if (entries.Length == 0)
            throw new ArgumentException("Cannot merge an empty group of entries.", nameof(sameSongEntries));

        UpvotedSong keep = ChooseCanonicalEntry(entries, fileAlbum, fileArtists)!;
        foreach (UpvotedSong entry in entries)
        {
            // Only the registration date is blended (oldest wins). The volume stays whatever the kept
            // row has: volume is a per-file measurement of the very song, not cumulative user data, so
            // there is nothing to "merge" - the canonical row's value is the right one.
            if (entry.DateAdded.HasValue && (!keep.DateAdded.HasValue || entry.DateAdded < keep.DateAdded))
                keep.DateAdded = entry.DateAdded;
        }

        UpvotedSong[] remove = entries.Where(entry => !ReferenceEquals(entry, keep)).ToArray();
        return (keep, remove);
    }

    /// <summary>
    /// Combines the album/artist tags of entries that describe one and the same song into a single tag
    /// set, treating empty fields as "not recorded" (rows that differ only because one of them has an
    /// empty artist or album are still the same song - e.g. metadata that was pruned on one client).
    /// Returns false when two entries CONTRADICT each other: both carry the same field with different
    /// non-empty values - those cannot be the same song (genuinely different same-named songs).
    /// The combined tags are built from the first non-empty value seen per field.
    /// </summary>
    public static bool TryGetCombinedTags(IEnumerable<UpvotedSong> sameSongEntries, out string artist, out string album)
    {
        artist = "";
        album = "";
        foreach (UpvotedSong entry in sameSongEntries)
        {
            if (entry.Artist.Length > 0 && artist.Length > 0 && !string.Equals(artist, entry.Artist, StringComparison.Ordinal))
                return false; // Two different artists on the same file name: different songs
            if (entry.Album.Length > 0 && album.Length > 0 && !string.Equals(album, entry.Album, StringComparison.Ordinal))
                return false; // Two different albums on the same file name: different songs
            if (artist.Length == 0 && entry.Artist.Length > 0)
                artist = entry.Artist;
            if (album.Length == 0 && entry.Album.Length > 0)
                album = entry.Album;
        }
        return true;
    }

    /// <summary>
    /// Fills the EMPTY tag fields of the kept entry of a duplicate merge from the combined tags of the
    /// group (see <see cref="TryGetCombinedTags"/>): this is the case when the kept entry won because it
    /// carries the song data while another entry recorded a field it is missing (e.g. a pruned artist).
    /// The caller must apply this only AFTER the other rows were removed and saved, otherwise updating
    /// the kept row onto the same identity would violate the unique index.
    /// </summary>
    public static bool TryFillMissingTags(UpvotedSong keep, string combinedAlbum, string combinedArtist, out string? artistToSet, out string? albumToSet)
    {
        artistToSet = null;
        albumToSet = null;
        if (keep.Artist.Length == 0 && combinedArtist.Length > 0)
            artistToSet = combinedArtist;
        if (keep.Album.Length == 0 && combinedAlbum.Length > 0)
            albumToSet = combinedAlbum;
        return artistToSet != null || albumToSet != null;
    }

    /// <summary>
    /// Finds the upvotedSong entry a song file belongs to, among the given candidate entries
    /// (e.g. all entries of the current user).
    /// - No entry with the file name: returns null (the file is not registered as a song yet).
    /// - Exactly one entry with the file name: returns it (the file name alone identifies the song).
    /// - Several entries share the file name (they are different songs): the tags of the file are read
    ///   via readFileTags and used to disambiguate. Returns the single entry carrying exactly these tags.
    /// - Several entries share the file name AND exactly the same tags: they are duplicate entries of one
    ///   and the same song (e.g. two clients registered the same file separately). The canonical entry is
    ///   returned deterministically (see <see cref="ChooseCanonicalEntry"/>); the duplicates should be
    ///   merged in the database (see <see cref="MergeSameSongEntries"/>, done automatically after pulls).
    /// - Several entries share the file name but none carries the tags of the file: returns null (the
    ///   file is not registered as a song yet - same-named entries belong to other songs).
    /// readFileTags is only invoked when several entries share the file name.
    /// </summary>
    public static UpvotedSong? ResolveUpvotedSongEntry(string fileName, IEnumerable<UpvotedSong> candidateSongs, Func<(string Album, string Artists)> readFileTags)
    {
        var filenameMatchingSongs = candidateSongs.Where(s => s.Name == fileName).ToArray();
        if (filenameMatchingSongs.Length == 0)
            return null; // No songs at all means nothing we can do
        if (filenameMatchingSongs.Length == 1)
            return filenameMatchingSongs.First();

        var (fileAlbum, fileArtists) = readFileTags();
        var fullMatchingSongs = filenameMatchingSongs.Where(s => s.Album == fileAlbum && s.Artist == fileArtists).ToArray();
        if (fullMatchingSongs.Length == 1)
            return fullMatchingSongs.First();
        if (fullMatchingSongs.Length > 1)
        {
            // Several entries are the very same song (same file name and tags): pick the canonical one.
            // The duplicates are merged away automatically after the next pull.
            return ChooseCanonicalEntry(fullMatchingSongs);
        }

        return null; // The file carries tags no entry has: it is not registered as a song yet
    }
}
