using Microsoft.EntityFrameworkCore;
using MusicPlayerSyncInterface.DTOs;

namespace MusicPlayerSyncInterface.Database;

public static class Model
{
    public static void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UpvotedSong>()
            .HasKey(s => s.SongId);
        modelBuilder.Entity<UpvotedSong>()
            .HasIndex(s => new { s.UserId, s.Name, s.Artist, s.Album })
            .IsUnique();

        modelBuilder.Entity<SongHistoryEntry>()
            .HasKey(s => new { s.UserId, s.SongId, s.Date });
        modelBuilder.Entity<SongHistoryEntry>()
            .HasOne(s => s.UpvotedSong)
            .WithMany() // No navigation property back to SongHistoryEntry
            .HasForeignKey(s => s.SongId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}