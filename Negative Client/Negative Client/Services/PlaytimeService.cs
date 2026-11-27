using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Negative_Client.Models;

namespace Negative_Client.Services
{
    /// <summary>
    /// Registra el tiempo real entre el inicio y el cierre del proceso de Minecraft.
    /// Un proceso auxiliar del propio launcher mantiene el seguimiento aunque
    /// el usuario cierre Negative Client mientras Minecraft sigue abierto.
    /// </summary>
    public sealed class PlaytimeService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        private sealed class PlaytimeRecord
        {
            public long TotalSeconds { get; set; }
            public long SessionStartUtcTicks { get; set; }
            public int ProcessId { get; set; }
            public long ProcessStartUtcTicks { get; set; }
        }

        private static string GetRecordPath(string instanceId)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(instanceId));
            string fileName = Convert.ToHexString(hash) + ".json";
            string directory = Path.Combine(InstanceService.LauncherRoot, "Playtime");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, fileName);
        }

        private static Mutex GetMutex(string instanceId)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(instanceId));
            string name = "NegativeClient_Playtime_" + Convert.ToHexString(hash);
            return new Mutex(false, name);
        }

        private static PlaytimeRecord ReadRecord(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    return JsonSerializer.Deserialize<PlaytimeRecord>(
                        File.ReadAllText(path), JsonOptions) ?? new PlaytimeRecord();
                }
            }
            catch
            {
                // Un archivo de tiempo dañado no debe impedir iniciar el juego.
            }

            return new PlaytimeRecord();
        }

        private static void WriteRecord(string path, PlaytimeRecord record)
        {
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(record, JsonOptions));
            File.Move(temporary, path, true);
        }

        public long GetTotalSeconds(string instanceId)
        {
            string path = GetRecordPath(instanceId);
            using Mutex mutex = GetMutex(instanceId);
            bool acquired = false;

            try
            {
                acquired = mutex.WaitOne(TimeSpan.FromSeconds(5));
                if (!acquired) return 0;
                return Math.Max(0, ReadRecord(path).TotalSeconds);
            }
            catch (AbandonedMutexException)
            {
                return Math.Max(0, ReadRecord(path).TotalSeconds);
            }
            finally
            {
                if (acquired) mutex.ReleaseMutex();
            }
        }

        public void StartMonitoring(string instanceId, Process minecraftProcess)
        {
            if (string.IsNullOrWhiteSpace(instanceId) || minecraftProcess == null)
                return;

            DateTime processStartUtc;
            try
            {
                processStartUtc = minecraftProcess.StartTime.ToUniversalTime();
            }
            catch
            {
                processStartUtc = DateTime.UtcNow;
            }

            string path = GetRecordPath(instanceId);
            using (Mutex mutex = GetMutex(instanceId))
            {
                bool acquired = false;
                try
                {
                    acquired = mutex.WaitOne(TimeSpan.FromSeconds(5));
                    if (!acquired) return;

                    PlaytimeRecord record = ReadRecord(path);
                    // No sobrescribir una sesión que ya está siendo seguida.
                    if (record.SessionStartUtcTicks == 0)
                    {
                        record.SessionStartUtcTicks = DateTime.UtcNow.Ticks;
                        record.ProcessId = minecraftProcess.Id;
                        record.ProcessStartUtcTicks = processStartUtc.Ticks;
                        WriteRecord(path, record);
                    }
                }
                finally
                {
                    if (acquired) mutex.ReleaseMutex();
                }
            }

            string? executable = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executable))
                return;

            try
            {
                ProcessStartInfo monitor = new(executable)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                monitor.ArgumentList.Add("--playtime-monitor");
                monitor.ArgumentList.Add(instanceId);
                monitor.ArgumentList.Add(minecraftProcess.Id.ToString());
                monitor.ArgumentList.Add(processStartUtc.Ticks.ToString());

                Process.Start(monitor);
            }
            catch
            {
                // El evento Exited del launcher actúa como respaldo.
            }
        }

        public void CompleteSession(string instanceId, int processId, long processStartUtcTicks, DateTime? exitUtc = null)
        {
            if (string.IsNullOrWhiteSpace(instanceId)) return;

            string path = GetRecordPath(instanceId);
            using Mutex mutex = GetMutex(instanceId);
            bool acquired = false;

            try
            {
                acquired = mutex.WaitOne(TimeSpan.FromSeconds(10));
                if (!acquired) return;

                PlaytimeRecord record = ReadRecord(path);
                if (record.SessionStartUtcTicks == 0 ||
                    record.ProcessId != processId ||
                    record.ProcessStartUtcTicks != processStartUtcTicks)
                    return;

                DateTime started = new(record.SessionStartUtcTicks, DateTimeKind.Utc);
                DateTime ended = (exitUtc ?? DateTime.UtcNow).ToUniversalTime();
                double elapsed = Math.Max(0, (ended - started).TotalSeconds);

                record.TotalSeconds += (long)Math.Floor(elapsed);
                record.SessionStartUtcTicks = 0;
                record.ProcessId = 0;
                record.ProcessStartUtcTicks = 0;
                WriteRecord(path, record);
            }
            catch (AbandonedMutexException)
            {
                // El siguiente inicio vuelve a leer y guardar el registro.
            }
            finally
            {
                if (acquired) mutex.ReleaseMutex();
            }
        }

        public static async Task RunMonitorAsync(string[] args)
        {
            if (args.Length != 4 ||
                !int.TryParse(args[2], out int processId) ||
                !long.TryParse(args[3], out long processStartTicks))
                return;

            string instanceId = args[1];
            DateTime? exitUtc = null;

            try
            {
                using Process target = Process.GetProcessById(processId);
                DateTime actualStart = target.StartTime.ToUniversalTime();

                if (actualStart.Ticks != processStartTicks)
                    return;

                await target.WaitForExitAsync();
                exitUtc = target.ExitTime.ToUniversalTime();
            }
            catch (ArgumentException)
            {
                // Minecraft terminó antes de que el monitor pudiera conectarse.
                exitUtc = DateTime.UtcNow;
            }
            catch (InvalidOperationException)
            {
                exitUtc = DateTime.UtcNow;
            }

            new PlaytimeService().CompleteSession(
                instanceId, processId, processStartTicks, exitUtc);
        }

        public static string Format(long totalSeconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(Math.Max(0, totalSeconds));
            long days = (long)time.TotalDays;
            int hours = time.Hours;
            int minutes = time.Minutes;

            return $"{days} d  {hours:00} h  {minutes:00} min";
        }
    }
}
