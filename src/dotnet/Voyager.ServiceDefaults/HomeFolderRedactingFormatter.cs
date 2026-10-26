using System.Globalization;
using System.Text.RegularExpressions;

using Serilog.Events;
using Serilog.Formatting;

namespace Voyager.ServiceDefaults;

/// <summary>
/// Writes log events like another formatter, with the home folder of the user replaced by "~". Paths in
/// messages and exceptions hold the name of the user's account, and a log file may end up attached to a
/// bug report in a public repository.
/// </summary>
internal sealed class HomeFolderRedactingFormatter : ITextFormatter
{
    // A pattern that backtracks too long must cost one log line, not the whole service.
    private static readonly TimeSpan _matchTimeout = TimeSpan.FromSeconds(1);

    private readonly ITextFormatter _inner;
    private readonly Regex _homeFolder;

    private HomeFolderRedactingFormatter(ITextFormatter inner, Regex homeFolder)
    {
        _inner = inner;
        _homeFolder = homeFolder;
    }

    /// <summary>Redacts the home folder of the current user in what <paramref name="inner"/> writes.</summary>
    public static ITextFormatter ForCurrentUser(ITextFormatter inner) =>
        ForFolder(inner, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>Redacts <paramref name="homeFolder"/> in what <paramref name="inner"/> writes.</summary>
    public static ITextFormatter ForFolder(ITextFormatter inner, string homeFolder)
    {
        // "" and "/" would turn every slash of the text into a "~".
        if (homeFolder.Length < 2)
        {
            return inner;
        }

        // Windows paths come with backslashes or slashes. The folder must end there: C:\Users\ann is not
        // the start of C:\Users\anna.
        var variants = new[] { homeFolder, homeFolder.Replace('\\', '/'), homeFolder.Replace('/', '\\') }
            .Distinct(StringComparer.Ordinal)
            .Select(Regex.Escape);
        var pattern = $@"(?:{string.Join('|', variants)})(?=[\\/]|$|[^\w.\-])";
        var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, _matchTimeout);
        return new HomeFolderRedactingFormatter(inner, regex);
    }

    /// <inheritdoc />
    public void Format(LogEvent logEvent, TextWriter output)
    {
        using var buffer = new StringWriter(CultureInfo.InvariantCulture);
        _inner.Format(logEvent, buffer);
        output.Write(_homeFolder.Replace(buffer.ToString(), "~"));
    }
}
