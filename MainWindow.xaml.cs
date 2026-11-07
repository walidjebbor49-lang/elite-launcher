private async Task DownloadRequiredModsAsync()
{
    if (_path == null) return;

    // Directorios locales de Minecraft
    string modsDir = Path.Combine(_path.BasePath, "mods");
    string resourcepacksDir = Path.Combine(_path.BasePath, "resourcepacks");

    Directory.CreateDirectory(modsDir);
    Directory.CreateDirectory(resourcepacksDir);

    // Tu usuario y repositorio de GitHub (sacado de tu propio enlace de GitHub)
    string githubUser = "walidjebbor49-lang";
    string repoName = "elite-launcher"; // Cambia esto si tu repositorio se llama diferente
    string branch = "main";

    // Enlaces directos a las carpetas 'mods' y 'pancho' en tu GitHub
    // (Ejemplo de estructura de archivos que tendrás subidos en tu repo)
    var filesToDownload = new Dictionary<string, string>
    {
        // Mods (se guardan en la carpeta 'mods')
        { Path.Combine(modsDir, "tu-mod.jar"), $"https://raw.githubusercontent.com/{githubUser}/{repoName}/{branch}/mods/tu-mod.jar" },
        
        // Texture Packs / Pancho (se guardan en la carpeta 'resourcepacks')
        { Path.Combine(resourcepacksDir, "texturepack.zip"), $"https://raw.githubusercontent.com/{githubUser}/{repoName}/{branch}/pancho/texturepack.zip" }
    };

    using var client = new HttpClient();

    foreach (var file in filesToDownload)
    {
        // Si el archivo ya existe localmente, no lo vuelve a descargar para ahorrar tiempo
        if (!File.Exists(file.Key))
        {
            try
            {
                byte[] bytes = await client.GetByteArrayAsync(file.Value);
                await File.WriteAllBytesAsync(file.Key, bytes);
            }
            catch
            {
                // Si falla uno porque aún no lo has subido, el juego arranca igual sin romperse
            }
        }
    }
}
