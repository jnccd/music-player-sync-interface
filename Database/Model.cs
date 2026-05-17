using Microsoft.EntityFrameworkCore;
using MusicPlayerSyncInterface.DTOs;

namespace MusicPlayerSyncInterface.Database;

public static class Model
{
    public static void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasKey(s => s.UserId);

        modelBuilder.Entity<UpvotedSong>()
            .HasKey(s => s.SongId);
        modelBuilder.Entity<UpvotedSong>()
            .HasOne(s => s.User)
            .WithMany() // No navigation property back to UpvotedSong
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<UpvotedSong>()
            .HasIndex(s => new { s.UserId, s.Name, s.Artist, s.Album })
            .IsUnique(); // TODO: This doesn't seem to work yet :/

        modelBuilder.Entity<SongHistoryEntry>()
            .HasKey(s => new { s.UserId, s.SongId, s.Date });
        modelBuilder.Entity<SongHistoryEntry>()
            .HasOne(s => s.UpvotedSong)
            .WithMany() // No navigation property back to SongHistoryEntry
            .HasForeignKey(s => s.SongId);
    }
}