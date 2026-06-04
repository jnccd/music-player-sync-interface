namespace MusicPlayerSyncInterface.DTOs.Composites;

public record UpdateVolumeRequest(Guid SongId, float NewVolume);