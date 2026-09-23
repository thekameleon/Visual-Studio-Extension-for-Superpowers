using System.Security.Cryptography;

namespace TheKameleon.Superpowers.Skills.Install;

public static class ContentHash
{
    public static string Of(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    public static string OfFile(string path) => Of(File.ReadAllBytes(path));
}
