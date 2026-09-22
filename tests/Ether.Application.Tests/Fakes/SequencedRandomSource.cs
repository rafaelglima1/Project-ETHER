using Ether.Domain.Combat;

namespace Ether.Application.Tests.Fakes;

/// <summary>Returns a scripted sequence of unit values (deterministic loot/crit).</summary>
internal sealed class SequencedRandomSource : IRandomSource
{
    private readonly Queue<double> _values;

    public SequencedRandomSource(params double[] values)
    {
        _values = new Queue<double>(values);
    }

    public double NextUnit() => _values.Count > 0 ? _values.Dequeue() : 0d;
}
