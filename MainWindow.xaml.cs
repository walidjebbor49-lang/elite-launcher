private async Task DownloadAndExtractModsAsync(string modsZipName)
{
    if (_path == null) return;

    string modsDir = Path.Combine(_path.BasePath, "mods");
    string resourcepacksDir = Path.Combine(_path.BasePath, "resourcepacks");

    Directory.CreateDirectory(modsDir);
    Directory.CreateDirectory(resourcepacksDir);

    string tempModsZip = Path.Combine(_path.BasePath, modsZipName);
    string panchoTexturePackPath = Path.Combine(resourcepacksDir, "pancho.zip");

    string githubUser = "walidjebbor49-lang";
    string repoName = "elite-launcher";
    string releaseTag = "v1.0.0"; // Coincide perfectamente con la tag que se ve en tu foto

    string modsUrl = $"https://github.com/{githubUser}/{repoName}/releases/download/{releaseTag}/{modsZipName}";
    string panchoUrl = $"https://github.com/{githubUser}/{repoName}/releases/download/{releaseTag}/pancho.zip";

    using var client = new HttpClient();

    // 1. Descargar y descomprimir los mods (Solo si el archivo existe en el Release)
    try
    {
        byte[] zipBytes = await client.GetByteArrayAsync(modsUrl);
        await File.WriteAllBytesAsync(tempModsZip, zipBytes);
        ZipFile.ExtractToDirectory(tempModsZip, modsDir, overwriteFiles: true);

        if (File.Exists(tempModsZip))
        {
            File.Delete(tempModsZip);
        }
    }
    catch
    {
        // Si intentan iniciar la 1.21 y el .zip aún no está subido, el juego arranca sin romperse
    }

    // 2. Descargar el texture pack Pancho
    if (!File.Exists(panchoTexturePackPath))
    {
        try
        {
            byte[] panchoBytes = await client.GetByteArrayAsync(panchoUrl);
            await File.WriteAllBytesAsync(panchoTexturePackPath, panchoBytes);
        }
        catch
        {
            // Salva cualquier error si el texture pack no estuviese disponible temporalmente
        }
    }
}
