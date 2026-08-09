using System;
using System.IO;

namespace UndertaleModLib.Util;

/// <summary>
/// Path utility functions.
/// </summary>
public static class Paths
{
    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="pathToTest"/> is contained within (starts with) <paramref name="directory"/>, 
    /// when the path is converted to be fully-qualified.
    /// </summary>
    /// <remarks>
    /// This can be used for error-checking and simple security purposes.
    /// </remarks>
    public static bool IsWithinDirectory(string directory, string pathToTest)
    {
        string fullDirectoryPath = Path.GetFullPath(directory);
        if (!fullDirectoryPath.EndsWith(Path.DirectorySeparatorChar))
        {
            fullDirectoryPath += Path.DirectorySeparatorChar;
        }
        string fullPathToTest = Path.GetFullPath(pathToTest);
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return fullPathToTest.StartsWith(fullDirectoryPath, comparison);
    }

    /// <summary>
    /// Throws an exception if <paramref name="pathToTest"/> is not contained within (starts with) <paramref name="directory"/>.
    /// </summary>
    /// <remarks>
    /// This can be used for error-checking and simple security purposes.
    /// </remarks>
    public static void VerifyWithinDirectory(string directory, string pathToTest)
    {
        if (!IsWithinDirectory(directory, pathToTest))
        {
            throw new Exception($"Path escapes its root directory ({pathToTest})");
        }
        VerifyNoReparsePoints(directory, pathToTest);
    }

    /// <summary>
    /// Rejects existing symbolic links, junctions, and other reparse points between a root and target path.
    /// </summary>
    public static void VerifyNoReparsePoints(string directory, string pathToTest)
    {
        string fullDirectoryPath = Path.GetFullPath(directory);
        string fullPathToTest = Path.GetFullPath(pathToTest);

        VerifyNotReparsePoint(fullDirectoryPath);
        string relativePath = Path.GetRelativePath(fullDirectoryPath, fullPathToTest);
        string currentPath = fullDirectoryPath;
        foreach (string component in relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                                                         StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Join(currentPath, component);
            VerifyNotReparsePoint(currentPath);
        }
    }

    private static void VerifyNotReparsePoint(string path)
    {
        try
        {
            if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
                throw new IOException($"Path contains a symbolic link or junction ({path})");
        }
        catch (FileNotFoundException)
        {
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    /// <summary>
    /// Similar to <see cref="Path.Join(string?, string?)"/>, but verifies that the end result is 
    /// within <paramref name="directory"/> using <see cref="VerifyWithinDirectory(string, string)"/>.
    /// </summary>
    public static string JoinVerifyWithinDirectory(string directory, string path)
    {
        string joined = Path.Join(directory, path);
        VerifyWithinDirectory(directory, joined);
        return joined;
    }

    /// <summary>
    /// Similar to <see cref="Path.Join(string?, string?, string?)"/>, but verifies that the end result is 
    /// within <paramref name="directory"/> using <see cref="VerifyWithinDirectory(string, string)"/>.
    /// </summary>
    public static string JoinVerifyWithinDirectory(string directory, string path1, string path2)
    {
        string joined = Path.Join(directory, path1, path2);
        VerifyWithinDirectory(directory, joined);
        return joined;
    }

    /// <summary>
    /// Similar to <see cref="Path.Join(string?, string?, string?, string?)"/>, but verifies that the end result is 
    /// within <paramref name="directory"/> using <see cref="VerifyWithinDirectory(string, string)"/>.
    /// </summary>
    public static string JoinVerifyWithinDirectory(string directory, string path1, string path2, string path3)
    {
        string joined = Path.Join(directory, path1, path2, path3);
        VerifyWithinDirectory(directory, joined);
        return joined;
    }

    /// <summary>
    /// Similar to <see cref="Path.Join(string?, string?)"/>, but verifies that the end result is 
    /// within <paramref name="directory"/> using <see cref="IsWithinDirectory(string, string)"/>.
    /// If not verified, this returns <see langword="null"/>.
    /// </summary>
    public static string TryJoinVerifyWithinDirectory(string directory, string path)
    {
        string joined = Path.Join(directory, path);
        if (!IsWithinDirectory(directory, joined))
        {
            return null;
        }
        try
        {
            VerifyNoReparsePoints(directory, joined);
        }
        catch (IOException)
        {
            return null;
        }
        return joined;
    }

    /// <summary>
    /// Similar to <see cref="Path.Join(string?, string?, string?)"/>, but verifies that the end result is 
    /// within <paramref name="directory"/> using <see cref="IsWithinDirectory(string, string)"/>.
    /// If not verified, this returns <see langword="null"/>.
    /// </summary>
    public static string TryJoinVerifyWithinDirectory(string directory, string path1, string path2)
    {
        string joined = Path.Join(directory, path1, path2);
        if (!IsWithinDirectory(directory, joined))
        {
            return null;
        }
        try
        {
            VerifyNoReparsePoints(directory, joined);
        }
        catch (IOException)
        {
            return null;
        }
        return joined;
    }

    /// <summary>
    /// Similar to <see cref="Path.Join(string?, string?, string?, string?)"/>, but verifies that the end result is 
    /// within <paramref name="directory"/> using <see cref="IsWithinDirectory(string, string)"/>.
    /// If not verified, this returns <see langword="null"/>.
    /// </summary>
    public static string TryJoinVerifyWithinDirectory(string directory, string path1, string path2, string path3)
    {
        string joined = Path.Join(directory, path1, path2, path3);
        if (!IsWithinDirectory(directory, joined))
        {
            return null;
        }
        try
        {
            VerifyNoReparsePoints(directory, joined);
        }
        catch (IOException)
        {
            return null;
        }
        return joined;
    }
}
