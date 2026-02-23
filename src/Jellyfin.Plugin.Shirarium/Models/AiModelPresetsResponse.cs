namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Response containing available AI model presets.
/// </summary>
public sealed class AiModelPresetsResponse
{
    public AiModelPreset[] Presets { get; set; } = System.Array.Empty<AiModelPreset>();
    public string SelectedPresetId { get; set; } = string.Empty;
}
