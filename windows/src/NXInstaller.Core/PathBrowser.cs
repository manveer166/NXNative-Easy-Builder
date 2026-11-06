namespace NXInstaller.Core;

public sealed record BrowserEntry(string Path, string Name, bool IsDirectory)
{
    public override string ToString() => (IsDirectory ? "[Folder]  " : "[XIP]  ") + Name;
}
public static class PathBrowser
{
    public static string FullPath(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Enter a local file or folder path.");
        var value = Environment.ExpandEnvironmentVariables(text.Trim().Trim('"'));
        if (value.StartsWith(@"\\", StringComparison.Ordinal)) throw new ArgumentException("Choose a local drive for setup files.");
        return System.IO.Path.GetFullPath(value);
    }
    public static string InitialDirectory(string? path)
    {
        try
        {
            string? value = string.IsNullOrWhiteSpace(path) ? null : FullPath(path);
            while (!string.IsNullOrEmpty(value))
            { if (Directory.Exists(value)) return value; value = System.IO.Path.GetDirectoryName(value); }
        }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException) { }
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
    public static Task<IReadOnlyList<BrowserEntry>> ReadAsync(string location, bool xipOnly, CancellationToken ct) => Task.Run<IReadOnlyList<BrowserEntry>>(() =>
    {
        var folder = FullPath(location); var entries = new List<BrowserEntry>();
        foreach (var item in Directory.EnumerateFileSystemEntries(folder))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                bool directory = File.GetAttributes(item).HasFlag(FileAttributes.Directory);
                if (directory || xipOnly && item.EndsWith(".xip", StringComparison.OrdinalIgnoreCase))
                    entries.Add(new(item, System.IO.Path.GetFileName(item), directory));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            if (entries.Count >= 10000) break;
        }
        return entries.OrderByDescending(x => x.IsDirectory).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }, ct);
}
