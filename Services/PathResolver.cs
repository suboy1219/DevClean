using System.IO;

namespace DevClean.Services;

public static class PathResolver
{
    public static IReadOnlyList<string> ExpandAll(IEnumerable<string> patterns)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string pattern in patterns)
        {
            foreach (string path in Expand(pattern))
            {
                if (seen.Add(path))
                {
                    result.Add(path);
                }
            }
        }

        return result;
    }

    public static IEnumerable<string> Expand(string pattern)
    {
        string expanded;
        try
        {
            expanded = Environment.ExpandEnvironmentVariables(pattern);
        }
        catch
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(expanded))
        {
            yield break;
        }

        if (!expanded.Contains('*') && !expanded.Contains('?'))
        {
            if (File.Exists(expanded) || Directory.Exists(expanded))
            {
                yield return expanded;
            }

            yield break;
        }

        string? root = Path.GetPathRoot(expanded);
        if (string.IsNullOrEmpty(root))
        {
            yield break;
        }

        string[] segments = expanded[root.Length..]
            .Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);

        var current = new List<string> { root };
        for (int i = 0; i < segments.Length; i++)
        {
            string segment = segments[i];
            bool last = i == segments.Length - 1;
            var next = new List<string>();

            foreach (string dir in current)
            {
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                if (segment.Contains('*') || segment.Contains('?'))
                {
                    try
                    {
                        foreach (string match in Directory.EnumerateDirectories(dir, segment))
                        {
                            next.Add(match);
                        }
                    }
                    catch
                    {
                        // Inaccessible folder: skip.
                    }

                    if (last)
                    {
                        try
                        {
                            foreach (string match in Directory.EnumerateFiles(dir, segment))
                            {
                                next.Add(match);
                            }
                        }
                        catch
                        {
                            // Inaccessible folder: skip.
                        }
                    }
                }
                else
                {
                    string candidate = Path.Combine(dir, segment);
                    if (last)
                    {
                        if (File.Exists(candidate) || Directory.Exists(candidate))
                        {
                            next.Add(candidate);
                        }
                    }
                    else if (Directory.Exists(candidate))
                    {
                        next.Add(candidate);
                    }
                }
            }

            current = next;
            if (current.Count == 0)
            {
                yield break;
            }
        }

        foreach (string path in current)
        {
            if (File.Exists(path) || Directory.Exists(path))
            {
                yield return path;
            }
        }
    }

    public static long GetSize(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return new FileInfo(path).Length;
            }

            if (!Directory.Exists(path))
            {
                return 0;
            }
        }
        catch
        {
            return 0;
        }

        long total = 0;
        var stack = new Stack<string>();
        stack.Push(path);

        while (stack.Count > 0)
        {
            string dir = stack.Pop();
            DirectoryInfo info;
            try
            {
                info = new DirectoryInfo(dir);
            }
            catch
            {
                continue;
            }

            try
            {
                foreach (FileInfo file in info.EnumerateFiles())
                {
                    try
                    {
                        total += file.Length;
                    }
                    catch
                    {
                        // File vanished or is locked: ignore.
                    }
                }
            }
            catch
            {
                // Directory inaccessible: skip its files.
            }

            try
            {
                foreach (DirectoryInfo sub in info.EnumerateDirectories())
                {
                    try
                    {
                        if ((sub.Attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            continue;
                        }
                    }
                    catch
                    {
                        continue;
                    }

                    stack.Push(sub.FullName);
                }
            }
            catch
            {
                // Directory inaccessible: skip its subdirectories.
            }
        }

        return total;
    }

    public static bool CommandExists(string fileName)
    {
        if (fileName.Contains('\\') || fileName.Contains('/'))
        {
            return File.Exists(fileName);
        }

        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        foreach (string entry in path.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string dir = entry.Trim().Trim('"');
                if (dir.Length == 0)
                {
                    continue;
                }

                if (File.Exists(Path.Combine(dir, fileName)))
                {
                    return true;
                }
            }
            catch
            {
                // Malformed PATH entry: ignore.
            }
        }

        return false;
    }
}
