using System.Diagnostics;
using System.Reflection;

namespace MacKeys;

/// <summary>Publisher details and links shown on the Help and Feedback tabs.</summary>
static class Brand
{
    public const string Publisher = "digitaltyp";
    public const string XHandle = "@digitaltyp";
    public const string XUrl = "https://x.com/digitaltyp";
    public const string RepoUrl = "https://github.com/digitaltyp0/MacKeys";
    public const string IssuesUrl = RepoUrl + "/issues";

    public static string Version =>
        Assembly.GetExecutingAssembly().GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "1.0.0";

    public static void Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Couldn't open {url}\n\n{ex.Message}", "MacKeys", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
