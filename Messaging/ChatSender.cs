using Il2CppInterop.Runtime;
using ProjectM;
using ProjectM.Network;
using ProjectM.UI;
using Unity.Entities;

namespace ChatPlus.Messaging;

internal static class ChatSender
{
    static EntityManager EntityManager => Core.EntityManager;

    const bool EnableLocalBubble = false;

    // The game stores chat text in a FixedString512Bytes (2-byte length + 510 UTF-8
    // bytes), and wraps outgoing text in <noparse>...</noparse> (+19 bytes) before the
    // wire conversion. 490 + 19 = 509 bytes, the safe ceiling under 510. The community
    // safe-send cap is 490 bytes, leaving headroom so it can never trip the buffer.
    public const int MaxChatBytes = 490;
    public const int ApproxMaxCharacters = 480;

    public static int Utf8ByteLength(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        int bytes = 0;
        for (int i = 0; i < text.Length; i++)
        {
            int c = text[i];
            if (c >= 0xD800 && c <= 0xDBFF && i + 1 < text.Length && text[i + 1] >= 0xDC00 && text[i + 1] <= 0xDFFF)
            {
                bytes += 4;
                i++;
            }
            else if (c >= 0x800)
            {
                bytes += 3;
            }
            else if (c >= 0x80)
            {
                bytes += 2;
            }
            else
            {
                bytes += 1;
            }
        }
        return bytes;
    }

    public static string ClampUtf8(string text, int maxBytes)
    {
        if (string.IsNullOrEmpty(text) || Utf8ByteLength(text) <= maxBytes) return text;
        int bytes = 0;
        int cut = 0;
        for (int i = 0; i < text.Length; i++)
        {
            int c = text[i];
            int width;
            if (c >= 0xD800 && c <= 0xDBFF && i + 1 < text.Length && text[i + 1] >= 0xDC00 && text[i + 1] <= 0xDFFF)
            {
                width = 4;
            }
            else if (c >= 0x800)
            {
                width = 3;
            }
            else if (c >= 0x80)
            {
                width = 2;
            }
            else
            {
                width = 1;
            }
            if (bytes + width > maxBytes) break;
            bytes += width;
            cut = i + 1;
            if (c >= 0xD800 && c <= 0xDBFF) cut++; // include the surrogate pair
        }
        return text.Substring(0, cut);
    }

    static ComponentType[]? _networkEventComponents;
    static ComponentType[] NetworkEventComponents => _networkEventComponents ??=
    [
        ComponentType.ReadOnly(Il2CppType.Of<FromCharacter>()),
        ComponentType.ReadOnly(Il2CppType.Of<NetworkEventType>()),
        ComponentType.ReadOnly(Il2CppType.Of<SendNetworkEventTag>()),
        ComponentType.ReadOnly(Il2CppType.Of<ChatMessageEvent>()),
    ];

    static readonly NetworkEventType WhisperEventType = new()
    {
        IsAdminEvent = false,
        EventId = NetworkEvents.EventId_ChatMessageEvent,
        IsDebugEvent = false,
    };

    static bool IsWorldReady
    {
        get
        {
            try
            {
                if (!Core.HasInitialized) return false;
                World w = Core.EntityManager.World;
                return w != null && w.IsCreated;
            }
            catch { return false; }
        }
    }

    public static bool CanSend
    {
        get
        {
            try
            {
                if (!IsWorldReady) return false;
                Entity user = Core.LocalUser;
                Entity character = Core.LocalCharacter;
                return user != Entity.Null && character != Entity.Null && user.Exists() && character.Exists();
            }
            catch { return false; }
        }
    }

    public static void SendChat(string text, ChatMessageType type, NetworkId target = default)
    {
        if (!CanSend || string.IsNullOrWhiteSpace(text)) return;
        if (!IsWorldReady) return;
        text = ClampUtf8(text, MaxChatBytes);

        try
        {
            NetworkId to = type == ChatMessageType.Whisper ? target : SafeGetNetworkId(Core.LocalUser);
            ChatHelper.SendChatMessageOfType(EntityManager, text, type, to);
            Core.Log.LogDebug($"[ChatPlus] Sent {type} to={to} text={text}");

            if (EnableLocalBubble)
            {
                try
                {
                    ServerChatMessageType serverType = type switch
                    {
                        ChatMessageType.Global => ServerChatMessageType.Global,
                        ChatMessageType.Team => ServerChatMessageType.Team,
                        ChatMessageType.System => ServerChatMessageType.System,
                        _ => ServerChatMessageType.Local,
                    };
                    NetworkId fromUser = SafeGetNetworkId(Core.LocalUser);
                    NetworkId fromChar = SafeGetNetworkId(Core.LocalCharacter);
                    Core.Log.LogInfo($"[ChatPlus] AddLocalMessage {serverType} fromUser={fromUser} fromChar={fromChar}");
                    ClientSystemChatUtils.AddLocalMessage(EntityManager, text, serverType, fromUser, default, fromChar);
                }
                catch (Exception ex) { Core.Log.LogDebug($"[ChatPlus] Local bubble skipped: {ex.Message}"); }
            }
        }
        catch (Exception ex)
        {
            Core.Log.LogError($"[ChatPlus] Send failed: {ex}");
        }
    }

    static NetworkId SafeGetNetworkId(Entity entity)
    {
        try
        {
            if (entity == Entity.Null || !entity.Exists()) return default;
            if (Core.EntityManager.TryGetComponentData<NetworkId>(entity, out NetworkId id)) return id;
            return default;
        }
        catch { return default; }
    }

    public static void SendWhisper(string text, NetworkId target)
    {
        if (!CanSend || string.IsNullOrWhiteSpace(text)) return;
        if (target == default) return;

        // Re-confirm the local entities immediately before building the network event.
        // If either is gone the FromCharacter payload would carry a dead entity.
        Entity user = Core.LocalUser;
        Entity character = Core.LocalCharacter;
        if (!user.Exists() || !character.Exists()) return;

        text = ClampUtf8(text, MaxChatBytes);

        try
        {
            Entity networkEntity = EntityManager.CreateEntity(NetworkEventComponents);
            EntityManager.SetComponentData(networkEntity, new FromCharacter
            {
                Character = character,
                User = user,
            });
            EntityManager.SetComponentData(networkEntity, WhisperEventType);
            EntityManager.SetComponentData(networkEntity, new ChatMessageEvent
            {
                MessageText = text,
                MessageType = ChatMessageType.Whisper,
                ReceiverEntity = target,
            });
            Core.Log.LogDebug($"[ChatPlus] Sent Whisper to={target} text={text}");
        }
        catch (Exception ex)
        {
            Core.Log.LogError($"[ChatPlus] Whisper send failed: {ex}");
        }
    }

    public static void SendCommand(string text)
    {
        SendChat(text, ChatMessageType.Local);
    }
}
