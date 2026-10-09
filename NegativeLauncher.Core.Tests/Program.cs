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

    Console.WriteLine("Correcto: comprobaciones de preferencias y estado completadas.");
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
