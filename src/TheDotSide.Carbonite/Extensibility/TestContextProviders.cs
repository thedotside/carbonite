using System.Reflection;

namespace TheDotSide.Carbonite.Extensibility;

/// <summary>Finds the test framework adapter shipped with Carbonite, when the test project references it.</summary>
internal static class TestContextProviders
{
    internal const string TUnitProviderTypeName =
        "TheDotSide.Carbonite.TUnit.TUnitTestContextProvider, TheDotSide.Carbonite.TUnit";

    /// <summary>Returns the TUnit adapter when it is present, or <see langword="null"/> for the generic mode.</summary>
    internal static ITestContextProvider? Discover()
    {
        try
        {
            var type = Type.GetType(TUnitProviderTypeName, throwOnError: false);
            return type is null
                ? null
                : (ITestContextProvider?)Activator.CreateInstance(type, nonPublic: true);
        }
        catch (Exception exception)
            when (exception
                    is FileLoadException
                        or BadImageFormatException
                        or TypeLoadException
                        or MissingMemberException
                        or TargetInvocationException
                        or InvalidCastException
            )
        {
            // Snapshots still work in the generic mode; say why the adapter is not used.
            Console.Error.WriteLine(
                $"Carbonite: the TUnit adapter could not be loaded ({exception.Message}). Snapshots use the generic mode."
            );
            return null;
        }
    }
}
