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
                "qwen3-4b-thinking",
                "Qwen 3 4B Thinking (Advanced)",
                "https://huggingface.co/bartowski/Qwen_Qwen3-4B-Thinking-2507-GGUF/resolve/main/Qwen_Qwen3-4B-Thinking-2507-Q6_K.gguf",
                "4B",
                "Deep reasoning for resolving ambiguous titles and absolute numbering."),
            new AiModelPreset(
                "ministral-3-3b",
                "Ministral 3 3B (Syntax Master)",
                "https://huggingface.co/bartowski/Ministral-3b-instruct-2410-GGUF/resolve/main/Ministral-3b-instruct-2410-Q6_K.gguf",
                "3B",
                "Exceptional JSON schema adherence and edge optimization."),
            new AiModelPreset(
                "phi-4-mini",
                "Phi-4 Mini 3.8B (Rationalist)",
                "https://huggingface.co/bartowski/phi-4-mini-instruct-GGUF/resolve/main/phi-4-mini-instruct-Q6_K.gguf",
                "3.8B",
                "Microsoft's SOTA for reasoning and logical extraction."),
            new AiModelPreset(
                "granite-3.3-2b",
                "IBM Granite 3.3 2B (Fast)",
                "https://huggingface.co/bartowski/ibm-granite_granite-3.3-2b-instruct-GGUF/resolve/main/ibm-granite_granite-3.3-2b-instruct-IQ4_XS.gguf",
                "2.5B",
                "Extremely low latency with high accuracy for its size."),
            new AiModelPreset(
                "custom",
                "Custom URL",
                "",
                "N/A",
                "Specify a custom GGUF download URL.")
        };}
