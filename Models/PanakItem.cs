using System;
using System.ComponentModel;
using System.Windows.Media;

namespace Panak.Models {
    public class PanakItem : INotifyPropertyChanged {
        private ImageSource? _iconSource;

        public string FullPath { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }

        public ImageSource? IconSource {
            get => _iconSource;
            set {
                if (!ReferenceEquals(_iconSource, value)) {
                    _iconSource = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IconSource)));
                }
            }
        }

        public string SizeDisplay { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
