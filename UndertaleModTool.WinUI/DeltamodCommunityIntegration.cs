using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace UndertaleModTool_WinUI;

internal static partial class DeltamodCommunityIntegration
{
    private const string ManifestFileName = ".deltamod-community.json";
    private const int MaximumManifestBytes = 64 * 1024;

    [GeneratedRegex(@"^[a-z0-9][a-z0-9_-]{0,62}(?:\.[a-z0-9][a-z0-9_-]{0,62}){2}$", RegexOptions.IgnoreCase)]
    private static partial Regex PackageIdRegex();

    [GeneratedRegex(@"^[a-z0-9][a-z0-9_-]*(?:\.[a-z0-9][a-z0-9_-]*)+$", RegexOptions.IgnoreCase)]
    private static partial Regex GameIdRegex();

    internal static bool CanExport(string? dataFilePath) =>
        TryLoadContext(dataFilePath, out _, out _);

    internal static async Task<CommunityExportResult> ExportAsync(
        string dataFilePath,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        if (!TryLoadContext(dataFilePath, out CommunityIntegrationContext? context, out string error) ||
            context is null)
            throw new InvalidOperationException(error);

        string exportId = $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}";
        string stagingDirectory = Path.Combine(context.ExportRoot, $"{exportId}.staging");
        string projectDirectory = Path.Combine(context.ExportRoot, exportId);
        string patchesDirectory = Path.Combine(stagingDirectory, "patches");
        string exportedDataFile = Path.Combine(patchesDirectory, context.DataFileName);
        bool projectCommitted = false;

        try
        {
            Directory.CreateDirectory(patchesDirectory);
            string sha256 = await CopyAndHashAsync(context.DataFilePath, exportedDataFile, cancellationToken);
            string version = $"0.0.{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

            string manifest = BuildManifest(context, version, sha256);
            string patchManifest =
                $"<patch type=\"override\" patch=\"./patches/{context.DataFileName}\" to=\"./{context.DataFileName}\" />{Environment.NewLine}";
            await File.WriteAllTextAsync(
                Path.Combine(stagingDirectory, "meta.toml"),
                manifest,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(stagingDirectory, "modding.xml"),
                patchManifest,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);

            Directory.Move(stagingDirectory, projectDirectory);
            projectCommitted = true;

            ProcessStartInfo startInfo = new()
            {
                FileName = context.CliExecutable,
                WorkingDirectory = projectDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("import");
            startInfo.ArgumentList.Add(projectDirectory);
            startInfo.ArgumentList.Add("--target");
            startInfo.ArgumentList.Add("community");
            if (dryRun)
                startInfo.ArgumentList.Add("--dry-run");

            using Process process = new() { StartInfo = startInfo };
            if (!process.Start())
                throw new InvalidOperationException("Deltamod Community CLI did not start.");

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(2));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                TryTerminate(process);
                throw new TimeoutException("Deltamod Community CLI did not finish within two minutes.");
            }

            string output = await outputTask;
            string standardError = await errorTask;
            if (process.ExitCode != 0)
            {
                string details = FirstUsefulLine(standardError) ?? FirstUsefulLine(output) ?? "No diagnostic output was returned.";
                throw new InvalidOperationException($"Deltamod Community CLI rejected the export: {details}");
            }

