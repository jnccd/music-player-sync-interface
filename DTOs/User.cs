using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace MusicPlayerSyncInterface.DTOs;

public class User(string UserId, string UserHandle, string UserDisplayName)
{
    /// <summary>
    /// Immutable, Unique.
    /// Can be used as a primary key.
    /// </summary>
    public string UserId { get; set; } = UserId;
    /// <summary>
    /// A unique name the user selected on registration, not guaranteed to be immutable.
    /// </summary>
    public string UserHandle { get; set; } = UserHandle;
    /// <summary>
    /// The display name of the user, can be changed by the user at any time, not guaranteed to be unique.
    /// May contain special characters, emojis, etc.
    /// </summary>
    public string UserDisplayName { get; set; } = UserDisplayName;

}
