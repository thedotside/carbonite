using System.Globalization;
using System.Text;
using TheDotSide.Carbonite.Text;

namespace TheDotSide.Carbonite.Engine;

/// <summary>Reads and writes snapshot files: UTF-8 without BOM, LF, one final line feed.</summary>
internal static class SnapshotFile
{
    private static readonly UTF8Encoding Utf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true
    );

    // A file another test or test process is still creating: empty, or locked by its writer.
    private const int ReadAttempts = 40;
    private static readonly TimeSpan ReadRetryDelay = TimeSpan.FromMilliseconds(25);

    /// <summary>Returns the normalized content of a snapshot file, or <see langword="null"/> when it does not exist.</summary>
    internal static string? ReadNormalized(string path)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                // Every file Carbonite writes ends with a line feed: an empty file is still being
                // created by a parallel test (for example the same tests on another framework).
                if (bytes.Length == 0 && attempt < ReadAttempts)
                {
                    Thread.Sleep(ReadRetryDelay);
                    continue;
                }

                return TextNormalizer.Normalize(Utf8.GetString(bytes));
            }
            catch (Exception exception)
                when (exception is FileNotFoundException or DirectoryNotFoundException)
            {
                return null;
            }
            catch (IOException) when (attempt < ReadAttempts)
            {
                // Locked by the process that is writing it.
                Thread.Sleep(ReadRetryDelay);
            }
        }
    }

    /// <summary>Writes <paramref name="content"/> through a temporary file, replacing any existing file.</summary>
    internal static void WriteAtomic(string path, string content)
    {
        var temporary = WriteTemporary(path, content);
        try
        {
            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            DeleteQuietly(temporary);
            throw;
        }
    }

    /// <summary>
    /// Creates the file only if it does not exist yet. Returns <see langword="false"/> and leaves
    /// the existing file untouched when another test created it first.
    /// </summary>
    internal static bool TryCreateNew(string path, string content)
    {
        // FileMode.CreateNew is the only creation that is atomic everywhere (O_EXCL, CREATE_NEW).
        // File.Move(overwrite: false) checks then renames on macOS and Linux, so two tests could
        // both "create" the same snapshot. FileShare.None keeps readers out until it is complete.
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var bytes = Utf8.GetBytes(TextNormalizer.Normalize(content) + "\n");
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None
            );
            stream.Write(bytes);
            return true;
        }
        catch (IOException) when (File.Exists(path))
        {
            return false;
        }
    }

    /// <summary>Deletes the file when it exists.</summary>
    internal static void DeleteIfExists(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (DirectoryNotFoundException)
        {
            // Nothing to delete.
        }
    }

    private static string WriteTemporary(string path, string content)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(
            directory,
            ".carbonite-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".tmp"
        );
        File.WriteAllText(temporary, TextNormalizer.Normalize(content) + "\n", Utf8);
        return temporary;
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The original failure matters more than a leftover temporary file.
        }
    }
}
