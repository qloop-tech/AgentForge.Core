using AgentForge.Verticals.Abstractions;

namespace AgentForge.Vertical.Template;

public sealed class SampleVerticalDescriptor(string systemPrompt) : IVerticalDescriptor
{
    public string VerticalId => "sample";
    public string DisplayName => "Sample Assistant";
    public string AgentName => "SampleAssistant";
    public string AgentDescription => "Answers questions for the sample vertical.";
    public string SystemPrompt => systemPrompt;
    public string McpServerName => "sample-mcp";
    public string AssetPathPrefix => "/sample-assets/";
    public string AssetRootPath => SamplePluginPaths.Assets;
    public string PreviewTitle => "Sample Assistant";
    public string PreviewDescription => "An AgentForge external vertical plugin.";
}
