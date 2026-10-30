namespace Panak.Models {
    public class AppSettings {
        public string Language { get; set; } = "hy";
        public int GridColumns { get; set; } = 5;
        public bool FocusModeEnabled { get; set; } = false;
        public double FocusDimmingOpacity { get; set; } = 0.40;
        public bool FocusBlurEnabled { get; set; } = false;
        public double FocusBlurRadius { get; set; } = 28.0;
        public string IconShape { get; set; } = "Squircle";
        public bool IsLocked { get; set; } = false;
        public bool LockPopupPosition { get; set; } = false;
        public bool RightDoubleClickEnabled { get; set; } = false;
        public bool SpaceQuickLookEnabled { get; set; } = true;
        public string QuickLookHotkey { get; set; } = "Space";
        public string ClosePopupHotkey { get; set; } = "Escape";
        public bool AutoStart { get; set; } = true;
        public string ThemeColor { get; set; } = Services.ShellHelper.GetDefaultPastelColorHex();
        public string ThemeMode { get; set; } = "Auto";
        public bool ExplorerContextMenuEnabled { get; set; } = true;
        public bool CombineFilesOnDrop { get; set; } = false;
        public bool OpenInCenterOnly { get; set; } = false;
        public bool AutoCheckUpdates { get; set; } = true;
        public double FolderOpacity { get; set; } = 0.85;
        public bool FolderBlurEnabled { get; set; } = true;
        public double FolderBlurRadius { get; set; } = 28.0;
        public string PagingMode { get; set; } = "Slider";
        public int GridRows { get; set; } = 3;
        public bool SliderArrowsEnabled { get; set; } = true;
        public bool SliderDragEnabled { get; set; } = true;
        public string GitHubRepo { get; set; } = "hranthakobian/Panak";
        public DateTime? LastUpdateCheck { get; set; }
        public bool RandomShapeOnCreate { get; set; } = false;
        public bool RandomColorOnCreate { get; set; } = false;
        public bool RandomOpacityOnCreate { get; set; } = false;
        public System.Collections.Generic.List<string> CustomPaletteColors { get; set; } = new();
        public System.Collections.Generic.List<string> HiddenPresetColors { get; set; } = new();
    }
}
