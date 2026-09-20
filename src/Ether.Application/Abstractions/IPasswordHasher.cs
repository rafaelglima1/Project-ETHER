namespace Ether.Application.Abstractions;

/// <summary>Password hashing abstraction. Implemented by the infrastructure layer.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
