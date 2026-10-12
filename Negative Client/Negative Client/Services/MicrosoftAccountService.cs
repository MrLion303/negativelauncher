using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.Auth.Microsoft.Sessions;
using Negative_Client.Models;
using XboxAuthNet.Game.Accounts;

namespace Negative_Client.Services
{
    public sealed class MicrosoftAccountService
    {
        public const int MaxAccounts = 3;

        public const string PremiumAccountMode =
            "premium";

        public const string OfflineAccountMode =
            "offline";

        private const string OfflineIdentifierPrefix =
            "offline:";

        public static MicrosoftAccountService Instance { get; } =
            new MicrosoftAccountService();

        private readonly JELoginHandler _loginHandler;

        private readonly LauncherPreferencesService _preferencesService =
            new LauncherPreferencesService();

        private readonly OfflineAccountService _offlineAccountService =
            new OfflineAccountService();

        private static readonly string SelectionFilePath =
            Path.Combine(
                InstanceService.LauncherRoot,
                "accounts",
                "selected-account.json");

        private readonly JsonSerializerOptions _jsonOptions =
            new()
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };

        private string? _selectedAccountIdentifier;

        private string _accountMode =
            PremiumAccountMode;

        private OfflineAccountProfile? _offlineProfile;

        public MSession? CurrentSession { get; private set; }

        public bool IsOfflineModeActive =>
            string.Equals(
                _accountMode,
                OfflineAccountMode,
                StringComparison.OrdinalIgnoreCase) &&
            _offlineProfile != null &&
            !string.IsNullOrWhiteSpace(_offlineProfile.Username);

        public OfflineAccountProfile? OfflineProfile =>
            _offlineProfile;

        public bool IsSignedIn =>
            IsOfflineModeActive ||
            (CurrentSession != null &&
             !string.IsNullOrWhiteSpace(_selectedAccountIdentifier));

        public string Username =>
            IsOfflineModeActive
                ? _offlineProfile?.Username ?? string.Empty
                : CurrentSession?.Username ?? string.Empty;

        public string Uuid =>
            IsOfflineModeActive
                ? MSession.CreateOfflineSession(
                        _offlineProfile!.Username)
                    .UUID ?? string.Empty
                : CurrentSession?.UUID ?? string.Empty;

        public string SelectedAccountIdentifier =>
            IsOfflineModeActive
                ? BuildOfflineIdentifier(
                    _offlineProfile!.Username)
                : _selectedAccountIdentifier ?? string.Empty;

        public string AccountMode =>
            _accountMode;

        private sealed class AccountSelectionState
        {
            public string SelectedAccountIdentifier { get; set; } =
                string.Empty;
        }

        private MicrosoftAccountService()
        {
            string accountsDirectory =
                Path.Combine(
                    InstanceService.LauncherRoot,
                    "accounts");

            Directory.CreateDirectory(
                accountsDirectory);

            string accountsFile =
                Path.Combine(
                    accountsDirectory,
                    "microsoft-accounts.json");

            _loginHandler =
                new JELoginHandlerBuilder()
                    .WithAccountManager(
                        accountsFile)
                    .Build();
        }

        // =====================================================
        // INICIALIZAR / REFRESCAR MODO
        // =====================================================

        public async Task InitializeAsync()
        {
            await LoadSelectedAccountAsync();

            await RefreshAccountModeAsync();

            if (IsOfflineModeActive)
            {
                CurrentSession =
                    MSession.CreateOfflineSession(
                        _offlineProfile!.Username);

                return;
            }

            List<MicrosoftAccountInfo> premiumAccounts =
                GetPremiumAccounts();

            if (premiumAccounts.Count == 0)
            {
                _selectedAccountIdentifier =
                    null;

                CurrentSession =
                    null;

                return;
            }

            bool selectionExists =
                !string.IsNullOrWhiteSpace(_selectedAccountIdentifier) &&
                premiumAccounts.Any(
                    account =>
                        string.Equals(
                            account.Identifier,
                            _selectedAccountIdentifier,
                            StringComparison.OrdinalIgnoreCase));

            if (!selectionExists)
            {
                _selectedAccountIdentifier =
                    premiumAccounts[0].Identifier;

                await SaveSelectedAccountAsync();
            }

            await TryRestoreSelectedSessionAsync();
        }

