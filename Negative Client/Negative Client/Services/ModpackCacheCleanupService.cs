using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Negative_Client.Services
{
    public sealed class ModpackCacheCleanupService
    {
        public async Task<ModpackCacheCleanupPlan> AnalyzeAsync()
        {
            await ModpackInstallerService.PackageCacheAccessLock
                .WaitAsync();

            try
            {
                return await Task.Run(
                    () =>
                    {
                        string root =
                            InstanceService.PackageCacheRoot;

                        if (!Directory.Exists(root))
                        {
                            return new ModpackCacheCleanupPlan(
                                0,
                                0);
                        }

                        long totalBytes =
                            0;

                        int fileCount =
                            0;

                        foreach (string filePath in
                            EnumerateFilesSafely(root))
                        {
                            fileCount++;

                            try
                            {
                                totalBytes +=
                                    new FileInfo(filePath)
                                        .Length;
                            }
                            catch
                            {
                            }
                        }

                        return new ModpackCacheCleanupPlan(
                            totalBytes,
                            fileCount);
                    });
            }
            finally
            {
                ModpackInstallerService.PackageCacheAccessLock
                    .Release();
            }
        }


        public async Task<ModpackCacheCleanupResult> ExecuteAsync()
        {
            await ModpackInstallerService.PackageCacheAccessLock
                .WaitAsync();

            try
            {
                return await Task.Run(
                    () =>
                    {
                        string root =
                            InstanceService.PackageCacheRoot;

                        if (!Directory.Exists(root))
                        {
                            return new ModpackCacheCleanupResult(
                                0,
                                0,
                                0);
                        }

                        long freedBytes =
                            0;

                        int deletedFiles =
                            0;

                        int failedFiles =
                            0;

                        foreach (string filePath in
                            EnumerateFilesSafely(root))
                        {
                            long fileBytes =
                                0;

                            try
                            {
                                if (File.Exists(filePath))
                                {
                                    fileBytes =
                                        new FileInfo(filePath)
                                            .Length;
                                }

                                File.Delete(filePath);

                                if (!File.Exists(filePath))
                                {
                                    freedBytes +=
                                        fileBytes;

                                    deletedFiles++;
                                }
                                else
                                {
                                    failedFiles++;
                                }
                            }
                            catch
                            {
                                failedFiles++;
                            }
                        }

                        // Quitamos subcarpetas vacías, sin tocar la carpeta packages.
                        foreach (string directoryPath in
                            EnumerateDirectoriesSafely(root)
                                .OrderByDescending(
                                    path => path.Length))
                        {
                            try
                            {
                                Directory.Delete(
                                    directoryPath,
                                    recursive: false);
                            }
                            catch
                            {
                                // Si contiene archivos que no se pudieron borrar, se conserva.
                            }
                        }

                        return new ModpackCacheCleanupResult(
                            freedBytes,
                            deletedFiles,
                            failedFiles);
                    });
            }
            finally
            {
                ModpackInstallerService.PackageCacheAccessLock
                    .Release();
            }
        }


        private static List<string> EnumerateFilesSafely(
            string root)
        {
            List<string> files =
                new();

            if (!Directory.Exists(root))
            {
                return files;
            }

            try
            {
                if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
                {
                    return files;
                }
            }
            catch
            {
                return files;
            }

            foreach (string directory in
                EnumerateDirectoriesSafely(root)
                    .Prepend(root))
            {
                try
                {
                    files.AddRange(
                        Directory.GetFiles(
                            directory,
                            "*",
                            SearchOption.TopDirectoryOnly));
                }
                catch
                {
                }
            }

            return files;
        }


        private static List<string> EnumerateDirectoriesSafely(
            string root)
        {
            List<string> directories =
                new();

            if (!Directory.Exists(root))
            {
                return directories;
            }

            try
            {
                if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
                {
                    return directories;
                }
            }
            catch
            {
                return directories;
            }

            Stack<string> pending =
                new();

            pending.Push(root);

            while (pending.Count > 0)
            {
                string current =
                    pending.Pop();

                string[] children;

                try
                {
                    children =
                        Directory.GetDirectories(
                            current,
                            "*",
                            SearchOption.TopDirectoryOnly);
                }
                catch
                {
                    continue;
                }

                foreach (string child in children)
                {
                    try
                    {
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
                        {
                            continue;
                        }
                    }
                    catch
                    {
                        continue;
                    }

                    directories.Add(child);
                    pending.Push(child);
                }
            }

            return directories;
        }
    }


    public sealed class ModpackCacheCleanupPlan
    {
        public ModpackCacheCleanupPlan(
            long totalBytes,
            int fileCount)
        {
            TotalBytes =
                totalBytes;

            FileCount =
                fileCount;
        }

        public long TotalBytes { get; }

        public int FileCount { get; }
    }


    public sealed class ModpackCacheCleanupResult
    {
        public ModpackCacheCleanupResult(
            long freedBytes,
            int deletedFiles,
            int failedFiles)
        {
            FreedBytes =
                freedBytes;

            DeletedFiles =
                deletedFiles;

            FailedFiles =
                failedFiles;
        }

        public long FreedBytes { get; }

        public int DeletedFiles { get; }

        public int FailedFiles { get; }
    }
}
