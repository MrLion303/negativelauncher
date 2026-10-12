using System;
using System.Threading;
using System.Threading.Tasks;

namespace Negative_Client.Services
{
    public sealed class DownloadOperationController : IDisposable
    {
        private readonly object _sync =
            new();


        private readonly CancellationTokenSource _stopSource =
            new();


        private CancellationTokenSource? _phaseSource;


        private TaskCompletionSource<bool>? _resumeSource;


        private bool _isPaused;


        public bool IsPaused
        {
            get
            {
                lock (_sync)
                {
                    return _isPaused;
                }
            }
        }


        private readonly CancellationToken _stopToken =
            _stopSource.Token;


        public bool IsStopped =>
            _stopToken.IsCancellationRequested;


        public CancellationToken StopToken =>
            _stopToken;


        public CancellationToken BeginPhase()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(DownloadOperationController));
                }

                _phaseSource?.Dispose();


                _phaseSource =
                    CancellationTokenSource
                        .CreateLinkedTokenSource(
                            _stopToken);


                if (_isPaused)
                {
                    _phaseSource.Cancel();
                }


                return _phaseSource.Token;
            }
        }


        public void EndPhase()
        {
            lock (_sync)
            {
                if (_disposed)
                    return;

                _phaseSource?.Dispose();


                _phaseSource =
                    null;
            }
        }


        public void Pause()
        {
            lock (_sync)
            {
                if (_disposed ||
                    _isPaused ||
                    IsStopped)
                {
                    return;
                }


                _isPaused =
                    true;


                _resumeSource =
                    new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);


                _phaseSource?.Cancel();
            }
        }


        public void Resume()
        {
            TaskCompletionSource<bool>? source;


            lock (_sync)
            {
                if (_disposed || !_isPaused)
                {
                    return;
                }


                _isPaused =
                    false;


                source =
                    _resumeSource;


                _resumeSource =
                    null;
            }


            source?.TrySetResult(
                true);
        }


        public void Stop()
        {
            TaskCompletionSource<bool>? resumeSource;

            lock (_sync)
            {
                if (_disposed)
                    return;

                if (!_stopToken.IsCancellationRequested)
                    _stopSource.Cancel();

                _phaseSource?.Cancel();

                _isPaused = false;
                resumeSource = _resumeSource;
                _resumeSource = null;
            }

            resumeSource?.TrySetResult(true);
        }


        public void ThrowIfStopped()
        {
            _stopToken.ThrowIfCancellationRequested();
        }


        public async Task WaitWhilePausedAsync()
        {
            Task? waitTask =
                null;


            lock (_sync)
            {
                if (!_disposed && _isPaused)
                {
                    _resumeSource ??=
                        new TaskCompletionSource<bool>(
                            TaskCreationOptions.RunContinuationsAsynchronously);


                    waitTask =
                        _resumeSource.Task;
                }
            }


            if (waitTask !=
                null)
            {
                await waitTask
                    .WaitAsync(
                        _stopSource.Token);
            }


            ThrowIfStopped();
        }


        private bool _disposed;


        public void Dispose()
        {
            TaskCompletionSource<bool>? resumeSource;

            lock (_sync)
            {
                if (_disposed)
                    return;

                _disposed = true;

                // La cancelación y la liberación se serializan con Stop/BeginPhase:
                // así no se puede liberar un token mientras otra llamada lo utiliza.
                try
                {
                    _stopSource.Cancel();
                }
                catch (AggregateException)
                {
                    // La limpieza debe completarse incluso si un callback falla.
                }

                try
                {
                    _phaseSource?.Cancel();
                }
                catch (AggregateException)
                {
                    // La limpieza debe completarse incluso si un callback falla.
                }

                _isPaused = false;
                resumeSource = _resumeSource;
                _resumeSource = null;

                _phaseSource?.Dispose();
                _phaseSource = null;
                _stopSource.Dispose();
            }

            resumeSource?.TrySetCanceled();
        }
    }
}
