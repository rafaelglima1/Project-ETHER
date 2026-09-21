using Ether.Domain.Accounts;
using Ether.Domain.Common;
using Ether.Domain.World;

namespace Ether.Domain.Characters;

/// <summary>
/// Character aggregate root (Blueprint v2.0 §10 / v5.0 §9).
/// M1 implements only the persistent foundation: identity, ownership, name,
/// class, lifecycle state, level/experience and position. Gameplay systems
/// (combat, inventory, quests, skills, ...) belong to later milestones.
/// </summary>
public sealed class Character
{
    public const int MinLevel = 1;
    public const int InitialLevel = 1;
    public const long InitialExperience = 0;
    public const int MaxNameLength = 24;

    private Character()
    {
        // Required by the persistence layer.
    }

    private Character(
        CharacterId id,
        AccountId accountId,
        string name,
        CharacterClass characterClass,
        WorldPosition position,
        DateTimeOffset nowUtc)
    {
        Id = id;
        AccountId = accountId;
        Name = name;
        Class = characterClass;
        State = CharacterState.Offline;
        Level = InitialLevel;
        Experience = InitialExperience;
        MapId = position.MapId;
        PositionX = position.X;
        PositionY = position.Y;
        CreatedAt = nowUtc;
        UpdatedAt = nowUtc;
    }

    public CharacterId Id { get; private set; }

    public AccountId AccountId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public CharacterClass Class { get; private set; }

    public CharacterState State { get; private set; }

    public int Level { get; private set; }

    public long Experience { get; private set; }

    public MapId MapId { get; private set; }

    public int PositionX { get; private set; }

    public int PositionY { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>The stored position, reconstructed from its persisted columns.</summary>
    public WorldPosition Position => new(MapId, PositionX, PositionY);

    /// <summary>Creates a new character in a valid initial state.</summary>
    public static Character Create(
        AccountId accountId,
        string name,
        CharacterClass characterClass,
        WorldPosition position,
        DateTimeOffset nowUtc)
    {
        if (accountId.IsEmpty)
        {
            throw new DomainException("Character must belong to an existing account.");
        }

        if (!Enum.IsDefined(characterClass))
        {
            throw new DomainException("Character class is invalid.");
        }

        return new Character(
            CharacterId.New(),
            accountId,
            NormalizeName(name),
            characterClass,
            position,
            nowUtc);
    }

    /// <summary>Changes the lifecycle state, rejecting invalid transitions.</summary>
    public void ChangeState(CharacterState next, DateTimeOffset nowUtc)
    {
        if (!Enum.IsDefined(next))
        {
            throw new DomainException("Character state is invalid.");
        }

        if (next == State)
        {
            return;
        }

        if (!IsTransitionAllowed(State, next))
        {
            throw new DomainException($"Character state cannot transition from {State} to {next}.");
        }

        State = next;
        UpdatedAt = nowUtc;
    }

    private static bool IsTransitionAllowed(CharacterState from, CharacterState to) =>
        (from, to) switch
        {
            (CharacterState.Offline, CharacterState.Loading) => true,
            (CharacterState.Loading, CharacterState.InWorld) => true,
            (CharacterState.Loading, CharacterState.Offline) => true,
            (CharacterState.InWorld, CharacterState.Combat) => true,
            (CharacterState.InWorld, CharacterState.DisconnectGrace) => true,
            (CharacterState.InWorld, CharacterState.Dead) => true,
            (CharacterState.InWorld, CharacterState.Offline) => true,
            (CharacterState.Combat, CharacterState.InWorld) => true,
            (CharacterState.Combat, CharacterState.Dead) => true,
            (CharacterState.Combat, CharacterState.DisconnectGrace) => true,
            (CharacterState.Dead, CharacterState.Respawning) => true,
            (CharacterState.Respawning, CharacterState.InWorld) => true,
            (CharacterState.DisconnectGrace, CharacterState.InWorld) => true,
            (CharacterState.DisconnectGrace, CharacterState.Offline) => true,
            _ => false,
        };

    /// <summary>
    /// Moves the character to a server-validated destination. The client only
    /// requests the destination; bounds and distance are enforced here.
    /// </summary>
    public void MoveTo(WorldPosition destination, int maxDistance, WorldMap map, DateTimeOffset nowUtc)
    {
        if (State is not (CharacterState.InWorld or CharacterState.Combat))
        {
            throw new CharacterNotInWorldException("Character must be in the world to move.");
        }

        if (!map.Contains(destination) || destination.MapId != MapId)
        {
            throw new MovementOutOfBoundsException("Destination is outside the current map.");
        }

        var distance = Math.Max(Math.Abs(destination.X - PositionX), Math.Abs(destination.Y - PositionY));
        if (distance > maxDistance)
        {
            throw new MovementTooFarException($"Movement exceeds the allowed distance of {maxDistance} tiles.");
        }

        PositionX = destination.X;
        PositionY = destination.Y;
        UpdatedAt = nowUtc;
    }

    /// <summary>
    /// Places the character into the world. The lifecycle always passes through
    /// <see cref="CharacterState.Loading"/> so a single state machine governs
    /// every transition.
    /// </summary>
    public void EnterWorld(DateTimeOffset nowUtc)
    {
        if (State == CharacterState.InWorld)
        {
            return;
        }

        if (State is not (CharacterState.Offline or CharacterState.Loading))
        {
            throw new DomainException($"Character cannot enter the world from state {State}.");
        }

        ChangeState(CharacterState.Loading, nowUtc);
        ChangeState(CharacterState.InWorld, nowUtc);
    }

    /// <summary>Marks the character as offline (leaving the world).</summary>
    public void LeaveWorld(DateTimeOffset nowUtc)
    {
        if (State == CharacterState.Offline)
        {
            return;
        }

        ChangeState(CharacterState.Offline, nowUtc);
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Character name must not be empty.");
        }

        var trimmed = name.Trim();

        if (trimmed.Length > MaxNameLength)
        {
            throw new DomainException($"Character name must be at most {MaxNameLength} characters.");
        }

        return trimmed;
    }
}
