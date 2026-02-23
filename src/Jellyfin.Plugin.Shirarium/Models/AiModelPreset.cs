namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Represents a predefined LLM model configuration.
/// </summary>
public sealed class AiModelPreset
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Parameters { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;

    public AiModelPreset(string id, string name, string url, string parameters, string rationale)
    {
        Id = id;
        Name = name;
        Url = url;
        Parameters = parameters;
        Rationale = rationale;
    }

    public static readonly AiModelPreset[] AvailablePresets = new[]
    {
        new AiModelPreset(
            "gemma-3-4b", 
            "Gemma 3 4B IT (Recommended)", 
            "https://huggingface.co/bartowski/google_gemma-3-4b-it-GGUF/resolve/main/google_gemma-3-4b-it-Q6_K.gguf", 
            "4B",
            "Best-in-class accuracy and multimodal reasoning."),
        new AiModelPreset(
            "granite-3.3-2b", 
            "IBM Granite 3.3 2B (Fast)", 
            "https://huggingface.co/bartowski/ibm-granite_granite-3.3-2b-instruct-GGUF/resolve/main/ibm-granite_granite-3.3-2b-instruct-IQ4_XS.gguf", 
            "2.5B",
            "Extremely low latency with high accuracy for its size."),
        new AiModelPreset(
            "qwen3-4b-thinking", 
            "Qwen 3 4B Thinking (Advanced)", 
            "https://huggingface.co/bartowski/Qwen_Qwen3-4B-Thinking-2507-GGUF/resolve/main/Qwen_Qwen3-4B-Thinking-2507-Q6_K.gguf", 
            "4B",
            "Deep reasoning for resolving ambiguous titles and absolute numbering."),
        new AiModelPreset(
            "llama-3.2-3b", 
            "Llama 3.2 3B (Generalist)", 
            "https://huggingface.co/bartowski/Llama-3.2-3B-Instruct-GGUF/resolve/main/Llama-3.2-3B-Instruct-Q6_K.gguf", 
            "3B",
            "Robust cultural knowledge for identifying obscure titles."),
        new AiModelPreset(
            "custom", 
            "Custom URL", 
            "", 
            "N/A",
            "Specify a custom GGUF download URL.")
    };
}
