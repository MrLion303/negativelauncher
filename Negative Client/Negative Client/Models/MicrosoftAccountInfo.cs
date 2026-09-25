namespace Negative_Client.Models
{
    public sealed class MicrosoftAccountInfo
    {
        public string Identifier { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        public bool IsSelected { get; set; }


        public string DisplayName
        {
            get
            {
                string name =
                    string.IsNullOrWhiteSpace(Username)
                        ? Identifier
                        : Username;

                return
                    IsSelected
                        ? $"{name}  •  EN USO"
                        : name;
            }
        }
    }
}
