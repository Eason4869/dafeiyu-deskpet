namespace Dafeiyu
{
    public sealed class VisibilityPolicy
    {
        public bool UserVisible { get; set; }
        public bool Fullscreen { get; set; }
        public bool OverrideFullscreen { get; set; }
        public VisibilityPolicy() { UserVisible=true; }
        public bool AutoHidden { get { return UserVisible && Fullscreen && !OverrideFullscreen; } }
        public bool Visible { get { return UserVisible && (!Fullscreen || OverrideFullscreen); } }
    }
}
