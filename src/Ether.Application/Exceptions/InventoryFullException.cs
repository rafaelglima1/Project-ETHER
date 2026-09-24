namespace Ether.Application.Exceptions;

/// <summary>
/// Raised when a character's inventory is full and cannot accept another stack.
/// Deterministic server result per canonical design; no mailbox or overflow storage.
/// </summary>
public sealed class InventoryFullException : Exception
{
    public InventoryFullException(int capacity)
        : base($"Inventory is full ({capacity} slots).")
    {
        Capacity = capacity;
    }

    public int Capacity { get; }
}
