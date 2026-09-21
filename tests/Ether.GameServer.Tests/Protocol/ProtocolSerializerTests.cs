using System.Text.Json;

using Ether.Contracts.Realtime;
using Ether.GameServer.Protocol;

namespace Ether.GameServer.Tests.Protocol;

public sealed class ProtocolSerializerTests
{
    private readonly ProtocolSerializer _serializer = new();

    [Fact]
    public void Command_envelope_round_trips()
    {
        var envelope = _serializer.Build(
            ProtocolMessageTypes.Command,
            ProtocolMessageNames.MovementMove,
            new MovementMoveCommandPayload(11, 12),
            Guid.NewGuid(),
            42);

        var json = _serializer.Serialize(envelope);
        var parsed = _serializer.TryDeserialize(json, out var reason);

        Assert.Null(reason);
        Assert.NotNull(parsed);
        Assert.Equal(ProtocolMessageTypes.Command, parsed!.Type);
        Assert.Equal(ProtocolMessageNames.MovementMove, parsed.Name);
        Assert.Equal(42, parsed.Sequence);
        Assert.Equal(11, _serializer.DeserializePayload<MovementMoveCommandPayload>(parsed.Payload)!.X);
    }

    [Fact]
    public void Event_envelope_uses_event_type_and_keeps_request_id()
    {
        var requestId = Guid.NewGuid();
        var envelope = _serializer.Event(ProtocolMessageNames.WorldSnapshot, new { mapId = 1 }, requestId, 7);

        Assert.Equal(ProtocolMessageTypes.Event, envelope.Type);
        Assert.Equal(requestId, envelope.RequestId);

        var json = _serializer.Serialize(envelope);
        Assert.Contains("\"type\":\"event\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Error_envelope_carries_code_and_message()
    {
        var envelope = _serializer.Error(
            ProtocolMessageNames.MovementRejected,
            ProtocolErrorCodes.OutOfBounds,
            "Destination is outside the map.",
            Guid.NewGuid(),
            9);

        Assert.Equal(ProtocolMessageTypes.Error, envelope.Type);

        var payload = _serializer.DeserializePayload<ProtocolErrorPayload>(envelope.Payload)!;
        Assert.Equal(ProtocolErrorCodes.OutOfBounds, payload.Code);
        Assert.Equal("Destination is outside the map.", payload.Message);
    }

    [Fact]
    public void Serialization_uses_camel_case_json()
    {
        var json = _serializer.Serialize(_serializer.Event("test", null, Guid.NewGuid(), 1));

        Assert.Contains("\"requestId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"sequence\"", json, StringComparison.Ordinal);
        Assert.Contains("\"type\"", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"name\":\"\"}")]
    public void Malformed_or_nameless_envelope_is_rejected(string json)
    {
        var parsed = _serializer.TryDeserialize(json, out var reason);

        Assert.Null(parsed);
        Assert.Equal(ProtocolErrorCodes.InvalidEnvelope, reason);
    }

    [Fact]
    public void Unsupported_version_is_rejected()
    {
        var json = JsonSerializer.Serialize(new
        {
            version = 99,
            type = "command",
            name = "movement.move",
            sequence = 1,
        });

        var parsed = _serializer.TryDeserialize(json, out var reason);

        Assert.Null(parsed);
        Assert.Equal(ProtocolErrorCodes.UnsupportedVersion, reason);
    }

    [Fact]
    public void Invalid_payload_returns_default_without_throwing()
    {
        var envelope = _serializer.Build(
            ProtocolMessageTypes.Command,
            ProtocolMessageNames.MovementMove,
            new { unexpected = "shape" },
            null,
            1);

        var payload = _serializer.DeserializePayload<MovementMoveCommandPayload>(envelope.Payload);

        Assert.Equal(0, payload!.X);
    }
}
