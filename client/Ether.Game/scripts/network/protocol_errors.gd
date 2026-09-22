class_name ProtocolErrors
extends RefCounted

## Maps server error codes to concise, non-technical user-facing messages.
## The raw code is still kept for the debug overlay.

const FALLBACK := "The server rejected that action."


static func user_message(code: String) -> String:
	if code == ProtocolMessages.CODE_INVALID_ENVELOPE:
		return "The server could not read the message."
	elif code == ProtocolMessages.CODE_UNSUPPORTED_VERSION:
		return "This client is not compatible with the server."
	elif code == ProtocolMessages.CODE_UNKNOWN_MESSAGE:
		return "The server does not support that action."
	elif code == ProtocolMessages.CODE_INVALID_PAYLOAD:
		return "That action was malformed."
	elif code == ProtocolMessages.CODE_MESSAGE_TOO_LARGE:
		return "The message was too large."
	elif code == ProtocolMessages.CODE_NOT_AUTHENTICATED:
		return "Your session is not authenticated."
	elif code == ProtocolMessages.CODE_ALREADY_AUTHENTICATED:
		return "This session is already authenticated."
	elif code == ProtocolMessages.CODE_INVALID_TOKEN:
		return "Your game session token was rejected."
	elif code == ProtocolMessages.CODE_TOKEN_EXPIRED:
		return "Your game session token expired."
	elif code == ProtocolMessages.CODE_WRONG_TOKEN_PURPOSE:
		return "That credential cannot open a game session."
	elif code == ProtocolMessages.CODE_NOT_AUTHORIZED:
		return "You are not allowed to do that."
	elif code == ProtocolMessages.CODE_NOT_IN_WORLD:
		return "Your character is not in the world."
	elif code == ProtocolMessages.CODE_ALREADY_IN_WORLD:
		return "Your character is already in the world."
	elif code == ProtocolMessages.CODE_INVALID_MAP:
		return "That map is unavailable."
	elif code == ProtocolMessages.CODE_OUT_OF_BOUNDS:
		return "You cannot move there."
	elif code == ProtocolMessages.CODE_TOO_FAR:
		return "That destination is too far away."
	elif code == ProtocolMessages.CODE_INVALID_STATE:
		return "That action is not available right now."
	elif code == ProtocolMessages.CODE_INVALID_SEQUENCE:
		return "The connection is out of sync."
	elif code == ProtocolMessages.CODE_RATE_LIMITED:
		return "You are moving too fast."
	elif code == ProtocolMessages.CODE_SERVER_BUSY:
		return "The server is busy. Try again."
	elif code == ProtocolMessages.CODE_INTERNAL_ERROR:
		return "The server hit an unexpected error."
	elif code == ProtocolMessages.CODE_ABILITY_NOT_FOUND:
		return "That ability is not available."
	elif code == ProtocolMessages.CODE_TARGET_NOT_FOUND:
		return "That target no longer exists."
	elif code == ProtocolMessages.CODE_TARGET_DEAD:
		return "That target is already dead."
	elif code == ProtocolMessages.CODE_ATTACKER_DEAD:
		return "You cannot attack while dead."
	elif code == ProtocolMessages.CODE_OUT_OF_RANGE:
		return "That target is out of range."
	elif code == ProtocolMessages.CODE_COOLDOWN_ACTIVE:
		return "That ability is still on cooldown."
	elif code == ProtocolMessages.CODE_SELF_TARGET:
		return "You cannot target yourself."
	return FALLBACK
