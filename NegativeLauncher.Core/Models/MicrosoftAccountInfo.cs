using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Negative_Client.Services;

namespace Negative_Client.Models
{
    public sealed class MicrosoftAccountInfo : INotifyPropertyChanged
    {
        private string _skinHeadPath =
            string.Empty;

        private string _skinTexturePath =
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
                string nextPath = value ?? string.Empty;
                bool samePath;

                if (string.IsNullOrEmpty(_skinHeadPath) || string.IsNullOrEmpty(nextPath))
                {
                    samePath = string.Equals(_skinHeadPath, nextPath, StringComparison.Ordinal);
                }
                else
                {
                    try
                    {
                        samePath = LauncherPathRules.PathsEqual(_skinHeadPath, nextPath);
                    }
                    catch
                    {
                        samePath = string.Equals(_skinHeadPath, nextPath, StringComparison.Ordinal);
                    }
                }

                if (samePath)
                    return;

                _skinHeadPath = nextPath;
                OnPropertyChanged();
            }
        }

        public string SkinTexturePath
        {
            get => _skinTexturePath;

            set
            {
                string nextPath = value ?? string.Empty;
                bool samePath;
                if (string.IsNullOrEmpty(_skinTexturePath) || string.IsNullOrEmpty(nextPath))
                {
                    samePath = string.Equals(_skinTexturePath, nextPath, StringComparison.Ordinal);
                }
                else
                {
                    try { samePath = LauncherPathRules.PathsEqual(_skinTexturePath, nextPath); }
                    catch { samePath = string.Equals(_skinTexturePath, nextPath, StringComparison.Ordinal); }
                }

                if (samePath)
                    return;

                _skinTexturePath = nextPath;
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
