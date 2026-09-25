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


        public bool IsStopped =>
            _stopSource.IsCancellationRequested;


        public CancellationToken StopToken =>
            _stopSource.Token;


        public CancellationToken BeginPhase()
        {
            lock (_sync)
            {
                _phaseSource?.Dispose();


                _phaseSource =
                    CancellationTokenSource
                        .CreateLinkedTokenSource(
                            _stopSource.Token);


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
                _phaseSource?.Dispose();


                _phaseSource =
                    null;
            }
        }


        public void Pause()
        {
            lock (_sync)
            {
                if (_isPaused ||
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
                if (!_isPaused)
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
            if (!_stopSource.IsCancellationRequested)
            {
                _stopSource.Cancel();
            }


            lock (_sync)
            {
                _phaseSource?.Cancel();
            }


            Resume();
        }


        public void ThrowIfStopped()
        {
            _stopSource.Token
                .ThrowIfCancellationRequested();
        }


        public async Task WaitWhilePausedAsync()
        {
            Task? waitTask =
                null;


            lock (_sync)
            {
                if (_isPaused)
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


        public void Dispose()
        {
            try
            {
                _phaseSource?.Dispose();
            }            catch
            {
            }


            _stopSource.Dispose();
        }
    }
}
