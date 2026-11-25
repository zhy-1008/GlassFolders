using System.Reflection;

[assembly: AssemblyVersion(VersionInfo.AssemblyNumber)]
[assembly: AssemblyFileVersion(VersionInfo.AssemblyNumber)]
[assembly: AssemblyInformationalVersion(VersionInfo.Number)]

internal static class VersionInfo
{
    internal const string Number = "1.0";
    internal const string AssemblyNumber = Number + ".0.0";
}