        public async Task RefreshAccountModeAsync()
        {
            LauncherPreferences preferences =
                await _preferencesService.LoadAsync();

            _accountMode =
                string.Equals(
                    preferences.AccountMode,
                    OfflineAccountMode,
                    StringComparison.OrdinalIgnoreCase)
                    ? OfflineAccountMode
                    : PremiumAccountMode;

            _offlineProfile =
                await _offlineAccountService.LoadAsync();

            if (string.Equals(
                    _accountMode,
                    OfflineAccountMode,
                    StringComparison.OrdinalIgnoreCase) &&
                (_offlineProfile == null ||
                 string.IsNullOrWhiteSpace(_offlineProfile.Username)))
            {
                _accountMode =
                    PremiumAccountMode;
            }

            if (IsOfflineModeActive)
            {
                CurrentSession =
                    MSession.CreateOfflineSession(
                        _offlineProfile!.Username);
            }
        }

        public async Task SetAccountModeAsync(
            string mode)
        {
            string normalized =
                string.Equals(
                    mode,
                    OfflineAccountMode,
                    StringComparison.OrdinalIgnoreCase)
                    ? OfflineAccountMode
                    : PremiumAccountMode;

            if (normalized == OfflineAccountMode)
            {
                OfflineAccountProfile? profile =
                    await _offlineAccountService.LoadAsync();

                if (profile == null ||
                    string.IsNullOrWhiteSpace(profile.Username))
                {
                    throw new InvalidOperationException(
                        "Primero guarda un perfil no premium válido.");
                }
            }

            LauncherPreferences preferences =
                await _preferencesService.LoadAsync();

            preferences.AccountMode =
                normalized;

            await _preferencesService.SaveAsync(
                preferences);

            await RefreshAccountModeAsync();

            if (normalized == PremiumAccountMode)
            {
                await TryRestoreSelectedSessionAsync();
            }
        }

        // =====================================================
        // LISTAR CUENTAS
        // =====================================================

        public List<MicrosoftAccountInfo> GetAccounts()
        {
            if (IsOfflineModeActive)
            {
                MSession offlineSession =
                    MSession.CreateOfflineSession(
                        _offlineProfile!.Username);

                return new List<MicrosoftAccountInfo>
                {
                    new MicrosoftAccountInfo
                    {
                        Identifier =
                            BuildOfflineIdentifier(
                                _offlineProfile.Username),

                        Uuid =
                            offlineSession.UUID ?? string.Empty,

                        Username =
                            _offlineProfile.Username,

                        IsSelected =
                            true,

                        IsOffline =
                            true
                    }
                };
            }

            return GetPremiumAccounts();
        }

