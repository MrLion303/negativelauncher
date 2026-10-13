using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using Negative_Client.Models;
using Negative_Client.Services;

string root = Path.Combine(Path.GetTempPath(), "NegativeLauncher.Core.Tests", Guid.NewGuid().ToString("N"));

try
{
    // La configuración MSAL es un identificador público, no un secreto.
    Assert(MicrosoftOAuthClientConfiguration.TryNormalizeClientId(
            "AABBCCDD-AABB-CCDD-AABB-CCDDEEFF0011", out string normalizedClientId) &&
        normalizedClientId == "aabbccdd-aabb-ccdd-aabb-ccddeeff0011",
        "Un Client ID válido debe normalizarse a GUID.");
    Assert(!MicrosoftOAuthClientConfiguration.TryNormalizeClientId("no-es-un-guid", out _) &&
        !MicrosoftOAuthClientConfiguration.TryNormalizeClientId(Guid.Empty.ToString(), out _),
        "Se deben rechazar Client IDs vacíos o mal formados.");
    Assert(MicrosoftOAuthClientConfiguration.ResolveClientId(
            "11111111-2222-3333-4444-555555555555",
            "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee") == "11111111-2222-3333-4444-555555555555",
        "La variable de entorno válida debe prevalecer sobre la preferencia guardada.");
    Assert(MicrosoftOAuthClientConfiguration.ResolveClientId(
            "valor-incorrecto",
            "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee") == "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" &&
        MicrosoftOAuthClientConfiguration.ResolveClientId(
            null,
            "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee") == "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" &&
        MicrosoftOAuthClientConfiguration.ResolveClientId(null, null) is null,
        "Una variable de entorno inválida debe permitir recuperar la preferencia guardada.");

    // La política de almacenamiento debe mantener Windows y usar rutas nativas en Linux/macOS.
    Assert(LauncherPaths.ResolveDefaultLauncherRoot(
            isWindows: true, isMacOS: false,
            home: "/home/pruebas", applicationData: "/windows/appdata", xdgDataHome: "/xdg") ==
        Path.Combine("/windows/appdata", "NegativeClient"),
        "El port debe conservar la carpeta de datos de Windows sin dejar que XDG la cambie.");
    Assert(LauncherPaths.ResolveDefaultLauncherRoot(
            isWindows: false, isMacOS: true,
            home: "/Users/pruebas", applicationData: "/fallback", xdgDataHome: "/xdg") ==
        Path.Combine("/Users/pruebas", "Library", "Application Support", "NegativeClient"),
        "macOS debe usar Application Support en lugar de la ruta XDG de Linux.");
    Assert(LauncherPaths.ResolveDefaultLauncherRoot(
            isWindows: false, isMacOS: false,
            home: "/home/pruebas", applicationData: "/fallback", xdgDataHome: "/datos/xdg") ==
        Path.Combine("/datos/xdg", "NegativeClient"),
        "Linux debe usar XDG_DATA_HOME cuando es una ruta absoluta.");
    Assert(LauncherPaths.ResolveDefaultLauncherRoot(
            isWindows: false, isMacOS: false,
            home: "/home/pruebas", applicationData: "/fallback", xdgDataHome: "ruta/relativa") ==
        Path.Combine("/home/pruebas", ".local", "share", "NegativeClient"),
        "Linux debe ignorar XDG_DATA_HOME si contiene una ruta relativa.");
    Assert(LauncherPaths.ResolveDefaultLauncherRoot(
            isWindows: false, isMacOS: false,
            home: null, applicationData: null, xdgDataHome: null) ==
        Path.Combine(Path.GetTempPath(), "NegativeClient"),
        "Debe existir un fallback válido si faltan HOME, ApplicationData y XDG_DATA_HOME.");

    string skinTextureDocument = JsonSerializer.Serialize(new
    {
        textures = new { SKIN = new { url = "http://textures.minecraft.net/texture/test-hash" } }
    });
    string encodedSkinTexture = Convert.ToBase64String(Encoding.UTF8.GetBytes(skinTextureDocument));
    string skinProfileJson = JsonSerializer.Serialize(new
    {
        properties = new[] { new { name = "textures", value = encodedSkinTexture } }
    });
    Assert(MinecraftSkinTextureService.TryGetSkinTextureUrl(skinProfileJson, out Uri skinTextureUri) &&
        skinTextureUri.Scheme == Uri.UriSchemeHttps &&
        skinTextureUri.Host == "textures.minecraft.net" &&
        skinTextureUri.AbsolutePath == "/texture/test-hash",
        "El servicio de skins debe validar el host oficial y normalizar URLs antiguas a HTTPS.");

    string maliciousSkinTextureDocument = JsonSerializer.Serialize(new
    {
        textures = new { SKIN = new { url = "https://example.invalid/fake-skin.png" } }
    });
    string encodedMaliciousSkin = Convert.ToBase64String(Encoding.UTF8.GetBytes(maliciousSkinTextureDocument));
    string maliciousProfileJson = JsonSerializer.Serialize(new
    {
        properties = new[] { new { name = "textures", value = encodedMaliciousSkin } }
    });
    Assert(!MinecraftSkinTextureService.TryGetSkinTextureUrl(maliciousProfileJson, out _) &&
        !MinecraftSkinTextureService.TryGetSkinTextureUrl("not-json", out _),
        "El servicio de skins debe rechazar hosts arbitrarios y perfiles malformados.");

    foreach (string disallowedUrl in new[]
    {
        "https://textures.minecraft.net:8443/texture/test-hash",
        "https://user@textures.minecraft.net/texture/test-hash",
        "https://textures.minecraft.net.example.invalid/texture/test-hash"
    })
    {
        string rejectedDocument = JsonSerializer.Serialize(new
        {
            textures = new { SKIN = new { url = disallowedUrl } }
        });
        string rejectedValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(rejectedDocument));
        string rejectedProfile = JsonSerializer.Serialize(new
        {
            properties = new[] { new { name = "textures", value = rejectedValue } }
        });
        Assert(!MinecraftSkinTextureService.TryGetSkinTextureUrl(rejectedProfile, out _),
            "Las texturas con puertos ajenos, credenciales o dominios engañosos deben rechazarse.");
    }

    // El calendario de temporada compartido debe dar el mismo resultado a WPF y Avalonia.
    var christmasTheme = LauncherHolidayCalendar.GetAutomaticState(new DateTime(2026, 12, 25));
    Assert(christmasTheme.Theme == LauncherHolidayTheme.Christmas &&
        christmasTheme.PrimaryHex == "#E83646" && christmasTheme.SecondaryHex == "#34BC5E" &&
        !christmasTheme.ShowFireworks && !christmasTheme.ShowBalloons,
        "La fecha navideña debe conservar la paleta y no activar efectos fuera de Año Nuevo.");

    var newYearEveTheme = LauncherHolidayCalendar.GetAutomaticState(new DateTime(2026, 12, 31));
    var newYearDayTheme = LauncherHolidayCalendar.GetAutomaticState(new DateTime(2027, 1, 1));
    Assert(newYearEveTheme.Theme == LauncherHolidayTheme.Christmas && newYearEveTheme.ShowFireworks &&
        newYearDayTheme.Theme == LauncherHolidayTheme.Christmas && newYearDayTheme.ShowFireworks,
        "El cambio de año debe usar la paleta navideña con fuegos artificiales.");

    Assert(LauncherHolidayCalendar.GetAutomaticState(new DateTime(2026, 10, 31)).Theme == LauncherHolidayTheme.Halloween,
        "Halloween debe activarse del 20 de octubre al 4 de noviembre.");
    Assert(LauncherHolidayCalendar.GetAutomaticState(new DateTime(2026, 2, 14)).Theme == LauncherHolidayTheme.Valentine,
        "San Valentín debe usar la ventana de fechas original.");

    var womensDayTheme = LauncherHolidayCalendar.GetAutomaticState(new DateTime(2026, 3, 8));
    Assert(womensDayTheme.Theme == LauncherHolidayTheme.WomensDay && womensDayTheme.PrimaryHex == "#EE4690",
        "El Día de la Mujer debe usar la misma paleta del launcher Windows.");

    var birthdayTheme = LauncherHolidayCalendar.GetAutomaticState(new DateTime(2026, 3, 27));
    Assert(birthdayTheme.Theme == LauncherHolidayTheme.Birthday && birthdayTheme.ShowBalloons,
        "El aniversario del launcher debe activar los globos.");

    Assert(LauncherHolidayCalendar.GetAutomaticState(new DateTime(2026, 7, 1)).Theme == LauncherHolidayTheme.Default &&
        LauncherHolidayCalendar.GetPreviewState(LauncherHolidayTheme.NewYear).ShowFireworks,
        "Los días normales deben usar el tema predeterminado y la vista previa de Año Nuevo debe mostrar fuegos artificiales.");
    Assert(LauncherHolidayCalendar.GetMonterreyNow().Offset == TimeSpan.FromHours(-6),
        "El calendario debe conservar el offset fijo de Monterrey usado por el launcher original.");

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

    bool emptyDriveIdRejected = false;
    try
    {
        await new GoogleDriveService().DownloadTextFileAsync("  ");
    }
    catch (ArgumentException)
    {
        emptyDriveIdRejected = true;
    }
    Assert(emptyDriveIdRejected,
        "El descargador compartido debe rechazar IDs vacíos antes de realizar solicitudes HTTP.");

    // El actualizador solo debe ofrecer paquetes Linux de la arquitectura del proceso.
    Assert(LauncherReleaseService.GetCompatibleReleaseAssetNames(true, System.Runtime.InteropServices.Architecture.X64)
            .SequenceEqual(new[] { "NegativeLauncher-linux-x64.tar.gz" }) &&
        LauncherReleaseService.GetCompatibleReleaseAssetNames(true, System.Runtime.InteropServices.Architecture.Arm64)
            .SequenceEqual(new[] { "NegativeLauncher-linux-arm64.tar.gz" }),
        "El actualizador debe seleccionar el paquete Linux correcto para x64 y ARM64.");
    Assert(LauncherReleaseService.GetCompatibleReleaseAssetNames(false, System.Runtime.InteropServices.Architecture.X64).Length == 0 &&
        LauncherReleaseService.GetCompatibleReleaseAssetNames(true, System.Runtime.InteropServices.Architecture.Arm).Length == 0,
        "El actualizador no debe ofrecer paquetes Linux en otros sistemas o arquitecturas no publicadas.");
    Version? version010 = LauncherReleaseService.ExtractVersion("v0.1.0");
    Version? version010Numeric = LauncherReleaseService.ExtractVersion("v0.10");
    Assert(LauncherReleaseService.ExtractVersion("Negative Client 0.1.1") == new Version(0, 1, 1) &&
        version010 == new Version(0, 1, 0) &&
        version010Numeric == new Version(0, 10) &&
        version010 != version010Numeric &&
        LauncherReleaseService.ExtractVersion("release sin versión") is null,
        "El actualizador debe extraer versiones numéricas y distinguir 0.1.0 de 0.10.");

    // La ubicación predeterminada debe respetar XDG_DATA_HOME en Linux y compartir raíz con las instancias.
    Directory.CreateDirectory(root);
    if (OperatingSystem.IsLinux())
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string? xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        string expectedLauncherRoot = !string.IsNullOrWhiteSpace(xdgDataHome) && Path.IsPathRooted(xdgDataHome)
            ? Path.Combine(xdgDataHome, "NegativeClient")
            : !string.IsNullOrWhiteSpace(home)
                ? Path.Combine(home, ".local", "share", "NegativeClient")
                : Path.Combine(
                    !string.IsNullOrWhiteSpace(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData))
                        ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                        : Path.GetTempPath(),
                    "NegativeClient");

        Assert(Path.IsPathRooted(LauncherPaths.DefaultLauncherRoot),
            "La ruta predeterminada de datos debe ser absoluta en Linux.");
        Assert(LauncherPathRules.PathsEqual(LauncherPaths.DefaultLauncherRoot, expectedLauncherRoot),
            "Linux debe respetar XDG_DATA_HOME válido o usar ~/.local/share cuando no esté definido.");
        Assert(LauncherPathRules.PathsEqual(InstanceService.LauncherRoot, LauncherPaths.DefaultLauncherRoot),
            "Las instancias y los datos generales deben compartir la raíz predeterminada del launcher.");
    }

    // La búsqueda de Java debe resolver el nombre correcto para cada plataforma y tolerar entradas inválidas.
    string javaSearchDirectory = Path.Combine(root, "java search path");
    Directory.CreateDirectory(javaSearchDirectory);
    string unixJavaCandidate = Path.Combine(javaSearchDirectory, "java");
    string windowsJavaCandidate = Path.Combine(javaSearchDirectory, "java.exe");
    await File.WriteAllTextAsync(unixJavaCandidate, "java-test");
    await File.WriteAllTextAsync(windowsJavaCandidate, "java-test");
    Assert(JavaExecutableHelper.FindOnPath(javaSearchDirectory, isWindows: false) == unixJavaCandidate &&
        JavaExecutableHelper.FindOnPath(javaSearchDirectory, isWindows: true) == windowsJavaCandidate,
        "La búsqueda de Java debe elegir el nombre ejecutable correspondiente al sistema.");
    string relativeJavaSearchDirectory = Path.GetRelativePath(Environment.CurrentDirectory, javaSearchDirectory);
    Assert(JavaExecutableHelper.FindOnPath(relativeJavaSearchDirectory, isWindows: false) ==
            Path.GetFullPath(unixJavaCandidate),
        "La búsqueda de Java debe devolver una ruta absoluta incluso si una entrada de PATH es relativa.");
    string invalidThenValidSearchPath = "directorio\0invalido" + Path.PathSeparator + javaSearchDirectory;
    Assert(JavaExecutableHelper.FindOnPath(invalidThenValidSearchPath, isWindows: false) ==
            Path.GetFullPath(unixJavaCandidate),
        "Una entrada PATH inválida no debe impedir localizar Java en las siguientes carpetas.");


    string quotedSearchPath = Path.Combine(root, "missing java directory") + Path.PathSeparator +
        "\"" + javaSearchDirectory + "\"";
    Assert(JavaExecutableHelper.FindOnPath(quotedSearchPath, isWindows: false) == unixJavaCandidate &&
        JavaExecutableHelper.FindOnPath(null, isWindows: false) is null &&
        JavaExecutableHelper.FindOnPath(string.Empty, isWindows: true) is null,
        "La búsqueda de Java debe ignorar entradas vacías o inexistentes y aceptar entradas entre comillas.");

    Assert(JavaExecutableHelper.ResolveExistingPath("java", javaSearchDirectory) ==
        Path.GetFullPath(unixJavaCandidate),
        "Una ruta relativa de Java debe convertirse en una ruta absoluta antes de cambiar el directorio de trabajo.");
    bool missingCustomJavaRejected = false;
    try
    {
        JavaExecutableHelper.ResolveExistingPath("java-inexistente", javaSearchDirectory);
    }
    catch (FileNotFoundException)
    {
        missingCustomJavaRejected = true;
    }
    Assert(missingCustomJavaRejected,
        "Una ruta personalizada de Java inexistente debe fallar antes de intentar lanzar Minecraft.");
    bool emptyCustomJavaRejected = false;
    try
    {
        JavaExecutableHelper.ResolveExistingPath("  ", javaSearchDirectory);
    }
    catch (ArgumentException)
    {
        emptyCustomJavaRejected = true;
    }
    Assert(emptyCustomJavaRejected,
        "Una ruta vacía de Java personalizado debe rechazarse explícitamente.");

    Assert(MinecraftRuntimeService.ResolveCustomJavaPath(unixJavaCandidate) == Path.GetFullPath(unixJavaCandidate),
        "La configuración de Java personalizado debe centralizar la resolución de rutas absolutas.");
    bool emptyRuntimeJavaRejected = false;
    try
    {
        MinecraftRuntimeService.ResolveCustomJavaPath("  ");
    }
    catch (InvalidOperationException ex)
    {
        emptyRuntimeJavaRejected = ex.Message.Contains("Java personalizado", StringComparison.OrdinalIgnoreCase);
    }
    Assert(emptyRuntimeJavaRejected,
        "El runtime debe explicar cómo corregir una ruta de Java personalizado vacía antes de descargar el juego.");
    bool missingRuntimeJavaRejected = false;
    try
    {
        MinecraftRuntimeService.ResolveCustomJavaPath(Path.Combine(javaSearchDirectory, "java-inexistente"));
    }
    catch (FileNotFoundException)
    {
        missingRuntimeJavaRejected = true;
    }
    Assert(missingRuntimeJavaRejected,
        "El runtime debe rechazar una ruta de Java personalizado inexistente antes de iniciar descargas.");

    // En Unix, el cierre normal debe enviar SIGTERM en lugar de intentar cerrar una ventana inexistente.
    if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
    {
        using var terminationProbe = Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/sleep",
            UseShellExecute = false,
            ArgumentList = { "30" }
        }) ?? throw new InvalidOperationException("No se pudo iniciar el proceso de prueba de cierre.");
        Assert(ProcessTerminationHelper.TryRequestGracefulExit(terminationProbe),
            "El helper debe solicitar el cierre normal del proceso en Unix.");
        await terminationProbe.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert(terminationProbe.HasExited,
            "El proceso Unix debe terminar tras recibir la señal de cierre normal.");
        Assert(ProcessTerminationHelper.TryRequestGracefulExit(terminationProbe),
            "Solicitar el cierre de un proceso que ya terminó debe ser idempotente.");
    }

    // El paquete Linux debe poder reparar el permiso de ejecución de Java sin ampliar permisos ajenos.
    if (!OperatingSystem.IsWindows())
    {
        string javaPermissionProbe = Path.Combine(root, "java-permission-probe");
        await File.WriteAllTextAsync(javaPermissionProbe, "#!/bin/sh\nexit 0\n");
        File.SetUnixFileMode(javaPermissionProbe,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);

        JavaExecutableHelper.EnsureExecutable(javaPermissionProbe);
        UnixFileMode repairedMode = File.GetUnixFileMode(javaPermissionProbe);
        Assert((repairedMode & UnixFileMode.UserExecute) != 0,
            "JavaExecutableHelper debe habilitar ejecución para el propietario en Linux.");
        Assert((repairedMode & UnixFileMode.GroupRead) != 0 &&
            (repairedMode & UnixFileMode.GroupExecute) == 0,
            "JavaExecutableHelper no debe alterar permisos de grupo que no necesita modificar.");
        using (var executableProbe = Process.Start(new ProcessStartInfo
        {
            FileName = javaPermissionProbe,
            UseShellExecute = false
        }) ?? throw new InvalidOperationException("No se pudo ejecutar la prueba de permisos Unix."))
        {
            await executableProbe.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert(executableProbe.ExitCode == 0,
                "Un archivo de Java con permiso de ejecución reparado debe poder iniciarse en Unix.");
        }


        bool missingJavaRejected = false;
        try
        {
            JavaExecutableHelper.EnsureExecutable(Path.Combine(root, "java-inexistente"));
        }
        catch (FileNotFoundException)
        {
            missingJavaRejected = true;
        }
        Assert(missingJavaRejected,
            "JavaExecutableHelper debe informar claramente si la ruta de Java no existe.");

        string javaTargetProbe = Path.Combine(root, "java-target-probe");
        string javaSymlinkProbe = Path.Combine(root, "java-symlink-probe");
        await File.WriteAllTextAsync(javaTargetProbe, "#!/bin/sh\nexit 0\n");
        File.SetUnixFileMode(javaTargetProbe,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
        File.CreateSymbolicLink(javaSymlinkProbe, javaTargetProbe);
        JavaExecutableHelper.EnsureExecutable(javaSymlinkProbe);
        UnixFileMode targetModeAfterSymlink = File.GetUnixFileMode(javaTargetProbe);
        Assert((targetModeAfterSymlink & UnixFileMode.UserExecute) == 0,
            "JavaExecutableHelper no debe cambiar permisos del destino real al recibir un enlace simbólico.");
    }

    // La extracción de modpacks debe rechazar rutas que intenten salir de la carpeta destino.
    string safeZipPath = Path.Combine(root, "modpack-seguro.zip");
    using (var zip = ZipFile.Open(safeZipPath, ZipArchiveMode.Create))
    {
        var entry = zip.CreateEntry("mods/prueba.txt");
        using var writer = new StreamWriter(entry.Open());
        writer.Write("contenido de prueba");
    }

    ModpackArchiveSafety.ValidateZip(safeZipPath);
    string extractionRoot = Path.Combine(root, "extraido-seguro");
    ModpackArchiveSafety.ExtractZipSafely(safeZipPath, extractionRoot, CancellationToken.None);
    Assert(File.ReadAllText(Path.Combine(extractionRoot, "mods", "prueba.txt")) == "contenido de prueba",
        "La extracción segura debe conservar archivos normales dentro del destino.");

    string traversalZipPath = Path.Combine(root, "modpack-ruta-insegura.zip");
    using (var zip = ZipFile.Open(traversalZipPath, ZipArchiveMode.Create))
    {
        var entry = zip.CreateEntry("../archivo-fuera.txt");
        using var writer = new StreamWriter(entry.Open());
        writer.Write("no debe extraerse");
    }

    bool traversalRejected = false;
    try
    {
        ModpackArchiveSafety.ExtractZipSafely(
            traversalZipPath,
            Path.Combine(root, "extraido-inseguro"),
            CancellationToken.None);
    }
    catch (InvalidOperationException)
    {
        traversalRejected = true;
    }
    Assert(traversalRejected && !File.Exists(Path.Combine(root, "archivo-fuera.txt")),
        "La extracción debe rechazar rutas ZIP que salgan del directorio de destino.");

    // Algunos ZIP de herramientas Windows guardan las rutas con '\\' como separador.
    string windowsTraversalZipPath = Path.Combine(root, "modpack-ruta-windows-insegura.zip");
    using (var zip = ZipFile.Open(windowsTraversalZipPath, ZipArchiveMode.Create))
    {
        var entry = zip.CreateEntry(@"..\archivo-fuera-windows.txt");
        using var writer = new StreamWriter(entry.Open());
        writer.Write("no debe extraerse");
    }

    bool windowsTraversalRejected = false;
    try
    {
        ModpackArchiveSafety.ExtractZipSafely(
            windowsTraversalZipPath,
            Path.Combine(root, "extraido-windows-inseguro"),
            CancellationToken.None);
    }
    catch (InvalidOperationException)
    {
        windowsTraversalRejected = true;
    }
    Assert(windowsTraversalRejected &&
        !File.Exists(Path.Combine(root, "archivo-fuera-windows.txt")),
        "La extracción debe bloquear traversal con separadores de Windows en todos los sistemas.");

    if (!OperatingSystem.IsWindows())
    {
        // Una ruta puede parecer interna y aun así escapar si un directorio es un enlace.
        string symlinkZipPath = Path.Combine(root, "modpack-enlace-inseguro.zip");
        using (var zip = ZipFile.Open(symlinkZipPath, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("enlace/salida.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("no debe salir del destino");
        }

        string symlinkExtractionRoot = Path.Combine(root, "extraido-con-enlace");
        string externalExtractionRoot = Path.Combine(root, "destino-externo");
        Directory.CreateDirectory(symlinkExtractionRoot);
        Directory.CreateDirectory(externalExtractionRoot);
        Directory.CreateSymbolicLink(
            Path.Combine(symlinkExtractionRoot, "enlace"),
            externalExtractionRoot);

        bool symlinkEscapeRejected = false;
        try
        {
            ModpackArchiveSafety.ExtractZipSafely(
                symlinkZipPath,
                symlinkExtractionRoot,
                CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            symlinkEscapeRejected = true;
        }

        Assert(symlinkEscapeRejected &&
            !File.Exists(Path.Combine(externalExtractionRoot, "salida.txt")),
            "La extracción debe rechazar directorios enlazados que redirijan archivos fuera del destino.");
    }

    if (!OperatingSystem.IsWindows())
    {
        // Un archivo de destino enlazado tampoco debe permitir sobrescribir fuera del modpack.
        string symlinkFileZipPath = Path.Combine(root, "modpack-archivo-enlazado.zip");
        using (var zip = ZipFile.Open(symlinkFileZipPath, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("mods/config.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("contenido malicioso");
        }

        string symlinkFileExtractionRoot = Path.Combine(root, "extraido-con-archivo-enlazado");
        string externalFile = Path.Combine(root, "config-externo.txt");
        Directory.CreateDirectory(Path.Combine(symlinkFileExtractionRoot, "mods"));
        await File.WriteAllTextAsync(externalFile, "contenido original");
        File.CreateSymbolicLink(
            Path.Combine(symlinkFileExtractionRoot, "mods", "config.txt"),
            externalFile);

        bool symlinkFileEscapeRejected = false;
        try
        {
            ModpackArchiveSafety.ExtractZipSafely(
                symlinkFileZipPath,
                symlinkFileExtractionRoot,
                CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            symlinkFileEscapeRejected = true;
        }

        Assert(symlinkFileEscapeRejected &&
            File.ReadAllText(externalFile) == "contenido original",
            "La extracción debe rechazar un archivo de destino enlazado y preservar el archivo externo.");
    }

    if (!OperatingSystem.IsWindows())
    {
        // La propia raíz de extracción tampoco puede ser un enlace a otra carpeta.
        string rootLinkZipPath = Path.Combine(root, "modpack-raiz-enlazada.zip");
        using (var zip = ZipFile.Open(rootLinkZipPath, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("mods/no-escribir.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("no debe escribirse");
        }

        string realRootTarget = Path.Combine(root, "raiz-real-externa");
        string linkedExtractionRoot = Path.Combine(root, "raiz-de-extraccion-enlazada");
        Directory.CreateDirectory(realRootTarget);
        Directory.CreateSymbolicLink(linkedExtractionRoot, realRootTarget);

        bool linkedRootRejected = false;
        try
        {
            ModpackArchiveSafety.ExtractZipSafely(
                rootLinkZipPath,
                linkedExtractionRoot,
                CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            linkedRootRejected = true;
        }

        Assert(linkedRootRejected &&
            !File.Exists(Path.Combine(realRootTarget, "mods", "no-escribir.txt")),
            "La extracción debe rechazar una raíz enlazada antes de escribir fuera del destino.");
    }

    bool relativeTraversalRejected = false;
    try
    {
        ModpackArchiveSafety.CombineRelativePath(root, "../fuera.txt");
    }
    catch (InvalidOperationException)
    {
        relativeTraversalRejected = true;
    }
    Assert(relativeTraversalRejected,
        "La resolución de rutas relativas debe bloquear intentos de salir de la raíz.");
    Assert(ModpackArchiveSafety.NormalizeRelativePath("/mods/prueba.jar") == "mods/prueba.jar",
        "La normalización debe quitar separadores iniciales y conservar rutas relativas.");

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

    // Mover archivos debe terminar atómicamente y nunca sobrescribir un destino existente.
    string fileMoveRoot = Path.Combine(root, "prueba-movimiento-archivos");
    string fileMoveSource = Path.Combine(fileMoveRoot, "origen", "archivo.txt");
    string fileMoveTarget = Path.Combine(fileMoveRoot, "destino", "archivo.txt");
    Directory.CreateDirectory(Path.GetDirectoryName(fileMoveSource)!);
    await File.WriteAllTextAsync(fileMoveSource, "contenido original");
    FileMigrationHelper.MoveFile(fileMoveSource, fileMoveTarget);
    Assert(!File.Exists(fileMoveSource) &&
        await File.ReadAllTextAsync(fileMoveTarget) == "contenido original",
        "Mover un archivo debe conservar su contenido y retirar el origen solo tras completar el movimiento.");

    string occupiedFileSource = Path.Combine(fileMoveRoot, "origen", "destino-ocupado.txt");
    string occupiedFileTarget = Path.Combine(fileMoveRoot, "destino", "destino-ocupado.txt");
    await File.WriteAllTextAsync(occupiedFileSource, "origen intacto");
    await File.WriteAllTextAsync(occupiedFileTarget, "destino existente");
    bool occupiedFileRejected = false;
    try
    {
        FileMigrationHelper.MoveFile(occupiedFileSource, occupiedFileTarget);
    }
    catch (IOException)
    {
        occupiedFileRejected = true;
    }
    Assert(occupiedFileRejected &&
        await File.ReadAllTextAsync(occupiedFileSource) == "origen intacto" &&
        await File.ReadAllTextAsync(occupiedFileTarget) == "destino existente",
        "Una migración de archivo no debe sobrescribir el destino ni borrar el origen si ya existe.");

    if (!OperatingSystem.IsWindows())
    {
        string executableFileSource = Path.Combine(fileMoveRoot, "origen", "java-ejecutable");
        string executableFileTarget = Path.Combine(fileMoveRoot, "destino", "java-ejecutable");
        await File.WriteAllTextAsync(executableFileSource, "runtime de prueba");
        UnixFileMode expectedMode = UnixFileMode.UserRead | UnixFileMode.UserWrite |
            UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
        File.SetUnixFileMode(executableFileSource, expectedMode);
        FileMigrationHelper.MoveFile(executableFileSource, executableFileTarget);
        Assert(!File.Exists(executableFileSource) &&
            File.GetUnixFileMode(executableFileTarget) == expectedMode,
            "Mover un runtime Unix debe conservar sus permisos de ejecución.");

        string protectedTarget = Path.Combine(fileMoveRoot, "origen", "enlace-destino.txt");
        string sourceLink = Path.Combine(fileMoveRoot, "origen", "enlace-origen.txt");
        await File.WriteAllTextAsync(protectedTarget, "destino real");
        File.CreateSymbolicLink(sourceLink, protectedTarget);
        bool symbolicFileRejected = false;
        try
        {
            FileMigrationHelper.MoveFile(
                sourceLink,
                Path.Combine(fileMoveRoot, "destino", "enlace-origen.txt"));
        }
        catch (IOException)
        {
            symbolicFileRejected = true;
        }

        Assert(symbolicFileRejected &&
            File.Exists(sourceLink) &&
            await File.ReadAllTextAsync(protectedTarget) == "destino real" &&
            !File.Exists(Path.Combine(fileMoveRoot, "destino", "enlace-origen.txt")),
            "La migración de archivos debe rechazar enlaces simbólicos y no modificar su destino real.");
    }

    // Una copia de directorio conserva la carpeta original hasta que el destino queda completo.
    string directoryCopyRoot = Path.Combine(root, "prueba-copia-directorios");
    string directoryCopySource = Path.Combine(directoryCopyRoot, "origen");
    string directoryCopyDestination = Path.Combine(directoryCopyRoot, "destino");
    Directory.CreateDirectory(Path.Combine(directoryCopySource, "subcarpeta"));
    await File.WriteAllTextAsync(Path.Combine(directoryCopySource, "raiz.txt"), "raíz");
    await File.WriteAllTextAsync(Path.Combine(directoryCopySource, "subcarpeta", "anidado.txt"), "anidado");
    string javaRuntimeSource = Path.Combine(directoryCopySource, "runtime-java-prueba");
    string javaRuntimeDestination = Path.Combine(directoryCopyDestination, "runtime-java-prueba");
    await File.WriteAllTextAsync(javaRuntimeSource, "runtime");
    if (!OperatingSystem.IsWindows())
    {
        File.SetUnixFileMode(javaRuntimeSource,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    }

    DirectoryMigrationHelper.CopyDirectoryContents(directoryCopySource, directoryCopyDestination);
    Assert(File.Exists(Path.Combine(directoryCopySource, "raiz.txt")) &&
        await File.ReadAllTextAsync(Path.Combine(directoryCopyDestination, "raiz.txt")) == "raíz" &&
        await File.ReadAllTextAsync(Path.Combine(directoryCopyDestination, "subcarpeta", "anidado.txt")) == "anidado",
        "La copia de carpetas debe preservar archivos raíz y anidados sin eliminar el origen.");

    Assert(await File.ReadAllTextAsync(javaRuntimeDestination) == "runtime",
        "La copia debe conservar los archivos del runtime.");
    if (!OperatingSystem.IsWindows())
    {
        UnixFileMode originalJavaMode = File.GetUnixFileMode(javaRuntimeSource);
        UnixFileMode copiedJavaMode = File.GetUnixFileMode(javaRuntimeDestination);
        Assert((originalJavaMode & UnixFileMode.UserExecute) == (copiedJavaMode & UnixFileMode.UserExecute) &&
            (originalJavaMode & UnixFileMode.GroupExecute) == (copiedJavaMode & UnixFileMode.GroupExecute) &&
            (originalJavaMode & UnixFileMode.OtherExecute) == (copiedJavaMode & UnixFileMode.OtherExecute),
            "Una migración por copia debe conservar los permisos de ejecución de Java en Linux y macOS.");
    }

    bool overlappingDirectoryCopyRejected = false;
    try
    {
        DirectoryMigrationHelper.CopyDirectoryContents(
            directoryCopySource, Path.Combine(directoryCopySource, "destino-anidado"));
    }
    catch (InvalidOperationException)
    {
        overlappingDirectoryCopyRejected = true;
    }
    Assert(overlappingDirectoryCopyRejected,
        "La copia de carpetas debe rechazar destinos dentro del propio origen.");

    if (!OperatingSystem.IsWindows())
    {
        // Una migración no debe recorrer un enlace que apunta fuera del árbol de origen.
        string symlinkRoot = Path.Combine(root, "prueba-enlaces-migracion");
        string symlinkSource = Path.Combine(symlinkRoot, "origen");
        string symlinkDestination = Path.Combine(symlinkRoot, "destino");
        string externalDirectory = Path.Combine(symlinkRoot, "datos-externos");
        Directory.CreateDirectory(symlinkSource);
        Directory.CreateDirectory(externalDirectory);
        string protectedFile = Path.Combine(externalDirectory, "no-copiar.txt");
        await File.WriteAllTextAsync(protectedFile, "datos externos");
        Directory.CreateSymbolicLink(
            Path.Combine(symlinkSource, "enlace-externo"),
            externalDirectory);

        bool symbolicDirectoryRejected = false;
        try
        {
            DirectoryMigrationHelper.CopyDirectoryContents(symlinkSource, symlinkDestination);
        }
        catch (IOException)
        {
            symbolicDirectoryRejected = true;
        }

        Assert(symbolicDirectoryRejected &&
            !Directory.Exists(symlinkDestination) &&
            await File.ReadAllTextAsync(protectedFile) == "datos externos",
            "La migración debe rechazar enlaces simbólicos antes de crear el destino o copiar datos externos.");

        // Tampoco debe escribir por medio de un enlace ya presente en el destino.
        string guardedRoot = Path.Combine(symlinkRoot, "destino-administrado");
        Directory.CreateDirectory(guardedRoot);
        Directory.CreateSymbolicLink(
            Path.Combine(guardedRoot, "enlace"),
            externalDirectory);

        bool symbolicDestinationRejected = false;
        try
        {
            DirectoryMigrationHelper.EnsureNoReparsePointsUnderRoot(
                guardedRoot,
                Path.Combine(guardedRoot, "enlace", "nuevo.txt"));
        }
        catch (IOException)
        {
            symbolicDestinationRejected = true;
        }

        Assert(symbolicDestinationRejected &&
            !File.Exists(Path.Combine(externalDirectory, "nuevo.txt")),
            "La migración debe rechazar destinos que atraviesan enlaces simbólicos.");
    }

    // El archivo de capturas debe mover imágenes, conservar sus metadatos y limpiarse al quedar vacío.
    string screenshotRoot = Path.Combine(root, "prueba-archivo-capturas");
    string originalScreenshots = Path.Combine(screenshotRoot, "instancia", "screenshots");
    Directory.CreateDirectory(originalScreenshots);
    string originalImage = Path.Combine(originalScreenshots, "captura.png");
    await File.WriteAllBytesAsync(originalImage, new byte[] { 1, 2, 3 });
    string archiveDirectoryRoot = Path.Combine(screenshotRoot, "archivadas");
    var screenshotStore = new ScreenshotArchiveStore(archiveDirectoryRoot);
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

    string screenshotsForDeletedInstance = Path.Combine(screenshotRoot, "instancia-borrada", "screenshots");
    Directory.CreateDirectory(screenshotsForDeletedInstance);
    await File.WriteAllBytesAsync(Path.Combine(screenshotsForDeletedInstance, "conservar-archivo.png"), new byte[] { 2, 4, 6 });
    string removedInstanceArchive = screenshotStore.ArchiveInstanceScreenshots(
        "instancia-borrada", "Instancia borrada", screenshotsForDeletedInstance);
    screenshotStore.DeleteArchivesForInstance("instancia-borrada");
    Assert(screenshotStore.GetArchives().All(x => x.InstanceId != "instancia-borrada") &&
        !Directory.Exists(Path.GetDirectoryName(removedInstanceArchive)),
        "Eliminar una instalación también debe limpiar sus capturas archivadas.");

    // Metadatos falsificados no deben convertir la limpieza de capturas en borrado arbitrario.
    string externalKeepDirectory = Path.Combine(root, "capturas-fuera-del-archivo", "screenshots");
    Directory.CreateDirectory(externalKeepDirectory);
    string externalKeepFile = Path.Combine(externalKeepDirectory, "no-borrar.png");
    await File.WriteAllBytesAsync(externalKeepFile, new byte[] { 8, 8, 8 });
    string forgedArchiveDirectory = Path.Combine(archiveDirectoryRoot, "registro-falsificado");
    Directory.CreateDirectory(forgedArchiveDirectory);
    await File.WriteAllTextAsync(
        Path.Combine(forgedArchiveDirectory, "archive-info.json"),
        JsonSerializer.Serialize(new ScreenshotArchiveInfo
        {
            InstanceId = "id-falsificado",
            InstanceName = "No confiable",
            ScreenshotsDirectory = externalKeepDirectory
        }));
    screenshotStore.DeleteArchivesForInstance("id-falsificado");
    Assert(File.Exists(externalKeepFile) && Directory.Exists(forgedArchiveDirectory) &&
        !screenshotStore.GetArchives().Any(x => x.InstanceId == "id-falsificado"),
        "Los metadatos que apuntan fuera de la raíz deben ignorarse y no deben permitir borrar ni enumerar capturas externas.");

    string siblingArchiveRoot = archiveDirectoryRoot + "-externo";
    string siblingScreenshots = Path.Combine(siblingArchiveRoot, "instancia", "screenshots");
    Directory.CreateDirectory(siblingScreenshots);
    string siblingImage = Path.Combine(siblingScreenshots, "no-borrar.png");
    await File.WriteAllBytesAsync(siblingImage, new byte[] { 1, 2, 3 });
    screenshotStore.CleanupArchiveIfEmpty(siblingImage);
    Assert(File.Exists(siblingImage),
        "La limpieza de capturas no debe borrar archivos de una carpeta hermana cuyo nombre comparte el prefijo del archivo.");

    bool invalidArchiveIdRejected = false;
    try
    {
        screenshotStore.ArchiveInstanceScreenshots("../fuera", "ID no válido", originalScreenshots);
    }
    catch (InvalidOperationException)
    {
        invalidArchiveIdRejected = true;
    }
    Assert(invalidArchiveIdRejected,
        "El archivador debe rechazar IDs que puedan escapar de su carpeta raíz.");

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
    Assert(OfflineAccountService.IsValidUsername("JugadorPrueba") &&
        OfflineAccountService.IsValidUsername("abc_123") &&
        OfflineAccountService.IsValidUsername("Jugádor") &&
        OfflineAccountService.IsValidUsername("玩家123") &&
        !OfflineAccountService.IsValidUsername("ab") &&
        !OfflineAccountService.IsValidUsername("Jugador Con Espacios") &&
        !OfflineAccountService.IsValidUsername("nombre-con-guion"),
        "Los perfiles sin conexión deben usar nombres de 3 a 16 caracteres: letras, números o guion bajo.");

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

    if (OperatingSystem.IsLinux())
    {
        string caseVariantSkin = Path.Combine(offlineAccountRoot, "accounts", "offline-skin.PNG");
        await File.WriteAllBytesAsync(caseVariantSkin, new byte[] { 9, 8, 7, 6 });
        await offlineAccounts.SaveAsync("JugadorPrueba", caseVariantSkin, "wide");
        Assert((await File.ReadAllBytesAsync(Path.Combine(offlineAccountRoot, "accounts", "offline-skin.png")))
                .SequenceEqual(new byte[] { 9, 8, 7, 6 }),
            "En Linux, una ruta de skin que solo difiera en mayúsculas debe copiarse como un archivo distinto.");
    }

    bool invalidOfflineNameRejected = false;
    try
    {
        await offlineAccounts.SaveAsync("Jugador Con Espacios", null, "wide", removeSkin: true);
    }
    catch (ArgumentException)
    {
        invalidOfflineNameRejected = true;
    }
    Assert(invalidOfflineNameRejected &&
        File.Exists(Path.Combine(offlineAccountRoot, "accounts", "offline-skin.png")) &&
        (await offlineAccounts.LoadAsync())?.Username == "JugadorPrueba",
        "Rechazar un nombre inválido no debe modificar el perfil guardado ni eliminar su skin.");

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
    string skinPathLower = Path.Combine(root, "skin.png");
    string skinPathUpper = Path.Combine(root, "SKIN.PNG");
    microsoftInfo.SkinHeadPath = skinPathLower;
    Assert(skinPathNotified && microsoftInfo.SkinHeadPath == skinPathLower,
        "Cambiar la ruta de la cabeza debe notificar a la interfaz.");
    skinPathNotified = false;
    microsoftInfo.SkinHeadPath = skinPathUpper;
    bool skinVolumeIgnoresCase =
        LauncherPathRules.GetPathComparison(root) == StringComparison.OrdinalIgnoreCase;
    Assert(microsoftInfo.SkinHeadPath == (skinVolumeIgnoresCase ? skinPathLower : skinPathUpper),
        "La ruta de skin debe seguir la sensibilidad a mayúsculas del volumen real.");
    Assert(skinPathNotified == !skinVolumeIgnoresCase,
        "Cambiar solo la capitalización debe notificar únicamente en un volumen sensible a mayúsculas.");
    Assert(Path.IsPathRooted(LauncherPaths.DefaultLauncherRoot),
        "La carpeta de datos predeterminada debe ser una ruta absoluta.");
    Assert(Path.GetFileName(LauncherPaths.DefaultLauncherRoot) == "NegativeClient",
        "La carpeta predeterminada debe conservar el nombre NegativeClient.");

    // La política de rutas se prueba para los tres sistemas sin depender del SO del runner.
    string simulatedHome = Path.Combine(root, "home-de-prueba");
    string simulatedAppData = Path.Combine(root, "datos-roaming-de-prueba");
    string simulatedXdgData = Path.Combine(root, "xdg-data-de-prueba");
    Assert(LauncherPaths.ResolveDefaultLauncherRoot(
            true, false, simulatedHome, simulatedAppData, simulatedXdgData) ==
        Path.Combine(simulatedAppData, "NegativeClient"),
        "Windows debe conservar la ubicación equivalente a ApplicationData.");
    Assert(LauncherPaths.ResolveDefaultLauncherRoot(
            false, true, simulatedHome, simulatedAppData, simulatedXdgData) ==
        Path.Combine(simulatedHome, "Library", "Application Support", "NegativeClient"),
        "macOS debe utilizar Library/Application Support.");
    Assert(LauncherPaths.ResolveDefaultLauncherRoot(
            false, false, simulatedHome, simulatedAppData, simulatedXdgData) ==
        Path.Combine(simulatedXdgData, "NegativeClient"),
        "Linux debe preferir un XDG_DATA_HOME absoluto.");
    Assert(LauncherPaths.ResolveDefaultLauncherRoot(
            false, false, simulatedHome, simulatedAppData, "ruta-relativa") ==
        Path.Combine(simulatedHome, ".local", "share", "NegativeClient"),
        "Linux debe ignorar un XDG_DATA_HOME relativo y usar ~/.local/share.");
    Assert(LauncherPaths.ResolveDefaultLauncherRoot(
            false, false, null, simulatedAppData, null) ==
        Path.Combine(simulatedAppData, "NegativeClient"),
        "Un entorno sin HOME ni XDG debe recurrir a ApplicationData cuando exista.");
    Assert(LauncherPaths.ResolveDefaultLauncherRoot(
            true, false, null, null, null) ==
        Path.Combine(Path.GetTempPath(), "NegativeClient"),
        "La selección de ruta debe seguir siendo absoluta en un entorno Windows restringido.");

    // Validar el soporte del loader antes de descargar o crear archivos.
    Assert(MinecraftRuntimeService.ValidateSupportedLoader(null, null) == "vanilla" &&
        MinecraftRuntimeService.ValidateSupportedLoader("  ", null) == "vanilla" &&
        MinecraftRuntimeService.ValidateSupportedLoader("Vanilla", null) == "vanilla",
        "Una instancia sin loader explícito debe tratarse como Vanilla.");
    Assert(MinecraftRuntimeService.ValidateSupportedLoader("Forge", "47.4.0") == "forge",
        "Forge debe aceptarse antes de iniciar la preparación.");
    Assert(MinecraftRuntimeService.ValidateSupportedLoader("NeoForge", "21.1.172") == "neoforge" &&
        MinecraftRuntimeService.ValidateSupportedLoader("Neo-Forge", "21.1.172") == "neoforge",
        "NeoForge debe aceptar los nombres habituales del loader.");
    Assert(MinecraftRuntimeService.ValidateSupportedLoader("Fabric", "0.16.0") == "fabric" &&
        MinecraftRuntimeService.ValidateSupportedLoader("FabricMC", "0.16.0") == "fabric",
        "Fabric y FabricMC deben resolver al mismo instalador.");
    Assert(MinecraftRuntimeService.ValidateSupportedLoader("Quilt", "0.27.1") == "quilt" &&
        MinecraftRuntimeService.ValidateSupportedLoader("QuiltMC", "0.27.1") == "quilt",
        "Quilt y QuiltMC deben resolver al mismo instalador.");
    Assert(MinecraftRuntimeService.ValidateSupportedLoader("Fabric", null) == "fabric" &&
        MinecraftRuntimeService.ValidateSupportedLoader("Quilt", null) == "quilt" &&
        MinecraftRuntimeService.ValidateSupportedLoader("NeoForge", null) == "neoforge",
        "Fabric, Quilt y NeoForge deben permitir resolver el loader estable cuando el catálogo omita la versión.");
    Assert(CmlLib.Core.ModLoaders.FabricMC.FabricInstaller.GetVersionName("1.20.4", "0.16.0") ==
            "fabric-loader-0.16.0-1.20.4" &&
        CmlLib.Core.ModLoaders.QuiltMC.QuiltInstaller.GetVersionName("1.20.4", "0.27.1") ==
            "quilt-loader-0.27.1-1.20.4",
        "Los perfiles de Fabric y Quilt deben mantener sus nombres de versión reconocidos por CmlLib.");
    Assert(
        MinecraftRuntimeService.NormalizeLoaderVersion("fabric-loader-0.16.0-1.20.4", "fabric", "1.20.4") == "0.16.0" &&
        MinecraftRuntimeService.NormalizeLoaderVersion("1.20.4-0.16.0", "fabric", "1.20.4") == "0.16.0" &&
        MinecraftRuntimeService.NormalizeLoaderVersion("quilt-loader-0.27.1-1.20.4", "quilt", "1.20.4") == "0.27.1" &&
        MinecraftRuntimeService.NormalizeLoaderVersion("1.20.1-forge-47.4.0", "forge", "1.20.1") == "47.4.0" &&
        MinecraftRuntimeService.NormalizeLoaderVersion("neoforge-21.1.172-1.21.1", "neoforge", "1.21.1") == "21.1.172" &&
        MinecraftRuntimeService.NormalizeLoaderVersion("1.21.1-21.1.172", "neoforge", "1.21.1") == "21.1.172" &&
        MinecraftRuntimeService.NormalizeLoaderVersion("FABRIC-LOADER-0.16.0-1.20.4", "fabric", "1.20.4") == "0.16.0" &&
        MinecraftRuntimeService.NormalizeLoaderVersion("1.20.4-fabric-loader-0.16.0-1.20.4", "fabric", "1.20.4") == "0.16.0" &&
        MinecraftRuntimeService.NormalizeLoaderVersion("quilt-0.27.1-1.20.4", "quilt", "1.20.4") == "0.27.1" &&
        MinecraftRuntimeService.NormalizeLoaderVersion("47.4.0-1.20.1", "forge", "1.20.1") == "47.4.0",
        "El runtime debe normalizar nombres de perfil completos, prefijos repetidos y diferencias de mayúsculas.");

    bool missingForgeVersionRejected = false;
    try
    {
        MinecraftRuntimeService.ValidateSupportedLoader("Forge", " ");
    }
    catch (InvalidOperationException)
    {
        missingForgeVersionRejected = true;
    }
    Assert(missingForgeVersionRejected,
        "Forge sin versión debe rechazarse antes de iniciar descargas.");

    Assert(MinecraftRuntimeService.ValidateForgeVersion("1.20.1-forge-47.4.0", "1.20.1") == "47.4.0",
        "La validación temprana de Forge debe aceptar una versión de perfil completa.");
    Assert(MinecraftRuntimeService.NormalizeConfiguredLoaderVersion(
            "1.20.4-fabric-loader-0.16.0-1.20.4", "fabric", "1.20.4") == "0.16.0" &&
        MinecraftRuntimeService.NormalizeConfiguredLoaderVersion(
            "neoforge-21.1.172-1.21.1", "neoforge", "1.21.1") == "21.1.172",
        "La validación previa debe conservar las versiones explícitas de Fabric y NeoForge.");
    bool malformedFabricVersionRejected = false;
    try
    {
        MinecraftRuntimeService.NormalizeConfiguredLoaderVersion("1.20.4", "fabric", "1.20.4");
    }
    catch (InvalidOperationException)
    {
        malformedFabricVersionRejected = true;
    }
    Assert(malformedFabricVersionRejected,
        "Un perfil Fabric que no contiene versión del loader debe rechazarse antes de descargar Minecraft.");

    bool malformedForgeVersionRejected = false;
    try
    {
        MinecraftRuntimeService.ValidateForgeVersion("1.20.1-forge-", "1.20.1");
    }
    catch (InvalidOperationException)
    {
        malformedForgeVersionRejected = true;
    }
    Assert(malformedForgeVersionRejected,
        "Una versión de Forge vacía tras normalizarse debe rechazarse antes de descargar Minecraft.");


    foreach (string unsupportedLoader in new[] { "FabricRebuild", "UnknownLoader" })
    {
        bool unsupportedLoaderRejected = false;
        try
        {
            MinecraftRuntimeService.ValidateSupportedLoader(unsupportedLoader, "prueba");
        }
        catch (NotSupportedException ex)
        {
            unsupportedLoaderRejected =
                ex.Message.Contains(unsupportedLoader, StringComparison.OrdinalIgnoreCase);
        }

        Assert(unsupportedLoaderRejected,
            $"El loader {unsupportedLoader} debe rechazarse con un mensaje claro antes de iniciar descargas.");
    }

    string javaExecutableProbe = Path.Combine(root, "java-ejecutable-prueba");
    await File.WriteAllTextAsync(javaExecutableProbe, "java de prueba");
    JavaExecutableHelper.EnsureExecutable(javaExecutableProbe);
    Assert(File.Exists(javaExecutableProbe),
        "La comprobación de Java no debe alterar archivos de ninguna plataforma.");
    if (!OperatingSystem.IsWindows())
    {
        File.SetUnixFileMode(javaExecutableProbe, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        JavaExecutableHelper.EnsureExecutable(javaExecutableProbe);
        UnixFileMode executableMode = File.GetUnixFileMode(javaExecutableProbe);
        Assert((executableMode & UnixFileMode.UserExecute) != 0 &&
            (executableMode & UnixFileMode.GroupExecute) == 0 &&
            (executableMode & UnixFileMode.OtherExecute) == 0,
            "Habilitar Java debe permitir ejecutarlo sin ampliar los permisos de grupo y otros.");

        string javaSymlinkTarget = Path.Combine(root, "java-enlace-destino-prueba");
        string javaSymlink = Path.Combine(root, "java-enlace-prueba");
        await File.WriteAllTextAsync(javaSymlinkTarget, "java de prueba enlazado");
        UnixFileMode protectedMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        File.SetUnixFileMode(javaSymlinkTarget, protectedMode);
        File.CreateSymbolicLink(javaSymlink, javaSymlinkTarget);
        JavaExecutableHelper.EnsureExecutable(javaSymlink);
        Assert(File.GetUnixFileMode(javaSymlinkTarget) == protectedMode &&
            File.Exists(javaSymlink),
            "La comprobación de Java no debe alterar los permisos del destino de un enlace simbólico.");
    }

    // Detectar la semántica real del volumen, incluyendo APFS sensible y no sensible a mayúsculas.
    string filesystemCaseProbeDirectory = Path.Combine(root, "prueba-sensibilidad-mayusculas");
    Directory.CreateDirectory(filesystemCaseProbeDirectory);
    string filesystemCaseProbe = Path.Combine(filesystemCaseProbeDirectory, "NegativeCaseProbe.txt");
    string filesystemCaseVariant = Path.Combine(filesystemCaseProbeDirectory, "negativecaseprobe.TXT");
    await File.WriteAllTextAsync(filesystemCaseProbe, "case");
    bool filesystemIsCaseInsensitive = File.Exists(filesystemCaseVariant);
    StringComparison expectedFilesystemComparison = filesystemIsCaseInsensitive
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    Assert(LauncherPathRules.GetPathComparison(filesystemCaseProbeDirectory) == expectedFilesystemComparison,
        "La comparación de rutas debe reflejar la sensibilidad real a mayúsculas de la ubicación.");
    Assert(LauncherPathRules.GetPathComparerForPath(filesystemCaseProbeDirectory)
            .Equals("mods/Foo.jar", "mods/foo.jar") == filesystemIsCaseInsensitive,
        "El inventario de archivos administrados debe seguir la semántica del volumen.");
    Assert(LauncherPathRules.PathsEqual(filesystemCaseProbe, filesystemCaseVariant) == filesystemIsCaseInsensitive,
        "La igualdad de rutas debe variar solo si el propio volumen trata los nombres como iguales.");

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

    {
        string mixedCasePath = Path.Combine(filesystemCaseProbeDirectory, "Carpeta");
        string lowerCasePath = Path.Combine(filesystemCaseProbeDirectory, "carpeta");
        Assert(LauncherPathRules.PathsEqual(mixedCasePath, lowerCasePath) == filesystemIsCaseInsensitive,
            "La igualdad de rutas debe seguir la configuración real del volumen.");
        Assert(LauncherPathRules.IsSameOrSubPath(lowerCasePath, mixedCasePath) == filesystemIsCaseInsensitive,
            "La validación de subrutas debe seguir la configuración real del volumen.");
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
    Assert(Directory.GetFiles(Path.Combine(instancesRoot, "guardada-01"), "instance.json.*.tmp").Length == 0,
        "El guardado de una instancia debe limpiar el archivo temporal al finalizar.");
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

    string mismatchedDirectory = Path.Combine(instancesRoot, "carpeta-distinta");
    Directory.CreateDirectory(mismatchedDirectory);
    await File.WriteAllTextAsync(
        Path.Combine(mismatchedDirectory, "instance.json"),
        JsonSerializer.Serialize(new InstalledInstance
        {
            Id = "guardada-01",
            Name = "Copia con ID ajeno"
        }));
    List<InstalledInstance> afterMismatchedDirectory = await instanceStore.LoadAllAsync();
    Assert(afterMismatchedDirectory.Count == 1 &&
        afterMismatchedDirectory[0].Id == "guardada-01" &&
        afterMismatchedDirectory[0].Name == "Instancia guardada",
        "Un instance.json con ID distinto al nombre de su carpeta debe ignorarse.");

    string unsafeStoredDirectory = Path.Combine(instancesRoot, "id-inseguro");
    Directory.CreateDirectory(unsafeStoredDirectory);
    await File.WriteAllTextAsync(
        Path.Combine(unsafeStoredDirectory, "instance.json"),
        JsonSerializer.Serialize(new InstalledInstance
        {
            Id = "../fuera",
            Name = "ID inseguro"
        }));
    List<InstalledInstance> afterUnsafeDirectory = await instanceStore.LoadAllAsync();
    Assert(afterUnsafeDirectory.Count == 1 && afterUnsafeDirectory[0].Id == "guardada-01",
        "Un ID inseguro en un archivo almacenado no debe incorporarse a la lista.");

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

    if (!OperatingSystem.IsWindows())
    {
        string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(homeDirectory))
        {
            Assert(LauncherStorageLayout.NormalizeRoot("~", fallbackRoot) ==
                LauncherPathRules.NormalizeFullPath(homeDirectory),
                "En Linux/macOS, una ruta escrita como ~ debe resolver a la carpeta personal.");
            Assert(LauncherStorageLayout.NormalizeRoot("~/Negative Launcher pruebas", fallbackRoot) ==
                LauncherPathRules.NormalizeFullPath(Path.Combine(homeDirectory, "Negative Launcher pruebas")),
                "En Linux/macOS, la abreviatura ~/ debe admitir rutas con espacios.");

            const string variableName = "NEGATIVE_LAUNCHER_TEST_STORAGE_HOME";
            string? previousValue = Environment.GetEnvironmentVariable(variableName);
            try
            {
                Environment.SetEnvironmentVariable(variableName, homeDirectory);
                Assert(LauncherStorageLayout.NormalizeRoot("$NEGATIVE_LAUNCHER_TEST_STORAGE_HOME/Negative Launcher pruebas", fallbackRoot) ==
                    LauncherPathRules.NormalizeFullPath(Path.Combine(homeDirectory, "Negative Launcher pruebas")),
                    "Linux/macOS deben expandir variables de entorno con la sintaxis $VARIABLE.");
                Assert(LauncherStorageLayout.NormalizeRoot("${NEGATIVE_LAUNCHER_TEST_STORAGE_HOME}/Negative Launcher pruebas", fallbackRoot) ==
                    LauncherPathRules.NormalizeFullPath(Path.Combine(homeDirectory, "Negative Launcher pruebas")),
                    "Linux/macOS deben expandir variables de entorno con la sintaxis ${VARIABLE}.");
            }
            finally
            {
                Environment.SetEnvironmentVariable(variableName, previousValue);
            }
        }
    }

    string filesystemRoot = Path.GetPathRoot(Path.GetFullPath(root))!;
    Assert(LauncherStorageLayout.NormalizeRoot(filesystemRoot, fallbackRoot) == filesystemRoot,
        "Normalizar la raíz del sistema no debe convertirla en una ruta relativa.");
    Assert(LauncherPathRules.PathsEqual(filesystemRoot, Path.Combine(filesystemRoot, ".")),
        "La raíz del sistema debe compararse correctamente con una ruta equivalente.");
    Assert(LauncherPathRules.IsSameOrSubPath(filesystemRoot, filesystemRoot),
        "La raíz del sistema debe reconocerse como la misma ruta al validar subrutas.");

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
    Assert(loaded.MicrosoftOAuthClientId == string.Empty,
        "Las preferencias antiguas sin Client ID deben seguir cargándose sin error.");

    // El Client ID público debe persistir para que el acceso Microsoft no requiera introducirlo en cada inicio.
    const string persistedClientId = "aabbccdd-aabb-ccdd-aabb-ccddeeff0011";
    loaded.MicrosoftOAuthClientId = persistedClientId;

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
        Assert(document.RootElement.GetProperty("MicrosoftOAuthClientId").GetString() == persistedClientId,
            "El archivo de preferencias debe guardar el Client ID de Microsoft.");
    }
    LauncherPreferences reloadedOAuthPreferences = await service.LoadAsync();
    Assert(reloadedOAuthPreferences.MicrosoftOAuthClientId == persistedClientId,
        "El Client ID debe conservarse después de cerrar y volver a cargar las preferencias.");

    // Un archivo dañado no debe impedir que abra el launcher.
    await File.WriteAllTextAsync(path, "{ archivo dañado");
    LauncherPreferences recovered = await service.LoadAsync();
    Assert(recovered.MaximumRamMb == 4096, "Un JSON dañado debe devolver valores por defecto.");
    Assert(File.Exists(path), "La recuperación debe volver a crear el archivo activo de preferencias.");
    Assert(Directory.GetFiles(root, "launcher-preferences.json.corrupt-*").Length == 1,
        "La recuperación debe conservar una copia del archivo de preferencias dañado.");
    LauncherPreferences recoveredFromDisk = await service.LoadAsync();
    Assert(recoveredFromDisk.MaximumRamMb == 4096,
        "Una segunda carga debe leer las preferencias predeterminadas restauradas.");
    Assert(Directory.GetFiles(root, "launcher-preferences.json.*.tmp").Length == 0,
        "El guardado de preferencias no debe dejar archivos temporales tras completarse.");

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

    bool rejectedTraversalId = false;
    try
    {
        await stateService.SetLastPlayedInstanceIdAsync("../fuera");
    }
    catch (InvalidOperationException)
    {
        rejectedTraversalId = true;
    }
    Assert(rejectedTraversalId, "El estado no debe guardar IDs que salgan de la carpeta de instancias.");

    await File.WriteAllTextAsync(
        statePath,
        JsonSerializer.Serialize(new { LastPlayedInstanceId = "../fuera" }));
    Assert(await stateService.GetLastPlayedInstanceIdAsync() is null,
        "Un ID inseguro guardado en disco debe ignorarse al recuperar el estado.");

    await File.WriteAllTextAsync(
        statePath,
        JsonSerializer.Serialize(new { LastPlayedInstanceId = "instancia-valida_01" }));
    Assert(await stateService.GetLastPlayedInstanceIdAsync() == "instancia-valida_01",
        "El estado debe seguir aceptando IDs válidos con guion bajo.");

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

    // Pausar antes de la primera fase no debe dejar que el runtime comience descargas sin esperar.
    using (var pauseBeforePhaseController = new DownloadOperationController())
    {
        pauseBeforePhaseController.Pause();
        CancellationToken initialPhaseToken = pauseBeforePhaseController.BeginPhase();
        Assert(initialPhaseToken.IsCancellationRequested,
            "Una fase que comienza mientras la operación está pausada debe quedar cancelada para poder reintentarse después.");
        pauseBeforePhaseController.EndPhase();
        Task initialPauseWait = pauseBeforePhaseController.WaitWhilePausedAsync();
        Assert(!initialPauseWait.IsCompleted,
            "La pausa previa a la primera fase debe mantenerse hasta que el usuario reanude.");
        pauseBeforePhaseController.Resume();
        await initialPauseWait;
        CancellationToken resumedPhaseToken = pauseBeforePhaseController.BeginPhase();
        Assert(!resumedPhaseToken.IsCancellationRequested,
            "La primera fase reintentada después de reanudar debe recibir un token activo.");
        pauseBeforePhaseController.EndPhase();
    }

    // Detener desde el estado pausado debe liberar la espera y cancelar el controlador.
    using (var stopWhilePausedController = new DownloadOperationController())
    {
        stopWhilePausedController.Pause();
        Task stoppedWait = stopWhilePausedController.WaitWhilePausedAsync();
        stopWhilePausedController.Stop();
        bool pauseWaitCanceled = false;
        try
        {
            await stoppedWait;
        }
        catch (OperationCanceledException)
        {
            pauseWaitCanceled = true;
        }

        Assert(pauseWaitCanceled && stopWhilePausedController.IsStopped,
            "Detener una operación pausada debe liberar la espera sin dejarla bloqueada.");
    }

    // Liberar una operación durante una pausa no debe dejar tareas esperando indefinidamente.
    var disposeWhilePausedController = new DownloadOperationController();
    disposeWhilePausedController.Pause();
    Task disposedPauseWait = disposeWhilePausedController.WaitWhilePausedAsync();
    disposeWhilePausedController.Dispose();
    // La liberación debe ser idempotente y también cancelar cualquier fase activa.
    disposeWhilePausedController.Dispose();
    var disposeDuringPhaseController = new DownloadOperationController();
    CancellationToken phaseDisposedToken = disposeDuringPhaseController.BeginPhase();
    disposeDuringPhaseController.Dispose();
    Assert(phaseDisposedToken.IsCancellationRequested,
        "Disponer un controlador con una fase activa debe cancelar su token de fase.");

    bool disposePauseWaitCanceled = false;
    try
    {
        await disposedPauseWait;
    }
    catch (OperationCanceledException)
    {
        disposePauseWaitCanceled = true;
    }

    Assert(disposePauseWaitCanceled,
        "Disponer un controlador pausado debe cancelar y liberar las tareas que esperan reanudación.");

    var disposedController = new DownloadOperationController();
    CancellationToken tokenAfterDispose = disposedController.StopToken;
    disposedController.Dispose();
    disposedController.Stop();
    disposedController.Pause();
    disposedController.Resume();
    disposedController.EndPhase();
    Assert(disposedController.IsStopped && tokenAfterDispose.IsCancellationRequested,
        "El estado y el token de cancelación deben seguir siendo consultables después de Dispose.");
    bool beginPhaseAfterDisposeRejected = false;
    try
    {
        disposedController.BeginPhase();
    }
    catch (ObjectDisposedException)
    {
        beginPhaseAfterDisposeRejected = true;
    }
    Assert(beginPhaseAfterDisposeRejected,
        "No se deben crear fases nuevas después de disponer el controlador.");

    var waitAfterDisposeController = new DownloadOperationController();
    waitAfterDisposeController.Pause();
    waitAfterDisposeController.Dispose();
    bool waitAfterDisposeCanceled = false;
    try
    {
        await waitAfterDisposeController.WaitWhilePausedAsync();
    }
    catch (OperationCanceledException)
    {
        waitAfterDisposeCanceled = true;
    }
    Assert(waitAfterDisposeCanceled,
        "Esperar después de Dispose debe producir cancelación, no acceder a un origen ya liberado.");

    var disposeRaceController = new DownloadOperationController();
    Task concurrentStop = Task.Run(() =>
    {
        for (int i = 0; i < 100; i++)
            disposeRaceController.Stop();
    });
    Task concurrentDispose = Task.Run(() =>
    {
        for (int i = 0; i < 100; i++)
            disposeRaceController.Dispose();
    });
    await Task.WhenAll(concurrentStop, concurrentDispose);
    Assert(disposeRaceController.IsStopped,
        "Stop y Dispose concurrentes deben terminar sin excepción y dejar la operación cancelada.");

    // La instalación de modpacks debe funcionar desde el núcleo con un ZIP ya cacheado, sin red ni WPF.
    string installerStorageRoot = Path.Combine(root, "almacen-instalador");
    var instanceService = new InstanceService();
    instanceService.ConfigureStorageRoot(installerStorageRoot);
    string installerCache = InstanceService.PackageCacheRoot;
    Directory.CreateDirectory(installerCache);

    string firstArchivePath = Path.Combine(installerCache, "overland-1.0.0-archive-file.zip");
    using (var zip = ZipFile.Open(firstArchivePath, ZipArchiveMode.Create))
    {
        using (var writer = new StreamWriter(zip.CreateEntry("mods/ejemplo.txt").Open()))
        {
            writer.Write("mod versión uno");
        }
        using (var optionsWriter = new StreamWriter(zip.CreateEntry("options.txt").Open()))
        {
            optionsWriter.Write("preferencias del usuario");
        }
        using (var obsoleteWriter = new StreamWriter(zip.CreateEntry("mods/obsoleto.txt").Open()))
        {
            obsoleteWriter.Write("archivo antiguo del paquete");
        }
    }

    var installer = new ModpackInstallerService(new GoogleDriveService(), instanceService);

    // Un loader desconocido debe fallar antes de crear temporales, cachear o instalar el paquete.
    const string unsupportedPackId = "pack-loader-no-compatible";
    var unsupportedManifest = new ModpackManifest
    {
        Id = unsupportedPackId,
        Name = "Loader desconocido",
        Version = "1.0.0",
        MinecraftVersion = "1.20.1",
        Loader = "LoaderDesconocido",
        LoaderVersion = "1.0",
        ArchiveFileId = "archivo-de-loader-no-compatible"
    };
    string[] cacheEntriesBeforeUnsupportedPack = Directory.GetFileSystemEntries(installerCache)
        .OrderBy(path => path, LauncherPathRules.PathComparer)
        .ToArray();
    string[] temporaryEntriesBeforeUnsupportedPack = Directory.Exists(InstanceService.TempRoot)
        ? Directory.GetFileSystemEntries(InstanceService.TempRoot)
            .OrderBy(path => path, LauncherPathRules.PathComparer)
            .ToArray()
        : Array.Empty<string>();

    bool unsupportedPackRejected = false;
    try
    {
        await installer.InstallOrUpdateAsync(unsupportedManifest, "TEST-LOADER", null);
    }
    catch (NotSupportedException)
    {
        unsupportedPackRejected = true;
    }

    string[] cacheEntriesAfterUnsupportedPack = Directory.GetFileSystemEntries(installerCache)
        .OrderBy(path => path, LauncherPathRules.PathComparer)
        .ToArray();
    string[] temporaryEntriesAfterUnsupportedPack = Directory.Exists(InstanceService.TempRoot)
        ? Directory.GetFileSystemEntries(InstanceService.TempRoot)
            .OrderBy(path => path, LauncherPathRules.PathComparer)
            .ToArray()
        : Array.Empty<string>();

    Assert(unsupportedPackRejected &&
        !Directory.Exists(instanceService.GetInstanceDirectory(unsupportedPackId)) &&
        cacheEntriesBeforeUnsupportedPack.SequenceEqual(cacheEntriesAfterUnsupportedPack, LauncherPathRules.PathComparer) &&
        temporaryEntriesBeforeUnsupportedPack.SequenceEqual(temporaryEntriesAfterUnsupportedPack, LauncherPathRules.PathComparer),
        "Un loader no soportado debe rechazarse sin crear carpetas, temporales ni archivos de caché.");

    var installManifest = new ModpackManifest
    {
        Id = "overland", Name = "OVERLAND SMP", Version = "1.0.0",
        MinecraftVersion = "1.20.1", Loader = "Forge", LoaderVersion = "47.4.0",
        ArchiveFileId = "archive-file"
    };
    InstalledInstance installedInstance = await installer.InstallOrUpdateAsync(
        installManifest, "OVERLAND-123", null!);
    string installedDirectory = instanceService.GetInstanceDirectory("overland");

    // La galería debe mostrar las capturas que siguen en una instalación y las archivadas.
    string activeScreenshots = Path.Combine(installedDirectory, "screenshots");
    Directory.CreateDirectory(Path.Combine(activeScreenshots, "subcarpeta"));
    string activeScreenshotFile = Path.Combine(activeScreenshots, "subcarpeta", "captura-activa.PNG");
    await File.WriteAllBytesAsync(activeScreenshotFile, new byte[] { 1, 2, 3, 4 });
    await File.WriteAllTextAsync(Path.Combine(activeScreenshots, "ignorar.txt"), "no es una captura");

    string galleryArchiveRoot = Path.Combine(root, "galeria-archivo-prueba");
    string retiredScreenshots = Path.Combine(root, "instancia-retirada", "screenshots");
    Directory.CreateDirectory(retiredScreenshots);
    await File.WriteAllBytesAsync(Path.Combine(retiredScreenshots, "captura-archivada.jpg"), new byte[] { 5, 6, 7 });
    var galleryArchiveStore = new ScreenshotArchiveStore(galleryArchiveRoot);
    string archivedGalleryScreenshots = galleryArchiveStore.ArchiveInstanceScreenshots(
        "instancia-retirada", "Instancia retirada", retiredScreenshots);

    var galleryScanner = new ScreenshotGalleryScanner(instanceService, galleryArchiveStore);
    IReadOnlyList<ScreenshotGalleryFile> galleryFiles = await galleryScanner.LoadAllAsync(
        new[] { installedInstance });
    Assert(galleryFiles.Count == 2 &&
        galleryFiles.Any(x => !x.IsArchived &&
            x.InstanceId == installedInstance.Id &&
            x.FilePath == Path.GetFullPath(activeScreenshotFile)) &&
        galleryFiles.Any(x => x.IsArchived &&
            x.InstanceId == "instancia-retirada" &&
            x.InstanceName == "Instancia retirada" &&
            x.FilePath == Path.GetFullPath(Path.Combine(archivedGalleryScreenshots, "captura-archivada.jpg"))) &&
        galleryFiles.All(x => !x.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)),
        "La galería debe combinar capturas activas y archivadas, recorrer subcarpetas y omitir archivos que no sean imágenes.");

    Assert(installedInstance.IsInstalled &&
        File.ReadAllText(Path.Combine(installedDirectory, "mods", "ejemplo.txt")) == "mod versión uno" &&
        File.ReadAllText(Path.Combine(installedDirectory, "options.txt")) == "preferencias del usuario" &&
        File.Exists(Path.Combine(installedDirectory, "mods", "obsoleto.txt")) &&
        File.Exists(Path.Combine(installedDirectory, "instance.json")),
        "El instalador compartido debe instalar un ZIP cacheado y guardar la instancia sin descargar archivos.");

    string secondArchivePath = Path.Combine(installerCache, "overland-1.0.1-archive-file-v2.zip");
    using (var zip = ZipFile.Open(secondArchivePath, ZipArchiveMode.Create))
    {
        using (var writer = new StreamWriter(zip.CreateEntry("mods/ejemplo.txt").Open()))
        {
            writer.Write("mod versión dos");
        }
        using (var newFileWriter = new StreamWriter(zip.CreateEntry("config/nuevo.txt").Open()))
        {
            newFileWriter.Write("archivo nuevo");
        }
        using (var optionsWriter = new StreamWriter(zip.CreateEntry("options.txt").Open()))
        {
            optionsWriter.Write("preferencias del paquete");
        }
    }

    var updateManifest = new ModpackManifest
    {
        Id = "overland", Name = "OVERLAND SMP", Version = "1.0.1",
        MinecraftVersion = "1.20.1", Loader = "Forge", LoaderVersion = "47.4.0",
        ArchiveFileId = "archive-file-v2"
    };
    InstalledInstance updatedInstance = await installer.InstallOrUpdateAsync(
        updateManifest, "OVERLAND-123", installedInstance);
    Assert(updatedInstance.InstalledVersion == "1.0.1",
        "La actualización debe guardar la versión nueva del modpack.");
    Assert(File.ReadAllText(Path.Combine(installedDirectory, "mods", "ejemplo.txt")) == "mod versión dos",
        "La actualización debe comparar el contenido real y reemplazar archivos aunque tamaño y fecha coincidan.");
    Assert(File.ReadAllText(Path.Combine(installedDirectory, "options.txt")) == "preferencias del usuario",
        "La actualización debe preservar options.txt del usuario.");
    Assert(!File.Exists(Path.Combine(installedDirectory, "mods", "obsoleto.txt")),
        "La actualización debe eliminar los archivos administrados que ya no están en el ZIP.");
    Assert(File.Exists(Path.Combine(installedDirectory, "config", "nuevo.txt")),
        "La actualización debe instalar los archivos nuevos del ZIP.");

    string sharedMinecraftRoot = InstanceService.SharedMinecraftRoot;
    await instanceService.DeleteInstanceAsync("overland");
    Assert(!Directory.Exists(installedDirectory),
        "Eliminar una instancia debe borrar únicamente la carpeta de esa instancia.");
    Assert(Directory.Exists(sharedMinecraftRoot),
        "Eliminar una instancia no debe borrar los recursos compartidos de Minecraft.");

    bool invalidInstanceIdRejected = false;
    try
    {
        await instanceService.DeleteInstanceAsync("../fuera");
    }
    catch (InvalidOperationException)
    {
        invalidInstanceIdRejected = true;
    }
    Assert(invalidInstanceIdRejected,
        "La eliminación debe rechazar IDs que intenten escapar del directorio de instancias.");

    // Cambiar de volumen debe conservar las instancias, los recursos compartidos
    // y la caché; las rutas con espacios son válidas en los tres sistemas.
    string migrationSource = instanceService.GetStorageRoot();
    string migrationTarget = Path.Combine(root, "nuevo almacenamiento multiplataforma");
    string instanceMarker = Path.Combine(InstanceService.InstancesRoot, "migracion-prueba", "instance.json");
    string minecraftMarker = Path.Combine(InstanceService.SharedMinecraftAssetsRoot, "migracion-prueba.txt");
    string cacheMarker = Path.Combine(InstanceService.PackageCacheRoot, "migracion-cache.zip");
    Directory.CreateDirectory(Path.GetDirectoryName(instanceMarker)!);
    await File.WriteAllTextAsync(instanceMarker, """{"Id":"migracion-prueba"}""");
    await File.WriteAllTextAsync(minecraftMarker, "assets compartidos");
    await File.WriteAllTextAsync(cacheMarker, "cache de prueba");
    await File.WriteAllTextAsync(Path.Combine(InstanceService.TempRoot, "temporal.txt"), "temporal");

    await instanceService.ChangeStorageRootAsync(migrationTarget);
    Assert(LauncherPathRules.PathsEqual(instanceService.GetStorageRoot(), migrationTarget),
        "El cambio de almacenamiento debe activar la nueva raíz normalizada.");
    Assert(File.ReadAllText(Path.Combine(InstanceService.InstancesRoot, "migracion-prueba", "instance.json")) ==
            """{"Id":"migracion-prueba"}""",
        "El cambio de almacenamiento debe conservar los metadatos de instancias.");
    Assert(File.ReadAllText(Path.Combine(InstanceService.SharedMinecraftAssetsRoot, "migracion-prueba.txt")) ==
            "assets compartidos",
        "El cambio de almacenamiento debe conservar los recursos compartidos de Minecraft.");
    Assert(File.ReadAllText(Path.Combine(InstanceService.PackageCacheRoot, "migracion-cache.zip")) ==
            "cache de prueba",
        "El cambio de almacenamiento debe conservar la caché de paquetes.");
    Assert(!Directory.Exists(Path.Combine(migrationSource, "temp")),
        "Los temporales antiguos deben eliminarse después de completar la migración.");

    // Una raíz nueva dentro de la actual produciría destinos anidados y puede
    // hacer que la caché intente copiarse dentro de sí misma.
    string nestedStorageTarget = Path.Combine(instanceService.GetStorageRoot(), "cache", "almacen-anidado");
    bool nestedStorageRejected = false;
    try
    {
        await instanceService.ChangeStorageRootAsync(nestedStorageTarget);
    }
    catch (InvalidOperationException)
    {
        nestedStorageRejected = true;
    }
    Assert(nestedStorageRejected &&
        LauncherPathRules.PathsEqual(instanceService.GetStorageRoot(), migrationTarget) &&
        File.Exists(Path.Combine(InstanceService.InstancesRoot, "migracion-prueba", "instance.json")),
        "Una raíz de almacenamiento anidada debe rechazarse sin mover ni perder las instancias.");

    // Si el destino ya contiene datos, la migración debe abortar sin sobrescribirlos
    // ni borrar los datos originales.
    string protectedMarker = Path.Combine(InstanceService.InstancesRoot, "protegido.txt");
    await File.WriteAllTextAsync(protectedMarker, "origen intacto");
    string occupiedTarget = Path.Combine(root, "destino ocupado");
    Directory.CreateDirectory(Path.Combine(occupiedTarget, "instances"));
    string occupiedMarker = Path.Combine(occupiedTarget, "instances", "no-sobrescribir.txt");
    await File.WriteAllTextAsync(occupiedMarker, "destino intacto");
    bool occupiedDestinationRejected = false;
    try
    {
        await instanceService.ChangeStorageRootAsync(occupiedTarget);
    }
    catch (InvalidOperationException)
    {
        occupiedDestinationRejected = true;
    }
    Assert(occupiedDestinationRejected &&
        File.ReadAllText(protectedMarker) == "origen intacto" &&
        File.ReadAllText(occupiedMarker) == "destino intacto",
        "Una migración hacia un destino ocupado debe detenerse y preservar ambos conjuntos de datos.");

    // Si el traslado de instancias funciona pero el de Minecraft falla,
    // el servicio debe revertir el primer movimiento y mantener la raíz activa.
    string rootBeforeFailedMigration = instanceService.GetStorageRoot();
    string failedTarget = Path.Combine(root, "destino con minecraft ocupado");
    Directory.CreateDirectory(Path.Combine(failedTarget, "minecraft"));
    string failedTargetMarker = Path.Combine(failedTarget, "minecraft", "no-sobrescribir.txt");
    await File.WriteAllTextAsync(failedTargetMarker, "minecraft de destino");
    bool partialMigrationRejected = false;
    try
    {
        await instanceService.ChangeStorageRootAsync(failedTarget);
    }
    catch (InvalidOperationException)
    {
        partialMigrationRejected = true;
    }
    Assert(partialMigrationRejected &&
        LauncherPathRules.PathsEqual(instanceService.GetStorageRoot(), rootBeforeFailedMigration) &&
        File.ReadAllText(protectedMarker) == "origen intacto" &&
        File.ReadAllText(failedTargetMarker) == "minecraft de destino" &&
        !Directory.Exists(Path.Combine(failedTarget, "instances")),
        "Si falla el segundo movimiento, el servicio debe revertir las instancias, retirar la copia trasladada y conservar ambas raíces.");

    // La limpieza solo puede borrar carpetas conocidas del almacenamiento del launcher.
    var unusedCleanupService = new UnusedFilesCleanupService(instanceService);
    string unmanagedCleanupPath = Path.Combine(root, "carpeta-ajena-a-la-limpieza");
    Directory.CreateDirectory(unmanagedCleanupPath);
    string unmanagedMarker = Path.Combine(unmanagedCleanupPath, "conservar.txt");
    await File.WriteAllTextAsync(unmanagedMarker, "no borrar");
    var unmanagedCleanupResult = await unusedCleanupService.ExecuteAsync(
        new UnusedFilesCleanupPlan(new[]
        {
            new UnusedFilesCleanupEntry(unmanagedCleanupPath, 10, "externo", "ruta no administrada")
        }));
    Assert(unmanagedCleanupResult.DeletedEntries == 0 &&
        unmanagedCleanupResult.FailedEntries == 1 &&
        File.Exists(unmanagedMarker),
        "La limpieza debe rechazar entradas de un plan que apunten fuera de las rutas administradas.");

    // Las carpetas de versiones normales continúan pudiéndose limpiar.
    string unusedVersionDirectory = Path.Combine(
        InstanceService.SharedMinecraftVersionsRoot, "version-sin-uso-prueba");
    Directory.CreateDirectory(unusedVersionDirectory);
    string unusedVersionFile = Path.Combine(unusedVersionDirectory, "prueba.json");
    await File.WriteAllTextAsync(unusedVersionFile, "{}");
    var safeCleanupResult = await unusedCleanupService.ExecuteAsync(
        new UnusedFilesCleanupPlan(new[]
        {
            new UnusedFilesCleanupEntry(
                unusedVersionDirectory,
                new FileInfo(unusedVersionFile).Length,
                "Minecraft compartido",
                "versión de prueba")
        }));
    Assert(safeCleanupResult.DeletedEntries == 1 &&
        safeCleanupResult.FailedEntries == 0 &&
        !Directory.Exists(unusedVersionDirectory),
        "La limpieza debe seguir eliminando una versión que esté dentro de la raíz permitida y no contenga enlaces.");

    if (!OperatingSystem.IsWindows())
    {
        // Una entrada que tenga un enlace interno no debe borrarse parcialmente ni seguirlo.
        string linkedVersionDirectory = Path.Combine(
            InstanceService.SharedMinecraftVersionsRoot, "version-con-enlace-prueba");
        string externalCleanupDirectory = Path.Combine(root, "datos-fuera-del-launcher");
        Directory.CreateDirectory(linkedVersionDirectory);
        Directory.CreateDirectory(externalCleanupDirectory);
        string externalCleanupMarker = Path.Combine(externalCleanupDirectory, "conservar.txt");
        await File.WriteAllTextAsync(externalCleanupMarker, "datos protegidos");
        Directory.CreateSymbolicLink(
            Path.Combine(linkedVersionDirectory, "enlace"),
            externalCleanupDirectory);

        var linkedCleanupResult = await unusedCleanupService.ExecuteAsync(
            new UnusedFilesCleanupPlan(new[]
            {
                new UnusedFilesCleanupEntry(linkedVersionDirectory, 100, "Minecraft compartido", "versión enlazada")
            }));

        Assert(linkedCleanupResult.DeletedEntries == 0 &&
            linkedCleanupResult.FailedEntries == 1 &&
            Directory.Exists(linkedVersionDirectory) &&
            await File.ReadAllTextAsync(externalCleanupMarker) == "datos protegidos",
            "La limpieza debe rechazar árboles con enlaces simbólicos y conservar los datos externos.");
    }

    // La estructura de almacenamiento debe usar rutas nativas y mantenerse igual en los tres sistemas.
    string layoutRoot = Path.Combine(root, "datos-launcher");
    var storageLayout = new LauncherStorageLayout(layoutRoot);
    Assert(storageLayout.Root == Path.GetFullPath(layoutRoot),
        "La raíz de almacenamiento debe normalizarse a una ruta absoluta.");
    Assert(storageLayout.InstancesRoot == Path.Combine(storageLayout.Root, "instances") &&
        storageLayout.PackageCacheRoot == Path.Combine(storageLayout.Root, "cache", "packages") &&
        storageLayout.TempRoot == Path.Combine(storageLayout.Root, "temp"),
        "Las instancias, la caché y los temporales deben quedar dentro de la raíz elegida.");
    Assert(storageLayout.SharedMinecraftAssetsRoot == Path.Combine(storageLayout.Root, "minecraft", "assets") &&
        storageLayout.SharedMinecraftLibrariesRoot == Path.Combine(storageLayout.Root, "minecraft", "libraries") &&
        storageLayout.SharedMinecraftVersionsRoot == Path.Combine(storageLayout.Root, "minecraft", "versions") &&
        storageLayout.SharedMinecraftRuntimeRoot == Path.Combine(storageLayout.Root, "minecraft", "runtime"),
        "Los recursos compartidos de Minecraft deben conservar una estructura estable entre Windows, Linux y macOS.");

    string normalizedLayoutRoot = LauncherStorageLayout.NormalizeRoot(
        "  " + layoutRoot + "  ", Path.Combine(root, "valor-predeterminado"));
    Assert(normalizedLayoutRoot == Path.GetFullPath(layoutRoot).TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
        "La ruta configurada debe recortarse y normalizarse antes de usarla.");
    Assert(LauncherStorageLayout.NormalizeRoot(
            "  ", layoutRoot) == Path.GetFullPath(layoutRoot).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
        "Una ruta vacía debe volver al directorio predeterminado de almacenamiento.");

    // Las raíces del sistema no deben convertirse en cadenas vacías al normalizar.
    string nativeRoot = Path.GetPathRoot(Path.GetFullPath(Path.DirectorySeparatorChar.ToString()))
        ?? throw new InvalidOperationException("El sistema no devolvió una raíz de ruta.");
    Assert(LauncherPathRules.NormalizeFullPath(nativeRoot) == nativeRoot,
        "La normalización debe conservar intacta la raíz del sistema de archivos.");

    // Una carpeta hermana con un prefijo parecido no es descendiente de la carpeta protegida.
    string protectedStorage = Path.Combine(root, "negative-storage");
    string siblingStorage = Path.Combine(root, "negative-storage-backup");
    Assert(LauncherPathRules.IsSameOrSubPath(
            Path.Combine(protectedStorage, "instances", "mundo"), protectedStorage) &&
        !LauncherPathRules.IsSameOrSubPath(siblingStorage, protectedStorage),
        "La validación de subrutas debe aceptar descendientes reales y rechazar carpetas hermanas con prefijos parecidos.");

    if (OperatingSystem.IsLinux())
    {
        string caseSensitiveDirectory = Path.Combine(root, "case-sensitive-path-check");
        Directory.CreateDirectory(caseSensitiveDirectory);
        Assert(LauncherPathRules.GetPathComparison(caseSensitiveDirectory) == StringComparison.Ordinal,
            "En Linux, una carpeta normal debe conservar la comparación sensible a mayúsculas.");
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
