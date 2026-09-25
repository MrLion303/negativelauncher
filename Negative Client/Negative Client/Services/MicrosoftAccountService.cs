using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;

namespace Negative_Client.Services
{
    public sealed class MicrosoftAccountService
    {
        public static MicrosoftAccountService Instance { get; } =
            new MicrosoftAccountService();


        private readonly JELoginHandler _loginHandler;


        public MSession? CurrentSession { get; private set; }


        public bool IsSignedIn =>
            CurrentSession != null;


        public string Username =>
            CurrentSession?.Username ??
            string.Empty;


        public string Uuid =>
            CurrentSession?.UUID ??
            string.Empty;


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
        // RESTAURAR SESIÓN SIN MOSTRAR LOGIN
        // =====================================================

        public async Task<bool> TryRestoreSessionAsync()
        {
            if (!_loginHandler
                    .AccountManager
                    .GetAccounts()
                    .Any())
            {
                CurrentSession =
                    null;

                return false;
            }


            try
            {
                CurrentSession =
                    await _loginHandler
                        .AuthenticateSilently();

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
        // VALIDAR / REFRESCAR SESIÓN PARA JUGAR
        // =====================================================

        public async Task<MSession?> GetValidSessionAsync()
        {
            if (!_loginHandler
                    .AccountManager
                    .GetAccounts()
                    .Any())
            {
                CurrentSession =
                    null;

                return null;
            }


            try
            {
                CurrentSession =
                    await _loginHandler
                        .AuthenticateSilently();

                return
                    CurrentSession;
            }
            catch
            {
                CurrentSession =
                    null;

                return null;
            }
        }


        // =====================================================
        // LOGIN INTERACTIVO
        // =====================================================

        public async Task<MSession> SignInInteractivelyAsync()
        {
            MSession session =
                await _loginHandler
                    .AuthenticateInteractively();


            CurrentSession =
                session;


            return
                session;
        }


        // =====================================================
        // CERRAR SESIÓN
        // =====================================================

        public async Task SignOutAsync()
        {
            try
            {
                if (_loginHandler
                        .AccountManager
                        .GetAccounts()
                        .Any())
                {
                    await _loginHandler
                        .Signout();
                }
            }
            finally
            {
                CurrentSession =
                    null;
            }
        }
    }
}
