using System.Text.Json;
using Negative_Client.Models;
using Negative_Client.Services;

string root = Path.Combine(Path.GetTempPath(), "NegativeLauncher.Core.Tests", Guid.NewGuid().ToString("N"));
try
{
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
