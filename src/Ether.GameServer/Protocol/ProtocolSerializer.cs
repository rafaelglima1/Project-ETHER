using System.Text.Json;
using System.Text.Json.Serialization;

using Ether.Contracts.Realtime;

namespace Ether.GameServer.Protocol;

/// <summary>
/// Serializes/deserializes the canonical realtime envelope. Kept independent of
/// the WebSocket transport so the contract is testable on its own.
/// </summary>
public sealed class ProtocolSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    public string Serialize(ProtocolEnvelope envelope) => JsonSerializer.Serialize(envelope, Options);

    public ProtocolEnvelope Event(string name, object? payload, Guid? requestId, long sequence) =>
        Build(ProtocolMessageTypes.Event, name, payload, requestId, sequence);

    public ProtocolEnvelope Error(string name, string code, string message, Guid? requestId, long sequence) =>
        Build(
            ProtocolMessageTypes.Error,
            name,
            new ProtocolErrorPayload(code, message),
            requestId,
            sequence);

    public ProtocolEnvelope Build(string type, string name, object? payload, Guid? requestId, long sequence) =>
        new()
        {
            Version = ProtocolMessageTypes.CurrentVersion,
            Type = type,
            Name = name,
            RequestId = requestId,
            Sequence = sequence,
            Payload = payload is null ? null : JsonSerializer.SerializeToElement(payload, Options),
        };

    /// <summary>Deserializes an envelope, returning null and a reason when invalid.</summary>
    public ProtocolEnvelope? TryDeserialize(string json, out string? failureReason)
    {
        failureReason = null;

        ProtocolEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<ProtocolEnvelope>(json, Options);
        }
        catch (JsonException)
        {
            failureReason = ProtocolErrorCodes.InvalidEnvelope;
            return null;
        }

        if (envelope is null || string.IsNullOrWhiteSpace(envelope.Name))
        {
            failureReason = ProtocolErrorCodes.InvalidEnvelope;
            return null;
        }

        if (envelope.Version != ProtocolMessageTypes.CurrentVersion)
        {
            failureReason = ProtocolErrorCodes.UnsupportedVersion;
            return null;
        }

        return envelope;
    }

    public T? DeserializePayload<T>(JsonElement? payload)
    {
        if (payload is null)
        {
            return default;
        }

        var element = payload.Value;
        if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return default;
        }

        try
        {
            return element.Deserialize<T>(Options);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
