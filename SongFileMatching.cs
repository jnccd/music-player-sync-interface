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
    /// Chooses the entry of a group of entries that all belong to one and the same song that should be
    /// kept (and returned when the group is resolved as one song). Deterministic so the server, the
    /// clients and the file matching all agree on the same row:
    /// 1. Entries carrying exactly the album/artist of the file the song was matched against win
    ///    (only when fileAlbum/fileArtists are given, i.e. an actual song file arbitrated the group;
    ///    entries without album/artist metadata are catch-alls that can never prove identity).
    /// 2. Entries of a synced account (UserId != "") win over purely local entries (UserId == ""),
    ///    since only synced entries can ever be voted on through the server.
    /// 3. Higher score wins (it holds the most up-to-date vote history).
    /// 4. Older DateAdded wins (ties and nulls: an entry with a date beats one without, then oldest).
    /// 5. Smallest SongId wins as a last resort.
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
        // 1. Entries whose tags are exactly the tags of the arbitrating file first (a metadata-less
        //    entry cannot prove it is the file, an entry with the exact tags can).
        if (fileAlbum != null && fileArtists != null)
        {
            bool aExact = TagsEqual(a.Artist, a.Album, fileArtists, fileAlbum);
            bool bExact = TagsEqual(b.Artist, b.Album, fileArtists, fileAlbum);
            int exactComparison = bExact.CompareTo(aExact);
            if (exactComparison != 0)
                return exactComparison;
        }
        // 2. Synced account rows first (UserId == "" marks purely local, not yet synced rows).
        int syncedComparison = string.IsNullOrEmpty(a.UserId).CompareTo(string.IsNullOrEmpty(b.UserId));
        if (syncedComparison != 0)
            return syncedComparison;
        // 3. Higher score first.
        int scoreComparison = b.Score.CompareTo(a.Score);
        if (scoreComparison != 0)
            return scoreComparison;
        // 4. Entries with a DateAdded before entries without one, then the oldest first.
        int dateComparison = CompareNullableDateAdded(a.DateAdded, b.DateAdded);
        if (dateComparison != 0)
            return dateComparison;
        // 5. Smallest SongId first (fully deterministic).
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
    /// (see <see cref="ChooseCanonicalEntry"/>) is kept and returned together with the entries that should
    /// be removed from the database. The kept entry keeps its own counters (score, streak, likes/dislikes
    /// - the highest-scored entry holds the most complete vote history, the others are dropped with
    /// theirs). Only the timeless facts are blended into the kept entry:
    /// - DateAdded becomes the oldest date of the group (null stays null).
    /// - Volume becomes the loudest (highest) volume of the group, since -1 means "not analyzed yet".
    /// When fileAlbum/fileArtists are given (an actual song file arbitrated that the entries are the same
    /// song), entries with exactly these tags win the canonical spot, so a metadata-less duplicate is
    /// absorbed into the properly tagged entry instead of the other way around.
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
            if (entry.DateAdded.HasValue && (!keep.DateAdded.HasValue || entry.DateAdded < keep.DateAdded))
                keep.DateAdded = entry.DateAdded;
            if (entry.Volume > keep.Volume)
                keep.Volume = entry.Volume;
        }

        UpvotedSong[] remove = entries.Where(entry => !ReferenceEquals(entry, keep)).ToArray();
        return (keep, remove);
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