        public List<MicrosoftAccountInfo> GetPremiumAccounts()
        {
            List<MicrosoftAccountInfo> result =
                new();

            foreach (IXboxGameAccount account in
                _loginHandler.AccountManager.GetAccounts())
            {
                string identifier =
                    account.Identifier ?? string.Empty;

                if (string.IsNullOrWhiteSpace(identifier))
                {
                    continue;
                }

                string username =
                    identifier;

                string uuid =
                    string.Empty;

                if (account is JEGameAccount jeAccount &&
                    jeAccount.Profile != null)
                {
                    if (!string.IsNullOrWhiteSpace(
                            jeAccount.Profile.Username))
                    {
                        username =
                            jeAccount.Profile.Username;
                    }

                    uuid =
                        jeAccount.Profile.UUID ?? string.Empty;
                }

                result.Add(
                    new MicrosoftAccountInfo
                    {
                        Identifier =
                            identifier,

                        Uuid =
                            uuid,

                        Username =
                            username,

                        IsSelected =
                            !IsOfflineModeActive &&
                            string.Equals(
                                identifier,
                                _selectedAccountIdentifier,
                                StringComparison.OrdinalIgnoreCase),

                        IsOffline =
                            false
                    });
            }

            return result
                .OrderByDescending(account => account.IsSelected)
                .ThenBy(
                    account => account.Username,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // =====================================================
        // AÑADIR UNA CUENTA NUEVA
        // =====================================================

        public async Task<MSession> AddAccountInteractivelyAsync()
        {
            if (GetPremiumAccounts().Count >= MaxAccounts)
            {
                throw new InvalidOperationException(
                    $"Negative Client permite un máximo de {MaxAccounts} cuentas Microsoft.");
            }

            IXboxGameAccount account =
                _loginHandler.AccountManager.NewAccount();

            MSession session =
                await _loginHandler.AuthenticateInteractively(
                    account);

            string identifier =
                account.Identifier ??
                session.UUID ??
                string.Empty;

            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new InvalidOperationException(
                    "Microsoft devolvió una cuenta sin identificador.");
            }

            _selectedAccountIdentifier =
                identifier;

            CurrentSession =
                session;

            await SaveSelectedAccountAsync();

            await SetAccountModeAsync(
                PremiumAccountMode);

            return session;
        }

        // =====================================================
        // CAMBIAR LA CUENTA EN USO
        // =====================================================

        public async Task<bool> SelectAccountAsync(
            string identifier)
        {
            if (IsOfflineIdentifier(identifier))
            {
                OfflineAccountProfile? profile =
                    await _offlineAccountService.LoadAsync();

                if (profile == null ||
                    string.IsNullOrWhiteSpace(profile.Username))
                {
                    return false;
                }

                await SetAccountModeAsync(
                    OfflineAccountMode);

                CurrentSession =
                    MSession.CreateOfflineSession(
                        profile.Username);

                return true;
            }

            IXboxGameAccount? account =
                FindAccount(identifier);

            if (account == null)
            {
                return false;
            }

            _selectedAccountIdentifier =
                identifier;

            CurrentSession =
                null;

            await SaveSelectedAccountAsync();

            await SetAccountModeAsync(
                PremiumAccountMode);

            try
            {
                CurrentSession =
                    await _loginHandler.AuthenticateSilently(
                        account);

                return CurrentSession != null;
            }
            catch
            {
                CurrentSession =
                    null;

                return false;
            }
        }

        // =====================================================
        // VOLVER A AUTENTICAR UNA CUENTA EXISTENTE
        // =====================================================

        public async Task<MSession> ReauthenticateAccountAsync(
            string identifier)
        {
            if (IsOfflineIdentifier(identifier))
            {
                throw new InvalidOperationException(
                    "Un perfil no premium no utiliza autenticación Microsoft.");
            }

            IXboxGameAccount? account =
                FindAccount(identifier);

            if (account == null)
            {
                throw new InvalidOperationException(
                    "No se encontró la cuenta seleccionada.");
            }

            MSession session =
                await _loginHandler.AuthenticateInteractively(
                    account);

            _selectedAccountIdentifier =
                account.Identifier ??
                session.UUID ??
                identifier;

            CurrentSession =
                session;

            await SaveSelectedAccountAsync();

            await SetAccountModeAsync(
                PremiumAccountMode);

            return session;
        }

        // =====================================================
        // RESTAURAR SIN MOSTRAR LOGIN
        // =====================================================

        public async Task<bool> TryRestoreSelectedSessionAsync()
        {
            if (IsOfflineModeActive)
            {
                CurrentSession =
                    MSession.CreateOfflineSession(
                        _offlineProfile!.Username);

                return true;
            }

            if (string.IsNullOrWhiteSpace(_selectedAccountIdentifier))
            {
                CurrentSession =
                    null;

                return false;
            }

            IXboxGameAccount? account =
                FindAccount(_selectedAccountIdentifier);

            if (account == null)
            {
                CurrentSession =
                    null;

                return false;
            }

            try
            {
                CurrentSession =
                    await _loginHandler.AuthenticateSilently(
                        account);

                return CurrentSession != null;
            }
            catch
            {
                CurrentSession =
                    null;

                return false;
            }
        }

        // =====================================================
        // OBTENER SESIÓN VÁLIDA PARA JUGAR
        // =====================================================

        public async Task<MSession?> GetValidSessionAsync()
        {
            await RefreshAccountModeAsync();

            if (IsOfflineModeActive)
            {
                CurrentSession =
                    MSession.CreateOfflineSession(
                        _offlineProfile!.Username);

                return CurrentSession;
            }

            bool restored =
                await TryRestoreSelectedSessionAsync();

            return restored
                ? CurrentSession
                : null;
        }

        // =====================================================
        // CERRAR SESIÓN DE UNA CUENTA
        // =====================================================

        public async Task SignOutAccountAsync(
            string identifier)
        {
            if (IsOfflineIdentifier(identifier))
            {
                await _offlineAccountService.DeleteAsync();

                LauncherPreferences preferences =
                    await _preferencesService.LoadAsync();

                preferences.AccountMode =
                    PremiumAccountMode;

                await _preferencesService.SaveAsync(
                    preferences);

                await RefreshAccountModeAsync();

                return;
            }

            IXboxGameAccount? account =
                FindAccount(identifier);

            if (account == null)
            {
                return;
            }

            await _loginHandler.Signout(
                account);

            if (string.Equals(
                    identifier,
                    _selectedAccountIdentifier,
                    StringComparison.OrdinalIgnoreCase))
            {
                CurrentSession =
                    null;
            }
        }

        // =====================================================
        // AUXILIARES
        // =====================================================

        private IXboxGameAccount? FindAccount(
            string identifier)
        {
            return _loginHandler
                .AccountManager
                .GetAccounts()
                .FirstOrDefault(
                    account =>
                        string.Equals(
                            account.Identifier,
                            identifier,
                            StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsOfflineIdentifier(
            string identifier)
        {
            return
                !string.IsNullOrWhiteSpace(identifier) &&
                identifier.StartsWith(
                    OfflineIdentifierPrefix,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildOfflineIdentifier(
            string username)
        {
            return OfflineIdentifierPrefix + username;
        }

        private async Task LoadSelectedAccountAsync()
        {
            try
            {
                if (!File.Exists(SelectionFilePath))
                {
                    _selectedAccountIdentifier =
                        null;

                    return;
                }

                string json =
                    await File.ReadAllTextAsync(
                        SelectionFilePath);

                AccountSelectionState? state =
                    JsonSerializer.Deserialize<AccountSelectionState>(
                        json,
                        _jsonOptions);

                _selectedAccountIdentifier =
                    string.IsNullOrWhiteSpace(
                        state?.SelectedAccountIdentifier)
                        ? null
                        : state.SelectedAccountIdentifier;
            }
            catch
            {
                _selectedAccountIdentifier =
                    null;
            }
        }

        private async Task SaveSelectedAccountAsync()
        {
            string? directory =
                Path.GetDirectoryName(SelectionFilePath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            AccountSelectionState state =
                new()
                {
                    SelectedAccountIdentifier =
                        _selectedAccountIdentifier ?? string.Empty
                };

            string json =
                JsonSerializer.Serialize(
                    state,
                    _jsonOptions);

            string temporaryPath =
                SelectionFilePath + ".tmp";

            await File.WriteAllTextAsync(
                temporaryPath,
                json);

            File.Move(
                temporaryPath,
                SelectionFilePath,
                overwrite: true);
        }
    }
}
