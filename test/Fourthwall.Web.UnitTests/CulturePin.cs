using System.Globalization;

namespace Fourthwall.Web.UnitTests;

/// <summary>
/// Swaps <see cref="CultureInfo.CurrentCulture"/> for the lifetime of a test and restores it,
/// so a culture-sensitive test can never leak into the ones that run after it.
/// </summary>
internal sealed class CulturePin : IDisposable
{
    private readonly CultureInfo _original;

    public CulturePin(string name)
    {
        _original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(name);
    }

    public void Dispose() => CultureInfo.CurrentCulture = _original;
}
