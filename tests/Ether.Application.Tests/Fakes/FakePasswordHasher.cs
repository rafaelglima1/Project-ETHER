using Ether.Application.Abstractions;

namespace Ether.Application.Tests.Fakes;

/// <summary>Deterministic password hasher for application-layer tests.</summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    public const string Prefix = "hashed:";

    public string Hash(string password) => Prefix + password;

    public bool Verify(string password, string passwordHash) =>
        string.Equals(passwordHash, Prefix + password, StringComparison.Ordinal);
}
