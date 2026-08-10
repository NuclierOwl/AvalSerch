namespace AvalNeroSercher.Models;

public class ModelInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsLoaded { get; set; }

    public override string ToString() => Name;
}
