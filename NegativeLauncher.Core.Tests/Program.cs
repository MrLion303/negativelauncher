using System.Text.Json;
using Negative_Client.Models;
using Negative_Client.Services;

string root = Path.Combine(Path.GetTempPath(), "NegativeLauncher.Core.Tests", Guid.NewGuid().ToString("N"));

try
{
    // Los modelos del catálogo y manifiesto deben conservar el formato JSON compartido.
    var catalog = new ModpackCatalog
    {
        Schema = 1,
        Packs = new List<ModpackCatalogEntry>
        {
            new() { Code = "OVERLAND-123", Id = "overland", ManifestFileId = "manifest-file" }
        }
    };
    string catalogJson = JsonSerializer.Serialize(catalog);
    ModpackCatalog? loadedCatalog = JsonSerializer.Deserialize<ModpackCatalog>(catalogJson,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    Assert(loadedCatalog?.Schema == 1 && loadedCatalog.Packs.Count == 1 &&
        loadedCatalog.Packs[0].Code == "OVERLAND-123" && loadedCatalog.Packs[0].ManifestFileId == "manifest-file",
        "El catálogo de modpacks debe serializarse y cargarse conservando sus campos.");

    var manifest = new ModpackManifest
    {
        Id = "overland", Name = "OVERLAND SMP", Version = "1.0.0",
        MinecraftVersion = "1.20.1", Loader = "Forge", LoaderVersion = "47.4.0",
        ArchiveFileId = "archive-file", IconFileId = "icon-file", BackgroundFileId = "background-file"
    };
    string manifestJson = JsonSerializer.Serialize(manifest);
    ModpackManifest? loadedManifest = JsonSerializer.Deserialize<ModpackManifest>(manifestJson,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    Assert(loadedManifest?.Id == "overland" && loadedManifest.MinecraftVersion == "1.20.1" &&
        loadedManifest.Loader == "Forge" && loadedManifest.LoaderVersion == "47.4.0" &&
        loadedManifest.ArchiveFileId == "archive-file" && loadedManifest.IconFileId == "icon-file" &&
        loadedManifest.BackgroundFileId == "background-file",
        "El manifiesto de modpack debe conservar versión, cargador y referencias de archivos.");

    // El lector del catálogo debe poder probarse sin Google Drive ni una interfaz gráfica.
    var catalogFiles = new Dictionary<string, string>
    {
        ["catalogo-prueba"] = """{"schema":1,"packs":[{"code":"OVERLAND-123","id":"overland","manifestFileId":"manifest-prueba"}]}""",
        ["manifest-prueba"] = """{"id":"overland","name":"OVERLAND SMP","version":"1.0.0","minecraftVersion":"1.20.1","loader":"Forge","loaderVersion":"47.4.0","archiveFileId":"archivo-prueba","iconFileId":"icono-prueba","backgroundFileId":"fondo-prueba"}"""
    };
    var catalogReader = new ModpackCatalogReader();
    int catalogDownloads = 0;
    Task<string> DownloadCatalogTextAsync(string fileId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        catalogDownloads++;
        return Task.FromResult(catalogFiles[fileId]);
    }

    ModpackManifest? foundModpack = await catalogReader.FindByCodeAsync(
        "overland-123", "catalogo-prueba", DownloadCatalogTextAsync);
    Assert(foundModpack?.Id == "overland" &&
        foundModpack.Name == "OVERLAND SMP" &&
        foundModpack.MinecraftVersion == "1.20.1" &&
        catalogDownloads == 2,
        "El lector debe encontrar códigos sin distinguir mayúsculas y validar el manifiesto descargado.");

    catalogDownloads = 0;
    ModpackManifest? missingModpack = await catalogReader.FindByCodeAsync(
        "CODIGO-QUE-NO-EXISTE", "catalogo-prueba", DownloadCatalogTextAsync);
    Assert(missingModpack == null && catalogDownloads == 1,
        "Un código desconocido debe devolver null sin descargar un manifiesto.");

    catalogDownloads = 0;
    ModpackManifest? emptyCodeResult = await catalogReader.FindByCodeAsync(
        "  ", "catalogo-prueba", DownloadCatalogTextAsync);
    Assert(emptyCodeResult == null && catalogDownloads == 0,
        "Un código vacío debe terminar sin realizar descargas.");

    catalogFiles["manifest-prueba"] =
        """{"id":"otro-modpack","name":"Modpack incorrecto","minecraftVersion":"1.20.1"}""";
    bool manifestMismatchRejected = false;
    try
    {
        await catalogReader.FindByCodeAsync("OVERLAND-123", "catalogo-prueba", DownloadCatalogTextAsync);
    }
    catch (InvalidOperationException)
    {
        manifestMismatchRejected = true;
    }
    Assert(manifestMismatchRejected,
        "El lector debe rechazar manifiestos cuyo ID no coincide con el catálogo.");

    // La validación de nombres de Minecraft debe funcionar sin WPF ni llamadas de red.
    Assert(MinecraftNameLookupService.IsValidMinecraftUsername("Steve_123"),
        "Debe aceptar nombres Minecraft válidos.");
    Assert(MinecraftNameLookupService.IsValidMinecraftUsername("  Alex  "),
        "Debe validar el nombre después de quitar espacios externos.");
    Assert(!MinecraftNameLookupService.IsValidMinecraftUsername("ab"),
        "Debe rechazar nombres de menos de tres caracteres.");
    Assert(!MinecraftNameLookupService.IsValidMinecraftUsername("nombre con espacios"),
        "Debe rechazar espacios internos.");
    Assert(!MinecraftNameLookupService.IsValidMinecraftUsername("nombre-con-guion"),
        "Debe rechazar guiones medios.");
    Assert(!MinecraftNameLookupService.IsValidMinecraftUsername("nombre demasiado largo"),
        "Debe rechazar nombres que excedan el formato permitido.");
    bool invalidMinecraftNameRejected = false;
    try
    {
        await new MinecraftNameLookupService().LookupAsync("nombre inválido");
    }
    catch (InvalidOperationException)
    {
        invalidMinecraftNameRejected = true;
    }
    Assert(invalidMinecraftNameRejected,
        "La consulta debe rechazar nombres inválidos antes de intentar acceder a la red.");
    var archiveInfo = new ScreenshotArchiveInfo
    {
        InstanceId = "overland",
        InstanceName = "OVERLAND SMP",
        ScreenshotsDirectory = Path.Combine(root, "capturas", "screenshots")
    };
    string archiveInfoJson = JsonSerializer.Serialize(archiveInfo);
    ScreenshotArchiveInfo? loadedArchiveInfo = JsonSerializer.Deserialize<ScreenshotArchiveInfo>(archiveInfoJson,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    Assert(loadedArchiveInfo?.InstanceId == "overland" &&
        loadedArchiveInfo.InstanceName == "OVERLAND SMP" &&
        loadedArchiveInfo.ScreenshotsDirectory == archiveInfo.ScreenshotsDirectory,
        "Los metadatos del archivo de capturas deben conservar la instancia y la ruta.");

    // Los contadores globales conservan el formato que se descarga del repositorio.
    var countdownFeed = new GlobalCountdownFeed
    {
        Schema = 1,
        UpdatedAt = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero),
        Countdowns = new List<GlobalCountdown>
        {
            new()
            {
                Id = "evento-prueba",
                Name = "Evento de prueba",
                EndAtUtc = new DateTimeOffset(2026, 10, 9, 18, 30, 0, TimeSpan.Zero),
                Active = true
            }
        }
    };
    string countdownJson = JsonSerializer.Serialize(countdownFeed);
    GlobalCountdownFeed? loadedCountdownFeed = JsonSerializer.Deserialize<GlobalCountdownFeed>(
        countdownJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    Assert(loadedCountdownFeed?.Schema == 1 &&
        loadedCountdownFeed.Countdowns.Count == 1 &&
        loadedCountdownFeed.Countdowns[0].Id == "evento-prueba" &&
        loadedCountdownFeed.Countdowns[0].Name == "Evento de prueba" &&
        loadedCountdownFeed.Countdowns[0].Active &&
        loadedCountdownFeed.Countdowns[0].EndAtUtc == countdownFeed.Countdowns[0].EndAtUtc &&
        loadedCountdownFeed.UpdatedAt == countdownFeed.UpdatedAt,
        "El feed de contadores debe conservar el esquema, las fechas UTC y el estado de cada contador.");

    // El archivo de capturas debe mover imágenes, conservar sus metadatos y limpiarse al quedar vacío.
    string screenshotRoot = Path.Combine(root, "prueba-archivo-capturas");
    string originalScreenshots = Path.Combine(screenshotRoot, "instancia", "screenshots");
    Directory.CreateDirectory(originalScreenshots);
    string originalImage = Path.Combine(originalScreenshots, "captura.png");
    await File.WriteAllBytesAsync(originalImage, new byte[] { 1, 2, 3 });
    var screenshotStore = new ScreenshotArchiveStore(Path.Combine(screenshotRoot, "archivadas"));
    string archivedScreenshots = screenshotStore.ArchiveInstanceScreenshots(
        "instancia-prueba", "Instancia de prueba", originalScreenshots);
    Assert(!Directory.Exists(originalScreenshots) &&
        File.Exists(Path.Combine(archivedScreenshots, "captura.png")),
        "Archivar capturas debe mover la carpeta original al archivo.");
    ScreenshotArchiveInfo[] screenshotArchives = screenshotStore.GetArchives();
    Assert(screenshotArchives.Length == 1 &&
        screenshotArchives[0].InstanceId == "instancia-prueba" &&
        screenshotArchives[0].InstanceName == "Instancia de prueba" &&
        screenshotArchives[0].ScreenshotsDirectory == archivedScreenshots,
        "El archivo debe recuperar los metadatos de la instancia archivada.");
    string archivedImage = Path.Combine(archivedScreenshots, "captura.png");
    File.Delete(archivedImage);
    screenshotStore.CleanupArchiveIfEmpty(archivedImage);
    Assert(screenshotStore.GetArchives().Length == 0 &&
        !Directory.Exists(Path.GetDirectoryName(archivedScreenshots)),
        "El archivo vacío debe eliminarse después de borrar su última imagen.");

    // El perfil offline conserva el formato y permite probarse sin tocar la carpeta real del launcher.

    // La migración mueve solo archivos compartibles y conserva las copias ya existentes.
    string migrationRoot = Path.Combine(root, "prueba-migracion");
    string legacyInstance = Path.Combine(migrationRoot, "instances", "instancia-antigua");
    string sharedRoot = Path.Combine(migrationRoot, "minecraft");
    string legacyAsset = Path.Combine(legacyInstance, "assets", "objects", "a", "asset.bin");
    string sharedAsset = Path.Combine(sharedRoot, "assets", "objects", "a", "asset.bin");
    string legacyLibrary = Path.Combine(legacyInstance, "libraries", "ejemplo", "1.0", "lib.jar");
    string sharedLibrary = Path.Combine(sharedRoot, "libraries", "ejemplo", "1.0", "lib.jar");
    string legacyRuntime = Path.Combine(legacyInstance, "runtime", "bin", "java");
    string legacyConfig = Path.Combine(legacyInstance, "config", "mod-config.txt");
    Directory.CreateDirectory(Path.GetDirectoryName(legacyAsset)!);
    Directory.CreateDirectory(Path.GetDirectoryName(legacyLibrary)!);
    Directory.CreateDirectory(Path.GetDirectoryName(sharedLibrary)!);
    Directory.CreateDirectory(Path.GetDirectoryName(legacyRuntime)!);
    Directory.CreateDirectory(Path.GetDirectoryName(legacyConfig)!);
    await File.WriteAllTextAsync(legacyAsset, "asset-antiguo");
    await File.WriteAllTextAsync(legacyLibrary, "libreria-antigua");
    await File.WriteAllTextAsync(sharedLibrary, "libreria-compartida");
    await File.WriteAllTextAsync(legacyRuntime, "java-runtime");
    await File.WriteAllTextAsync(legacyConfig, "config-de-mod");
    var migration = new MinecraftDirectoryMigration(
        Path.Combine(sharedRoot, "assets"),
        Path.Combine(sharedRoot, "libraries"),
        Path.Combine(sharedRoot, "versions"),
        Path.Combine(sharedRoot, "runtime"));
    migration.MigrateLegacyRuntimeForDirectory(legacyInstance);
    Assert(File.Exists(sharedAsset) && await File.ReadAllTextAsync(sharedAsset) == "asset-antiguo" &&
        !File.Exists(legacyAsset),
        "La migración debe mover assets antiguos al almacenamiento compartido.");
    Assert(await File.ReadAllTextAsync(sharedLibrary) == "libreria-compartida" &&
        File.Exists(legacyLibrary),
        "Si una biblioteca ya existe en la carpeta compartida, debe conservar ambas copias sin sobrescribir.");
    Assert(File.Exists(Path.Combine(sharedRoot, "runtime", "bin", "java")) &&
        !File.Exists(legacyRuntime),
        "La migración debe mover el runtime antiguo al almacenamiento compartido.");
    Assert(await File.ReadAllTextAsync(legacyConfig) == "config-de-mod",
        "La migración no debe tocar la configuración de mods de la instancia.");

    Directory.CreateDirectory(root);
    string offlineAccountRoot = Path.Combine(root, "cuenta-offline");
    var offlineAccounts = new OfflineAccountService(offlineAccountRoot);
    string sourceSkin = Path.Combine(root, "skin-prueba.png");
    await File.WriteAllBytesAsync(sourceSkin, new byte[] { 1, 2, 3, 4 });
    var savedOfflineProfile = await offlineAccounts.SaveAsync("  JugadorPrueba  ", sourceSkin, "SLIM");
    Assert(savedOfflineProfile.Username == "JugadorPrueba" && savedOfflineProfile.SkinModel == "slim",
        "El perfil offline debe normalizar el nombre y el modelo de skin.");
    Assert(File.Exists(Path.Combine(offlineAccountRoot, "accounts", "offline-profile.json")) &&
        File.Exists(Path.Combine(offlineAccountRoot, "accounts", "offline-skin.png")),
        "El perfil y su skin deben conservar sus nombres y ubicación.");
    var loadedOfflineProfile = await offlineAccounts.LoadAsync();
    Assert(loadedOfflineProfile?.Username == "JugadorPrueba" &&
        loadedOfflineProfile.SkinModel == "slim" &&
        File.Exists(loadedOfflineProfile.SkinFilePath),
        "El perfil offline debe poder guardarse y cargarse con su skin.");
    await offlineAccounts.SaveAsync("JugadorPrueba", null, "otro-valor", removeSkin: true);
    var profileWithoutSkin = await offlineAccounts.LoadAsync();
    Assert(profileWithoutSkin?.SkinModel == "wide" &&
        string.IsNullOrEmpty(profileWithoutSkin.SkinFilePath) &&
        !File.Exists(Path.Combine(offlineAccountRoot, "accounts", "offline-skin.png")),
        "Quitar la skin debe mantener el perfil y normalizar el modelo desconocido a wide.");
    await offlineAccounts.DeleteAsync();
    Assert(await offlineAccounts.LoadAsync() == null,
        "Eliminar la cuenta offline debe borrar el perfil guardado.");

    // El modelo de cuentas mantiene los nombres y las notificaciones usados por la interfaz.
    var microsoftInfo = new MicrosoftAccountInfo
    {
        Identifier = "cuenta@example.com",
        Username = "JugadorPremium",
        IsSelected = true
    };
    Assert(microsoftInfo.DisplayName == "JugadorPremium  •  EN USO",
        "El nombre visible de una cuenta premium debe conservar su formato.");
    microsoftInfo.IsOffline = true;
    Assert(microsoftInfo.DisplayName == "JugadorPremium  •  NO PREMIUM  •  EN USO",
        "El nombre visible de una cuenta offline debe conservar sus etiquetas.");
    bool skinPathNotified = false;
    microsoftInfo.PropertyChanged += (_, args) =>
        skinPathNotified |= args.PropertyName == nameof(MicrosoftAccountInfo.SkinHeadPath);
    microsoftInfo.SkinHeadPath = "/tmp/skin.png";
    Assert(skinPathNotified && microsoftInfo.SkinHeadPath == "/tmp/skin.png",
        "Cambiar la ruta de la cabeza debe notificar a la interfaz.");
    microsoftInfo.SkinHeadPath = "/TMP/SKIN.PNG";
    Assert(microsoftInfo.SkinHeadPath == "/tmp/skin.png",
        "La comparación de rutas de skin debe seguir ignorando diferencias de mayúsculas.");
    Assert(Path.IsPathRooted(LauncherPaths.DefaultLauncherRoot),
        "La carpeta de datos predeterminada debe ser una ruta absoluta.");
    Assert(Path.GetFileName(LauncherPaths.DefaultLauncherRoot) == "NegativeClient",
        "La carpeta predeterminada debe conservar el nombre NegativeClient.");
    if (OperatingSystem.IsWindows())
    {
        string originalWindowsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NegativeClient");
        Assert(string.Equals(
            Path.GetFullPath(LauncherPaths.DefaultLauncherRoot),
            Path.GetFullPath(originalWindowsRoot),
            StringComparison.OrdinalIgnoreCase),
            "En Windows debe conservarse exactamente la carpeta de datos original.");
    }

    // El repositorio debe seguir la ruta vigente si cambia la ubicación configurada.
    string dynamicInstancesRoot = Path.Combine(root, "ubicacion-inicial", "instances");
    var dynamicStore = new InstanceDataStore(() => dynamicInstancesRoot);
    await dynamicStore.SaveAsync(new InstalledInstance { Id = "antes-del-cambio", Name = "Antes" });
    string previousInstancePath = Path.Combine(dynamicInstancesRoot, "antes-del-cambio", "instance.json");
    dynamicInstancesRoot = Path.Combine(root, "ubicacion-nueva", "instances");
    await dynamicStore.SaveAsync(new InstalledInstance { Id = "despues-del-cambio", Name = "Después" });
    Assert(File.Exists(previousInstancePath),
        "Cambiar la ruta no debe borrar automáticamente la ubicación anterior.");
    Assert(File.Exists(Path.Combine(dynamicInstancesRoot, "despues-del-cambio", "instance.json")),
        "El repositorio debe usar la nueva ubicación en las operaciones posteriores.");

    // El almacenamiento compartido mantiene instance.json y omite archivos dañados.
    string instancesRoot = Path.Combine(root, "repositorio-instancias", "instances");
    var instanceStore = new InstanceDataStore(() => instancesRoot);
    var storedInstance = new InstalledInstance
    {
        Id = "guardada-01",
        Name = "Instancia guardada",
        MinecraftVersion = "1.20.1",
        Loader = "Forge",
        LoaderVersion = "47.4.0",
        LaunchVersionName = "1.20.1-forge-47.4.0",
        RuntimePrepared = true
    };
    await instanceStore.SaveAsync(storedInstance);
    Assert(File.Exists(Path.Combine(instancesRoot, "guardada-01", "instance.json")),
        "El repositorio debe conservar la ruta instance.json.");
    List<InstalledInstance> storedInstances = await instanceStore.LoadAllAsync();
    Assert(storedInstances.Count == 1 && storedInstances[0].Id == "guardada-01" &&
        storedInstances[0].LaunchVersionName == storedInstance.LaunchVersionName,
        "El repositorio debe recuperar la instancia guardada.");

    string damagedDirectory = Path.Combine(instancesRoot, "instancia-danada");
    Directory.CreateDirectory(damagedDirectory);
    await File.WriteAllTextAsync(Path.Combine(damagedDirectory, "instance.json"), "{ json roto");
    List<InstalledInstance> afterDamagedFile = await instanceStore.LoadAllAsync();
    Assert(afterDamagedFile.Count == 1 && afterDamagedFile[0].Id == "guardada-01",
        "Una instancia con JSON dañado no debe impedir cargar las demás.");

    bool rejectedUnsafeStoredId = false;
    try
    {
        await instanceStore.SaveAsync(new InstalledInstance { Id = "../fuera" });
    }
    catch (InvalidOperationException)
    {
        rejectedUnsafeStoredId = true;
    }
    Assert(rejectedUnsafeStoredId, "El repositorio no debe permitir guardar IDs que salgan de la carpeta de instancias.");

    // El modelo de instancia compartido conserva nombres y valores serializados.
    var instanceModel = new InstalledInstance
    {
        Id = "instancia-prueba",
        Name = "Prueba",
        InstallCode = "pack-001",
        IsInstalled = true,
        RuntimePrepared = true,
        LaunchVersionName = "1.20.1-forge-47.4.0",
        InstalledVersion = "1.20.1-forge-47.4.0",
        MinecraftVersion = "1.20.1",
        Loader = "Forge",
        LoaderVersion = "47.4.0",
        IconFileId = "icono-prueba",
        BackgroundFileId = "fondo-prueba"
    };
    string instanceJson = JsonSerializer.Serialize(instanceModel);
    using (JsonDocument instanceDocument = JsonDocument.Parse(instanceJson))
    {
        JsonElement instanceProperties = instanceDocument.RootElement;
        Assert(instanceProperties.GetProperty("Id").GetString() == "instancia-prueba",
            "El modelo debe conservar la propiedad Id.");
        Assert(instanceProperties.GetProperty("LaunchVersionName").GetString() == "1.20.1-forge-47.4.0",
            "El modelo debe conservar LaunchVersionName.");
        Assert(instanceProperties.GetProperty("RuntimePrepared").GetBoolean(),
            "El modelo debe conservar RuntimePrepared.");
        Assert(instanceProperties.GetProperty("BackgroundFileId").GetString() == "fondo-prueba",
            "El modelo debe conservar BackgroundFileId.");
    }
    InstalledInstance? restoredInstance = JsonSerializer.Deserialize<InstalledInstance>(instanceJson);
    Assert(restoredInstance?.Id == instanceModel.Id &&
        restoredInstance.Name == instanceModel.Name &&
        restoredInstance.InstallCode == instanceModel.InstallCode &&
        restoredInstance.IsInstalled == instanceModel.IsInstalled &&
        restoredInstance.RuntimePrepared == instanceModel.RuntimePrepared &&
        restoredInstance.LaunchVersionName == instanceModel.LaunchVersionName &&
        restoredInstance.InstalledVersion == instanceModel.InstalledVersion &&
        restoredInstance.MinecraftVersion == instanceModel.MinecraftVersion &&
        restoredInstance.Loader == instanceModel.Loader &&
        restoredInstance.LoaderVersion == instanceModel.LoaderVersion &&
        restoredInstance.IconFileId == instanceModel.IconFileId &&
        restoredInstance.BackgroundFileId == instanceModel.BackgroundFileId,
        "El modelo de instancia debe sobrevivir un ciclo JSON sin perder propiedades.");

    // El mapa de carpetas conserva la estructura de almacenamiento original.
    var layout = new LauncherStorageLayout(root);
    Assert(layout.Root == Path.GetFullPath(root), "El almacenamiento debe normalizar su ruta raíz.");
    Assert(layout.InstancesRoot == Path.Combine(layout.Root, "instances"),
        "Las instancias deben conservar su carpeta.");
    Assert(layout.PackageCacheRoot == Path.Combine(layout.Root, "cache", "packages"),
        "La caché de paquetes debe conservar su ubicación.");
    Assert(layout.TempRoot == Path.Combine(layout.Root, "temp"),
        "Los temporales deben conservar su ubicación.");
    Assert(layout.SharedMinecraftAssetsRoot == Path.Combine(layout.Root, "minecraft", "assets"),
        "Los assets compartidos deben conservar su ubicación.");
    Assert(layout.SharedMinecraftLibrariesRoot == Path.Combine(layout.Root, "minecraft", "libraries"),
        "Las bibliotecas compartidas deben conservar su ubicación.");
    Assert(layout.SharedMinecraftVersionsRoot == Path.Combine(layout.Root, "minecraft", "versions"),
        "Las versiones compartidas deben conservar su ubicación.");
    Assert(layout.SharedMinecraftRuntimeRoot == Path.Combine(layout.Root, "minecraft", "runtime"),
        "El runtime compartido debe conservar su ubicación.");

    bool rejectedEmptyStorageRoot = false;
    try
    {
        _ = new LauncherStorageLayout(" ");
    }
    catch (ArgumentException)
    {
        rejectedEmptyStorageRoot = true;
    }
    Assert(rejectedEmptyStorageRoot, "No debe aceptarse una ruta de almacenamiento vacía.");

    string fallbackRoot = Path.Combine(root, "predeterminado");
    Assert(LauncherStorageLayout.NormalizeRoot(null, fallbackRoot) == Path.GetFullPath(fallbackRoot)
        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
        "Una ruta no configurada debe volver a la ubicación predeterminada.");
    Assert(LauncherStorageLayout.NormalizeRoot("  ", fallbackRoot) == Path.GetFullPath(fallbackRoot)
        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
        "Una ruta en blanco debe volver a la ubicación predeterminada.");
    Assert(LauncherStorageLayout.NormalizeRoot("  carpeta-prueba  ", fallbackRoot) ==
        Path.GetFullPath("carpeta-prueba").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
        "La ruta configurada debe quitar espacios exteriores y resolverse como ruta absoluta.");

    Assert(LauncherPathRules.PathsEqual(Path.Combine(root, "ruta"), Path.Combine(root, "ruta", ".")),
        "Las rutas equivalentes deben reconocerse como iguales.");
    Assert(LauncherPathRules.IsSameOrSubPath(Path.Combine(root, "padre", "hijo"), Path.Combine(root, "padre")),
        "Debe reconocerse una ruta ubicada dentro de otra.");
    Assert(!LauncherPathRules.IsSameOrSubPath(Path.Combine(root, "padre-extra"), Path.Combine(root, "padre")),
        "Una carpeta cuyo nombre solo comparte el prefijo no debe tratarse como subcarpeta.");

    LauncherPathRules.ValidateInstanceId("instancia-1_a");
    bool rejectedEmptyInstanceId = false;
    try
    {
        LauncherPathRules.ValidateInstanceId(" ");
    }
    catch (InvalidOperationException)
    {
        rejectedEmptyInstanceId = true;
    }
    Assert(rejectedEmptyInstanceId, "No debe aceptarse un ID de instancia vacío.");
    bool rejectedUnsafeInstanceId = false;
    try
    {
        LauncherPathRules.ValidateInstanceId("../otra-carpeta");
    }
    catch (InvalidOperationException)
    {
        rejectedUnsafeInstanceId = true;
    }
    Assert(rejectedUnsafeInstanceId, "El ID de instancia no debe permitir separadores de ruta.");

    LauncherPathRules.ValidateStorageRootChange(
        Path.Combine(root, "nuevo-almacenamiento"),
        Path.Combine(root, "actual", "instances"),
        Path.Combine(root, "actual", "minecraft"));
    bool rejectedInstancesChild = false;
    try
    {
        LauncherPathRules.ValidateStorageRootChange(
            Path.Combine(root, "actual", "instances", "anidado"),
            Path.Combine(root, "actual", "instances"),
            Path.Combine(root, "actual", "minecraft"));
    }
    catch (InvalidOperationException)
    {
        rejectedInstancesChild = true;
    }
    Assert(rejectedInstancesChild, "No debe permitirse colocar el nuevo almacenamiento dentro de las instancias actuales.");

    bool rejectedMinecraftChild = false;
    try
    {
        LauncherPathRules.ValidateStorageRootChange(
            Path.Combine(root, "actual", "minecraft", "anidado"),
            Path.Combine(root, "actual", "instances"),
            Path.Combine(root, "actual", "minecraft"));
    }
    catch (InvalidOperationException)
    {
        rejectedMinecraftChild = true;
    }
    Assert(rejectedMinecraftChild, "No debe permitirse colocar el nuevo almacenamiento dentro de Minecraft actual.");

    var service = new LauncherPreferencesService(root);

    // Primera ejecución: crea preferencias por defecto en el formato existente.
    LauncherPreferences defaults = await service.LoadAsync();
    Assert(defaults.MaximumRamMb == 4096, "La RAM por defecto debe ser 4096 MB.");
    Assert(defaults.UseAutomaticJava, "Java automático debe estar habilitado por defecto.");
    Assert(defaults.AccountMode == "premium", "El modo de cuenta por defecto debe seguir siendo premium.");
    Assert(File.Exists(Path.Combine(root, "launcher-preferences.json")), "La primera carga debe crear el archivo.");

    // El JSON anterior debe seguir leyendo propiedades y valores conocidos.
    string path = Path.Combine(root, "launcher-preferences.json");
    await File.WriteAllTextAsync(path, """
    {
      "maximumrammb": 500,
      "useautomaticjava": false,
      "customjavapath": null,
      "customjavaarguments": "-Xmx4G",
      "developerminecraftversion": null,
      "storagerootpath": null,
      "accountmode": "offline"
    }
    """);
    LauncherPreferences loaded = await service.LoadAsync();
    Assert(loaded.MaximumRamMb == 1024, "La RAM debe limitarse al mínimo existente.");
    Assert(!loaded.UseAutomaticJava, "Debe conservarse el valor de Java automático.");
    Assert(loaded.CustomJavaPath == string.Empty, "Las rutas nulas deben normalizarse.");
    Assert(loaded.CustomJavaArguments == "-Xmx4G", "Deben conservarse los argumentos personalizados.");
    Assert(loaded.AccountMode == "offline", "Debe conservarse el modo de cuenta.");

    // Guardado: normaliza RAM y mantiene los nombres de propiedades serializados.
    loaded.MaximumRamMb = 40000;
    loaded.DeveloperMode = true;
    await service.SaveAsync(loaded);
    string savedJson = await File.ReadAllTextAsync(path);
    using (JsonDocument document = JsonDocument.Parse(savedJson))
    {
        Assert(document.RootElement.GetProperty("MaximumRamMb").GetInt32() == 32768, "La RAM debe limitarse al máximo existente.");
        Assert(document.RootElement.GetProperty("DeveloperMode").GetBoolean(), "Debe guardarse el modo de desarrollador.");
        Assert(document.RootElement.GetProperty("AccountMode").GetString() == "offline", "El archivo debe mantener AccountMode.");
    }

    // Un archivo dañado no debe impedir que abra el launcher.
    await File.WriteAllTextAsync(path, "{ archivo dañado");
    LauncherPreferences recovered = await service.LoadAsync();
    Assert(recovered.MaximumRamMb == 4096, "Un JSON dañado debe devolver valores por defecto.");

    // El estado de la última instancia mantiene el archivo y la propiedad originales.
    var stateService = new LauncherStateService(root);
    string? lastPlayed = await stateService.GetLastPlayedInstanceIdAsync();
    Assert(lastPlayed is null, "Sin archivo de estado debe devolver null.");

    await stateService.SetLastPlayedInstanceIdAsync("instancia-prueba");
    Assert(await stateService.GetLastPlayedInstanceIdAsync() == "instancia-prueba",
        "Debe guardar y recuperar el ID de la última instancia.");

    string statePath = Path.Combine(root, "launcher-state.json");
    using (JsonDocument stateDocument = JsonDocument.Parse(await File.ReadAllTextAsync(statePath)))
    {
        Assert(stateDocument.RootElement.GetProperty("LastPlayedInstanceId").GetString() == "instancia-prueba",
            "Debe conservar el nombre de propiedad del archivo de estado existente.");
    }

    await stateService.ClearLastPlayedInstanceAsync();
    Assert(await stateService.GetLastPlayedInstanceIdAsync() is null,
        "Al limpiar el estado no debe quedar una instancia seleccionada.");

    await File.WriteAllTextAsync(statePath, "{ estado dañado");
    Assert(await stateService.GetLastPlayedInstanceIdAsync() is null,
        "Un estado JSON dañado debe ignorarse sin bloquear el launcher.");

    bool rejectedEmptyId = false;
    try
    {
        await stateService.SetLastPlayedInstanceIdAsync(" ");
    }
    catch (ArgumentException)
    {
        rejectedEmptyId = true;
    }
    Assert(rejectedEmptyId, "No debe guardarse un ID de instancia vacío.");

    // La enumeración debe ignorar carpetas sin instance.json y aceptar JSON con casing antiguo.
    string emptyInstanceDirectory = Path.Combine(instancesRoot, "carpeta-sin-json");
    Directory.CreateDirectory(emptyInstanceDirectory);
    string legacyJsonDirectory = Path.Combine(instancesRoot, "instancia-json-antiguo");
    Directory.CreateDirectory(legacyJsonDirectory);
    await File.WriteAllTextAsync(
        Path.Combine(legacyJsonDirectory, "instance.json"),
        """{"id":"json-antiguo","name":"Formato anterior","minecraftVersion":"1.20.1"}""");
    List<InstalledInstance> afterCompatibilityCases = await instanceStore.LoadAllAsync();
    Assert(afterCompatibilityCases.Count == 2 &&
        afterCompatibilityCases.Any(instance => instance.Id == "guardada-01") &&
        afterCompatibilityCases.Any(instance => instance.Id == "json-antiguo" &&
            instance.Name == "Formato anterior"),
        "La enumeración debe omitir carpetas sin instance.json, ignorar JSON dañado y aceptar nombres de propiedades con mayúsculas/minúsculas distintas.");

    // El controlador de operaciones conserva pausa, reanudación y cancelación.
    using (var operationController = new DownloadOperationController())
    {
        CancellationToken phaseToken = operationController.BeginPhase();
        operationController.Pause();
        Assert(operationController.IsPaused && phaseToken.IsCancellationRequested,
            "Pausar una operación debe cancelar la fase actual.");
        Task pauseWait = operationController.WaitWhilePausedAsync();
        Assert(!pauseWait.IsCompleted,
            "La operación debe esperar mientras esté pausada.");
        operationController.Resume();
        await pauseWait;
        Assert(!operationController.IsPaused,
            "Reanudar debe liberar la espera de pausa.");

        CancellationToken activePhaseToken = operationController.BeginPhase();
        operationController.Stop();
        Assert(operationController.IsStopped && operationController.StopToken.IsCancellationRequested,
            "Detener debe cancelar el token global de la operación.");
        Assert(activePhaseToken.IsCancellationRequested,
            "Detener debe cancelar también la fase activa.");
    }

    Console.WriteLine("Correcto: comprobaciones de preferencias, estado y almacenamiento completadas.");
}
finally
{
    try
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
    catch
    {
        // La limpieza temporal no debe ocultar el resultado de las pruebas.
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
