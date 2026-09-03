using MusicPlayerSyncInterface.DTOs;
using System;
using System.Collections.Generic;
using System.IO;
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
    /// Finds the upvotedSong entry a song file belongs to, among the given candidate entries
    /// (e.g. all entries of the current user).
    /// - No entry with the file name: returns null (the file is not registered as a song yet).
    /// - Exactly one entry with the file name: returns it (the file name alone identifies the song).
    /// - Several entries share the file name (they are different songs): the tags of the file are read
    ///   via readFileTags and used to disambiguate. Exactly one matching entry is returned; otherwise an
    ///   InvalidDataException is thrown, since the mapping is ambiguous.
    /// readFileTags is only invoked when several entries share the file name.
    /// </summary>
    public static UpvotedSong? ResolveUpvotedSongEntry(string fileName, IEnumerable<UpvotedSong> candidateSongs, Func<(string Album, string Artists)> readFileTags)
    {
        var filenameMatchingSongs = candidateSongs.Where(s => s.Name == fileName).ToArray();
        if (filenameMatchingSongs.Length == 1)
            return filenameMatchingSongs.First();
        if (filenameMatchingSongs.Length == 0)
            return null; // No songs at all means nothing we can do

        var (fileAlbum, fileArtists) = readFileTags();
        var fullMatchingSongs = filenameMatchingSongs.Where(s => s.Album == fileAlbum && s.Artist == fileArtists).ToArray();
        if (fullMatchingSongs.Length == 1)
            return fullMatchingSongs.First();

        throw new InvalidDataException($"Multiple upvotedSong entries match the file \"{fileName}\" (album: {fileAlbum}, artists: {fileArtists}), cannot tell which one it belongs to.");
    }
}
