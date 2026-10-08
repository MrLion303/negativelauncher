using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace Negative_Client.Models
{
    public sealed class ScreenshotGalleryItem :
        INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _showSelection;


        public string FilePath { get; set; } =
            string.Empty;


        public string FileName { get; set; } =
            string.Empty;


        public string InstanceId { get; set; } =
            string.Empty;


        public string InstanceName { get; set; } =
            string.Empty;


        public DateTime CapturedAt { get; set; }


        public BitmapSource? Thumbnail { get; set; }


        public string DisplayDate =>
            CapturedAt.ToString(
                "dd/MM/yyyy  HH:mm");


        public bool IsSelected
        {
            get =>
                _isSelected;

            set
            {
                if (_isSelected ==
                    value)
                {
                    return;
                }


                _isSelected =
                    value;


                OnPropertyChanged();
            }
        }


        public bool ShowSelection
        {
            get =>
                _showSelection;

            set
            {
                if (_showSelection ==
                    value)
                {
                    return;
                }


                _showSelection =
                    value;


                OnPropertyChanged();
            }
        }


        public event PropertyChangedEventHandler?
            PropertyChanged;


        private void OnPropertyChanged(
            [CallerMemberName]
            string? propertyName = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(
                    propertyName));
        }
    }
}
