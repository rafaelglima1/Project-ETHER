using Ether.Domain.Combat;

namespace Ether.Infrastructure.Random;

/// <summary>Server-side randomness source backed by <see cref="Random.Shared"/>.</summary>
public sealed class SharedRandomSource : IRandomSource
{
    public double NextUnit() => System.Random.Shared.NextDouble();
}
