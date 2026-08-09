using System;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UndertaleModLib.Project;
using UndertaleModLib.Util;

namespace UndertaleModTests;

[TestClass]
public class ProjectSecurityTests
{
    [TestMethod]
    public void ProjectScriptsRequireExplicitOptIn()
    {
        string root = Path.Combine(Path.GetTempPath(), $"umt-project-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string projectPath = Path.Combine(root, "project.json");
        try
        {
            ProjectContext defaultContext = ProjectContext.CreateWithDirectories(root, root, projectPath);
            ProjectContext trustedContext = ProjectContext.CreateWithDirectories(root, root, projectPath, allowScripts: true);

            Assert.IsFalse(defaultContext.AllowScripts);
            Assert.IsTrue(trustedContext.AllowScripts);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void ProjectPathsRejectDirectoryLinks()
    {
        string root = Path.Combine(Path.GetTempPath(), $"umt-root-{Guid.NewGuid():N}");
        string outside = Path.Combine(Path.GetTempPath(), $"umt-outside-{Guid.NewGuid():N}");
        string link = Path.Combine(root, "linked");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        try
        {
            try
            {
                Directory.CreateSymbolicLink(link, outside);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                Assert.Inconclusive($"Symbolic links are unavailable on this runner: {ex.Message}");
            }

            Assert.Throws<IOException>(() => Paths.VerifyWithinDirectory(root, Path.Combine(link, "file.txt")));
        }
        finally
        {
            if (Directory.Exists(link))
                Directory.Delete(link);
            Directory.Delete(root, true);
            Directory.Delete(outside, true);
        }
    }

    [TestMethod]
    public void BackupUsesNextAvailableDirectoryInsideGameRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), $"umt-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, GameFileBackup.DefaultBackupDirectoryName));
        File.WriteAllText(Path.Combine(root, GameFileBackup.DefaultBackupDirectoryName + "1"), "occupied");
        try
        {
            GameFileBackup backup = new(root);
            backup.BackupFile(Path.Combine(root, "new-file.txt"));
            backup.SaveManifest();

            Assert.IsTrue(Directory.Exists(Path.Combine(root, GameFileBackup.DefaultBackupDirectoryName + "2")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void BackupDeduplicatesCaseEquivalentWindowsPaths()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("This behavior is specific to case-insensitive Windows paths.");

        string root = Path.Combine(Path.GetTempPath(), $"umt-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            GameFileBackup backup = new(root);
            backup.BackupFile(Path.Combine(root, "new-file.txt"));
            backup.BackupFile(Path.Combine(root, "NEW-FILE.TXT"));
            backup.SaveManifest();

            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, GameFileBackup.ManifestFilename)));
            Assert.AreEqual(1, manifest.RootElement.GetProperty("Files").GetArrayLength());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void BackupRejectsPathsOutsideGameRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), $"umt-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            GameFileBackup backup = new(root);
            string outside = Path.Combine(Path.GetTempPath(), $"umt-outside-{Guid.NewGuid():N}.txt");

            Assert.Throws<Exception>(() => backup.BackupFile(outside));
            Assert.IsFalse(File.Exists(Path.Combine(root, GameFileBackup.ManifestFilename)));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
