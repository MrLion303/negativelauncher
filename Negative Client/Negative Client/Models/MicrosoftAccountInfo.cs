using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Negative_Client.Models
{
    public sealed class MicrosoftAccountInfo : INotifyPropertyChanged
    {
        private string _skinHeadPath =
            string.Empty;

        public string Identifier { get; set; } =
            string.Empty;

        public string Uuid { get; set; } =
            string.Empty;

        public string Username { get; set; } =
            string.Empty;

        public bool IsSelected { get; set; }

        public bool IsOffline { get; set; }

        public string SkinHeadPath
        {
            get =>
                _skinHeadPath;

            set
            {
                if (string.Equals(
                        _skinHeadPath,
                        value,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _skinHeadPath =
                    value ?? string.Empty;

                OnPropertyChanged();
            }
        }

        public string DisplayName
        {
            get
            {
                string name =
                    string.IsNullOrWhiteSpace(Username)
                        ? Identifier
                        : Username;

                if (IsOffline)
                {
                    return IsSelected
                        ? $"{name}  •  NO PREMIUM  •  EN USO"
                        : $"{name}  •  NO PREMIUM";
                }

                return IsSelected
                    ? $"{name}  •  EN USO"
                    : name;
            }
        }

        public event PropertyChangedEventHandler?
            PropertyChanged;

        private void OnPropertyChanged(
            [CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(
                    propertyName));
        }
    }
}
