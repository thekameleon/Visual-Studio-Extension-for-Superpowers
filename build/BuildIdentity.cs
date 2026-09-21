using System;
using System.Reflection;

namespace TheKameleon.Superpowers;

internal static class BuildIdentity
{
    public static string Version => typeof(BuildIdentity).Assembly
        .GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "Unknown";

    public static string AssemblyPath => typeof(BuildIdentity).Assembly.Location;

    public static string Describe() =>
        $"Loaded build: {Version}{Environment.NewLine}Loaded DLL: {AssemblyPath}";
}
