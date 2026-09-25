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
        public static MicrosoftAccountService Instance { get; } =
            new MicrosoftAccountService();


        private readonly JELoginHandler _loginHandler;


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


        public MSession? CurrentSession { get; private set; }


        public bool IsSignedIn =>
            CurrentSession != null &&
            !string.IsNullOrWhiteSpace(
                _selectedAccountIdentifier);


        public string Username =>
            CurrentSession?.Username ??
            string.Empty;


        public string Uuid =>
            CurrentSession?.UUID ??
            string.Empty;


        public string SelectedAccountIdentifier =>
            _selectedAccountIdentifier ??
            string.Empty;


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
        // INICIALIZAR
        // =====================================================

        public async Task InitializeAsync()
        {
            await LoadSelectedAccountAsync();


            List<MicrosoftAccountInfo> accounts =
                GetAccounts();


            if (accounts.Count == 0)
            {
                _selectedAccountIdentifier =
                    null;

                CurrentSession =
                    null;

                return;
            }


            bool selectionExists =
                !string.IsNullOrWhiteSpace(
                    _selectedAccountIdentifier) &&
                accounts.Any(
                    account =>
                        string.Equals(
                            account.Identifier,
                            _selectedAccountIdentifier,
                            StringComparison.OrdinalIgnoreCase));


            if (!selectionExists)
            {
                _selectedAccountIdentifier =
                    accounts[0].Identifier;


                await SaveSelectedAccountAsync();
            }


            await TryRestoreSelectedSessionAsync();
        }


        // =====================================================
        // LISTAR CUENTAS
        // =====================================================

        public List<MicrosoftAccountInfo> GetAccounts()
        {
            List<MicrosoftAccountInfo> result =
                new();


            foreach (IXboxGameAccount account in
                _loginHandler
                    .AccountManager
                    .GetAccounts())
            {
                string identifier =
                    account.Identifier ??
                    string.Empty;


                if (string.IsNullOrWhiteSpace(
                        identifier))
                {
                    continue;
                }


                string username =
                    identifier;


                if (account is JEGameAccount jeAccount &&
                    jeAccount.Profile != null &&
                    !string.IsNullOrWhiteSpace(
                        jeAccount.Profile.Username))
                {
                    username =
                        jeAccount.Profile.Username;
                }


                result.Add(
                    new MicrosoftAccountInfo
                    {
                        Identifier =
                            identifier,

                        Username =
                            username,

                        IsSelected =
                            string.Equals(
                                identifier,
                                _selectedAccountIdentifier,
                                StringComparison.OrdinalIgnoreCase)
                    });
            }


            return result
                .OrderByDescending(
                    account =>
                        account.IsSelected)
                .ThenBy(
                    account =>
                        account.Username,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
        }


        // =====================================================
        // AÑADIR UNA CUENTA NUEVA
        // =====================================================

        public async Task<MSession> AddAccountInteractivelyAsync()
        {
            IXboxGameAccount account =
                _loginHandler
                    .AccountManager
                    .NewAccount();


            MSession session =
                await _loginHandler
                    .AuthenticateInteractively(
                        account);


            string identifier =
                account.Identifier ??
                session.UUID ??
                string.Empty;


            if (string.IsNullOrWhiteSpace(
                    identifier))
            {
                throw new InvalidOperationException(
                    "Microsoft devolvió una cuenta sin identificador.");
            }


            _selectedAccountIdentifier =
                identifier;


            CurrentSession =
                session;


            await SaveSelectedAccountAsync();


            return session;
        }


        // =====================================================
        // CAMBIAR LA CUENTA EN USO
        // =====================================================

        public async Task<bool> SelectAccountAsync(
            string identifier)
        {
            IXboxGameAccount? account =
                FindAccount(
                    identifier);


            if (account == null)
            {
                return false;
            }


            _selectedAccountIdentifier =
                identifier;


            CurrentSession =
                null;


            await SaveSelectedAccountAsync();


            try
            {
                CurrentSession =
                    await _loginHandler
                        .AuthenticateSilently(
                            account);


                return
                    CurrentSession != null;
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
            IXboxGameAccount? account =
                FindAccount(
                    identifier);


            if (account == null)
            {
                throw new InvalidOperationException(
                    "No se encontró la cuenta seleccionada.");
            }


            MSession session =
                await _loginHandler
                    .AuthenticateInteractively(
                        account);


            _selectedAccountIdentifier =
                account.Identifier ??
                session.UUID ??
                identifier;


            CurrentSession =
                session;


            await SaveSelectedAccountAsync();


            return session;
        }


        // =====================================================
        // RESTAURAR SIN MOSTRAR LOGIN
        // =====================================================

        public async Task<bool> TryRestoreSelectedSessionAsync()
        {
            if (string.IsNullOrWhiteSpace(
                    _selectedAccountIdentifier))
            {
                CurrentSession =
                    null;

                return false;
            }


            IXboxGameAccount? account =
                FindAccount(
                    _selectedAccountIdentifier);


            if (account == null)
            {
                CurrentSession =
                    null;

                return false;
            }


            try
            {
                CurrentSession =
                    await _loginHandler
                        .AuthenticateSilently(
                            account);


                return
                    CurrentSession != null;
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
            bool restored =
                await TryRestoreSelectedSessionAsync();


            return
                restored
                    ? CurrentSession
                    : null;
        }


        // =====================================================
        // CERRAR SESIÓN DE UNA CUENTA
        // =====================================================

        public async Task SignOutAccountAsync(
            string identifier)
        {
            IXboxGameAccount? account =
                FindAccount(
                    identifier);


            if (account == null)
            {
                return;
            }


            await _loginHandler
                .Signout(
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


        private async Task LoadSelectedAccountAsync()
        {
            try
            {
                if (!File.Exists(
                        SelectionFilePath))
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
                Path.GetDirectoryName(
                    SelectionFilePath);


            if (!string.IsNullOrWhiteSpace(
                    directory))
            {
                Directory.CreateDirectory(
                    directory);
            }


            AccountSelectionState state =
                new()
                {
                    SelectedAccountIdentifier =
                        _selectedAccountIdentifier ??
                        string.Empty
                };


            string json =
                JsonSerializer.Serialize(
                    state,
                    _jsonOptions);


            string temporaryPath =
                SelectionFilePath +
                ".tmp";


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
