namespace AvalNeroSercher.Models
{
    public class CameraInfo
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;

        public override string ToString() => Name;
    }
}