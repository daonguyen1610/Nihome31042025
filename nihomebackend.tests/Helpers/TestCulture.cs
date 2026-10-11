using System.Globalization;
using System.Runtime.CompilerServices;

namespace nihomebackend.tests.Helpers;

/// <summary>
/// Localized rendering (PDF exports, validation limits, number formatting)
/// follows the ambient culture, so the same test can pass on CI and fail on a
/// developer machine whose locale differs. Pin the test process to invariant
/// culture so results match CI and the deployed Linux container.
/// </summary>
internal static class TestCulture
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
    }
}
