namespace Voyager.Architecture.Tests;

/// <summary>
/// Locates the repository files that the architecture tests inspect.
/// </summary>
internal static class RepositoryLayout
{
    private const string SOLUTION_FILE = "Voyager.slnx";

    /// <summary>
    /// Walks up from the test output folder until it finds the solution file.
    /// </summary>
    public static DirectoryInfo FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, SOLUTION_FILE)))
        {
            directory = directory.Parent;
        }

        return directory
            ?? throw new InvalidOperationException($"No {SOLUTION_FILE} found above {AppContext.BaseDirectory}.");
    }

    /// <summary>
    /// Production projects: everything under src/dotnet and the .NET part of each plugin.
    /// </summary>
    public static IEnumerable<FileInfo> FindProductionProjects()
    {
        var root = FindRoot();
        var sources = new[]
        {
            Path.Combine(root.FullName, "src", "dotnet"),
            Path.Combine(root.FullName, "plugins"),
        };

        return sources
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.csproj", SearchOption.AllDirectories))
            .Where(file => !IsTestProject(file))
            .Select(file => new FileInfo(file));
    }

    private static bool IsTestProject(string projectPath) =>
        Path.GetFileNameWithoutExtension(projectPath).EndsWith(".Tests", StringComparison.Ordinal);
}
