using System.Text.RegularExpressions;

namespace TiktokLiveRec.Extensions;

internal static class SearchFileHelper
{
    public static IEnumerable<string> SearchFiles(string directory, string regexPattern, bool searchSubdirectories = true)
    {
        Regex regex = new(regexPattern, RegexOptions.IgnoreCase);
        HashSet<string> returned = new(StringComparer.OrdinalIgnoreCase);

        foreach (string root in GetSearchRoots(directory))
        {
            foreach (string file in EnumerateFiles(root, regex, SearchOption.TopDirectoryOnly))
            {
                if (returned.Add(file))
                {
                    yield return file;
                }
            }

            if (!searchSubdirectories)
            {
                continue;
            }

            foreach (string file in EnumerateFiles(root, regex, SearchOption.AllDirectories))
            {
                if (returned.Add(file))
                {
                    yield return file;
                }
            }
        }
    }

    private static IEnumerable<string> GetSearchRoots(string directory)
    {
        string baseDirectory = Path.GetFullPath(AppContext.BaseDirectory);
        yield return baseDirectory;

        string requestedDirectory = string.IsNullOrWhiteSpace(directory)
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(directory);

        if (!string.Equals(baseDirectory, requestedDirectory, StringComparison.OrdinalIgnoreCase))
        {
            yield return requestedDirectory;
        }
    }

    private static IEnumerable<string> EnumerateFiles(string directory, Regex regex, SearchOption searchOption)
    {
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        EnumerationOptions options = new()
        {
            RecurseSubdirectories = searchOption == SearchOption.AllDirectories,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
        };

        foreach (string file in Directory.EnumerateFiles(directory, "*", options))
        {
            if (regex.IsMatch(Path.GetFileName(file)))
            {
                yield return file;
            }
        }
    }
}
