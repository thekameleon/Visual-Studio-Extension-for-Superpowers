using System.Globalization;

namespace TheKameleon.Superpowers.Bridge.Contracts;

public static class BridgePipeNaming
{
    public const string PipeNamePrefix = "TheKameleon.Superpowers.Bridge";

    public static string GetPipeName(int hostProcessId)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0}.{1}", PipeNamePrefix, hostProcessId);
    }
}