            return new CommunityExportResult(projectDirectory, version, sha256, output.Trim());
        }
        catch
        {
            if (!projectCommitted && Directory.Exists(stagingDirectory))
            {
                try
                {
                    Directory.Delete(stagingDirectory, recursive: true);
                }
                catch
                {
                    // Preserve the original export failure.
                }
            }
            throw;
        }
    }

    internal static bool TryLoadContext(
        string? dataFilePath,
        out CommunityIntegrationContext? context,
        out string error)
    {
        context = null;
        error = "This file was not opened from a Deltamod Community workspace.";
        if (string.IsNullOrWhiteSpace(dataFilePath))
            return false;

        try
        {
            string fullDataPath = Path.GetFullPath(dataFilePath);
            FileInfo dataFile = new(fullDataPath);
            if (!IsRegularFile(dataFile))
            {
                error = "The Community workspace data file is missing or linked.";
                return false;
            }

            string editorDirectory = dataFile.DirectoryName
                ?? throw new InvalidOperationException("The data file has no parent directory.");
            string manifestPath = Path.Combine(editorDirectory, ManifestFileName);
            FileInfo manifestFile = new(manifestPath);
            if (!IsRegularFile(manifestFile) || manifestFile.Length > MaximumManifestBytes)
                return false;

            string json = File.ReadAllText(manifestPath, Encoding.UTF8);
            CommunityManifest? manifest = JsonSerializer.Deserialize<CommunityManifest>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (manifest?.SchemaVersion != 1 ||
                string.IsNullOrWhiteSpace(manifest.DataFile) ||
                Path.GetFileName(manifest.DataFile) != manifest.DataFile ||
                string.IsNullOrWhiteSpace(manifest.ExportRoot) ||
                manifest.Package is null)
            {
                error = "The Deltamod Community workspace manifest is invalid.";
                return false;
            }

            string declaredDataPath = Path.GetFullPath(Path.Combine(editorDirectory, manifest.DataFile));
            if (!string.Equals(declaredDataPath, fullDataPath, StringComparison.OrdinalIgnoreCase))
            {
                error = "The workspace manifest does not belong to this data file.";
                return false;
            }

            DirectoryInfo? workspaceDirectory = Directory.GetParent(editorDirectory);
            if (workspaceDirectory is null || !IsRegularDirectory(workspaceDirectory))
            {
                error = "The Deltamod Community workspace directory is unavailable.";
                return false;
            }

            string exportRoot = Path.GetFullPath(Path.Combine(editorDirectory, manifest.ExportRoot));
            if (!IsWithin(workspaceDirectory.FullName, exportRoot))
            {
                error = "The Community export directory escapes the workspace.";
                return false;
            }
            DirectoryInfo exportDirectory = new(exportRoot);
            if (!IsRegularDirectory(exportDirectory))
            {
                error = "The Community export directory is missing or linked.";
                return false;
            }

            string cliExecutable = Path.GetFullPath(manifest.CliExecutable ?? string.Empty);
            FileInfo cliFile = new(cliExecutable);
            if (!Path.IsPathFullyQualified(cliExecutable) ||
                !string.Equals(cliFile.Extension, ".exe", StringComparison.OrdinalIgnoreCase) ||
                !IsRegularFile(cliFile))
            {
                error = "The configured Deltamod Community CLI executable is unavailable or linked.";
                return false;
            }

            string packageId = manifest.Package.PackageId?.Trim() ?? string.Empty;
            string gameId = manifest.Package.Game?.Trim() ?? string.Empty;
            if (!PackageIdRegex().IsMatch(packageId) || !GameIdRegex().IsMatch(gameId))
            {
                error = "The Community package or game identifier is invalid.";
                return false;
            }

            context = new CommunityIntegrationContext(
                fullDataPath,
                dataFile.Name,
                exportRoot,
                cliFile.FullName,
                CleanDisplayValue(manifest.Package.Name, "UndertaleModTool edits"),
                packageId,
                gameId,
                CleanDisplayValue(manifest.Package.Author, Environment.UserName));
            error = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or JsonException or NotSupportedException)
        {
            error = $"The Deltamod Community workspace could not be validated: {ex.Message}";
            return false;
        }
    }

    private static async Task<string> CopyAndHashAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using FileStream source = new(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using FileStream destination = new(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[1024 * 1024];
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            hash.AppendData(buffer, 0, read);
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        await destination.FlushAsync(cancellationToken);
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string BuildManifest(CommunityIntegrationContext context, string version, string sha256) =>
        $"""
        [metadata]
        name = {TomlString(context.PackageName)}
        version = {TomlString(version)}
        description = {TomlString($"Full GameMaker data override exported by UndertaleModTool. SHA-256: {sha256}")}
        author = [{TomlString(context.Author)}]
        packageID = {TomlString(context.PackageId)}
        game = {TomlString(context.GameId)}
        mergeSupport = false
        tags = ["UndertaleModTool", "Community integration"]

        [exporter]
        tool = "UndertaleModTool WinUI"
        version = "1"
        """;

    private static string TomlString(string value) =>
        JsonSerializer.Serialize(value);

    private static string CleanDisplayValue(string? value, string fallback)
    {
        string cleaned = string.Join(' ', (value ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned[..Math.Min(cleaned.Length, 160)];
    }

    private static bool IsWithin(string root, string candidate)
    {
        string relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(candidate));
        return !Path.IsPathFullyQualified(relative) &&
               !relative.Equals("..", StringComparison.Ordinal) &&
               !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static bool IsRegularFile(FileInfo file) =>
        file.Exists &&
        (file.Attributes & FileAttributes.ReparsePoint) == 0 &&
        file.LinkTarget is null &&
        HasSingleHardLink(file.FullName);

    private static bool IsRegularDirectory(DirectoryInfo directory) =>
        directory.Exists &&
        (directory.Attributes & FileAttributes.ReparsePoint) == 0 &&
        directory.LinkTarget is null;

    private static string? FirstUsefulLine(string value) =>
        value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
             .Select(line => line.Trim())
             .FirstOrDefault(line => line.Length > 0) is { } line
            ? line[..Math.Min(line.Length, 400)]
            : null;

    private static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // The process may have exited between the checks.
        }
    }

    private static bool HasSingleHardLink(string path)
    {
        try
        {
            using SafeFileHandle handle = File.OpenHandle(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            return GetFileInformationByHandle(handle, out ByHandleFileInformation information) &&
                   information.NumberOfLinks == 1;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out ByHandleFileInformation information);

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    internal sealed record CommunityExportResult(
        string ProjectDirectory,
        string Version,
        string Sha256,
        string CliOutput);

    internal sealed record CommunityIntegrationContext(
        string DataFilePath,
        string DataFileName,
        string ExportRoot,
        string CliExecutable,
        string PackageName,
        string PackageId,
        string GameId,
        string Author);

    private sealed class CommunityManifest
    {
        public int SchemaVersion { get; set; }
        public string? DataFile { get; set; }
        public string? ExportRoot { get; set; }
        public string? CliExecutable { get; set; }
        public CommunityPackage? Package { get; set; }
    }

    private sealed class CommunityPackage
    {
        public string? Name { get; set; }
        public string? PackageId { get; set; }
        public string? Game { get; set; }
        public string? Author { get; set; }
    }
}
