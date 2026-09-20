using Ether.Domain.Common;

namespace Ether.Domain.Quests;

/// <summary>
/// Strongly-typed identifier for a quest definition.
/// </summary>
public readonly record struct QuestId
{
    public QuestId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("QuestId must not be empty.");
        }

        Value = value;
    }

    public Guid Value { get; }

    public static QuestId Empty => default;

    public bool IsEmpty => Value == Guid.Empty;

    public static QuestId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
