using System.Text.Json.Serialization;

namespace Com.MiniScreenNotes.Models;

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppConfig))]
internal sealed partial class AppConfigJsonContext : JsonSerializerContext
{
}
