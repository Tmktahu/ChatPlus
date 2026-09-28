using ChatPlus.Models;
using ProjectM.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;

namespace ChatPlus.Services;

internal static class ChatDataService
{
    const int MaxLines = 2000;
    const int MaxPartners = 32;
    const double DedupWindowMs = 1000;

    static readonly List<ChatLine> Lines = new();
    static readonly Queue<NetworkId> SenderIds = new();

    // Total lines removed from the FRONT of Lines. Front removal shifts every index,
    // so a consumer that stores AllLines indices (the chat window) must offset by the
    // delta or its window will point at the wrong lines after a trim.
    public static int TrimOffset { get; private set; }

    internal static bool WireTestLog = false;

    public struct WhisperPartner
    {
        public NetworkId Id;
        public string Name;
    }

    static readonly List<WhisperPartner> Partners = new();
    static readonly Dictionary<string, NetworkId> PlayerDirectory = new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<ChatLine> AllLines => Lines;
    public static IReadOnlyList<WhisperPartner> WhisperPartners => Partners;

    public static string PartnersSignature
    {
        get
        {
            try
            {
                return string.Join(";", Partners.Select(p => $"{p.Id.GetHashCode()}:{p.Name}"));
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    public static event Action<ChatLine>? LineCaptured;

    public static bool IsSenderBearing(ServerChatMessageType type) => type switch
    {
        ServerChatMessageType.Global
            or ServerChatMessageType.Local
            or ServerChatMessageType.Region
            or ServerChatMessageType.Team
            or ServerChatMessageType.WhisperFrom => true,
        _ => false,
    };

    public static ChatChannel MapChannel(ServerChatMessageType type) => type switch
    {
        ServerChatMessageType.Global => ChatChannel.Global,
        ServerChatMessageType.Local or ServerChatMessageType.Region => ChatChannel.Local,
        ServerChatMessageType.Team => ChatChannel.Clan,
        ServerChatMessageType.WhisperFrom or ServerChatMessageType.WhisperTo => ChatChannel.Whisper,
        ServerChatMessageType.System or ServerChatMessageType.Lore => ChatChannel.System,
        _ => ChatChannel.Other,
    };

    public static void RegisterWhisperPartner(NetworkId id, string name)
    {
        if (id == default) return;
        name = string.IsNullOrEmpty(name) ? "?" : name;

        for (int i = 0; i < Partners.Count; i++)
        {
            if (Partners[i].Id == id)
            {
                Partners[i] = new WhisperPartner { Id = id, Name = name };
                return;
            }
        }

        Partners.Insert(0, new WhisperPartner { Id = id, Name = name });
        if (Partners.Count > MaxPartners) Partners.RemoveRange(MaxPartners, Partners.Count - MaxPartners);
    }

    public static void EnqueueSenderId(NetworkId id)
    {
        SenderIds.Enqueue(id);
    }

    public static void CaptureFormatted(ServerChatMessageType messageType, string userName, string text)
    {
        string sender = string.IsNullOrEmpty(userName) || userName == "?"
            ? string.Empty
            : userName;
        text ??= string.Empty;
        text = NormalizeNewlines(text);

        if (messageType is ServerChatMessageType.System or ServerChatMessageType.Lore)
        {
            sender = string.Empty;
        }

        NetworkId senderId = default;
        if (IsSenderBearing(messageType) && SenderIds.Count > 0)
        {
            senderId = SenderIds.Dequeue();
        }

        if (messageType == ServerChatMessageType.WhisperFrom)
        {
            RegisterWhisperPartner(senderId, sender);
        }

        if (IsSenderBearing(messageType) && senderId != default && !string.IsNullOrEmpty(sender))
        {
            RegisterPlayerName(sender, senderId);
        }

        AddLine(new ChatLine(MapChannel(messageType), sender, text, senderId));
    }

    public static void RegisterPlayerName(string name, NetworkId id)
    {
        if (string.IsNullOrEmpty(name) || name == "?" || id == default) return;
        try { PlayerDirectory[name] = id; }
        catch { }
    }

    public static bool TryResolvePlayer(string name, out NetworkId id)
    {
        id = default;
        if (string.IsNullOrWhiteSpace(name)) return false;
        try { return PlayerDirectory.TryGetValue(name.Trim(), out id) && id != default; }
        catch { id = default; return false; }
    }

    public static void AddLocalEcho(ChatChannel channel, string text)
    {
        AddLine(new ChatLine(channel, ResolveOwnName(), NormalizeNewlines(text), Core.LocalUser.GetNetworkId()));
    }

    public static void AddSystemEcho(string text)
    {
        AddLine(new ChatLine(ChatChannel.System, string.Empty, NormalizeNewlines(text), Core.LocalUser.GetNetworkId()));
    }

    public static void AddWhisperEcho(NetworkId partnerId, string partnerName, string text)
    {
        RegisterWhisperPartner(partnerId, partnerName);
        AddLine(new ChatLine(ChatChannel.Whisper, ResolveOwnName(), NormalizeNewlines(text), Core.LocalUser.GetNetworkId(), partnerId, partnerName));
    }

    static string ResolveOwnName()
    {
        try
        {
            Entity user = Core.LocalUser;
            if (user != Entity.Null && user.Exists() && user.Has<User>())
            {
                string name = user.Read<User>().CharacterName.ToString();
                if (!string.IsNullOrEmpty(name)) return name;
            }
        }
        catch
        {
        }

        return "You";
    }

    static void AddLine(ChatLine line)
    {
        if (Lines.Count > 0)
        {
            ChatLine last = Lines[^1];
            if (last.Channel == line.Channel &&
                last.Sender == line.Sender &&
                last.Text == line.Text &&
                (line.ReceivedUtc - last.ReceivedUtc).TotalMilliseconds < DedupWindowMs)
            {
                return;
            }
        }

        Lines.Add(line);
        if (Lines.Count > MaxLines)
        {
            int remove = Lines.Count - MaxLines;
            Lines.RemoveRange(0, remove);
            TrimOffset += remove;
        }

        if (WireTestLog)
        {
            Core.Log.LogInfo($"[ChatPlus][Wire] {line.Channel} | {(line.Sender.Length == 0 ? "<system>" : line.Sender)} | {line.Text}");
        }

        LineCaptured?.Invoke(line);
    }

    public static void Clear()
    {
        Lines.Clear();
        SenderIds.Clear();
        Partners.Clear();
        PlayerDirectory.Clear();
        TrimOffset = 0;
    }

    public static void SeedLine(ChatLine line)
    {
        line = ChatLine.Restore(line.Channel, line.Sender, NormalizeNewlines(line.Text),
            line.ReceivedUtc, line.SenderId);
        // Prepend so restored history always sorts BEFORE any live lines, even if a
        // live message was captured before seeding ran. Load iterates stored lines
        // newest -> oldest, so prepending each preserves ascending order overall.
        Lines.Insert(0, line);
        if (Lines.Count > MaxLines)
        {
            int remove = Lines.Count - MaxLines;
            Lines.RemoveRange(0, remove);
            TrimOffset += remove;
        }

        LineCaptured?.Invoke(line);
    }

    static string NormalizeNewlines(string text)
    {
        if (text == null) return string.Empty;
        if (text.Length == 0) return text;
        if (text.Contains("\r"))
        {
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        // Collapse runs of newlines into a single line break so a source that uses
        // two (CRLF pairs or an embedded blank line) does not render as a gap.
        if (text.IndexOf("\n\n") >= 0)
        {
            var sb = new System.Text.StringBuilder(text.Length);
            bool previousWasNewline = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\n')
                {
                    if (!previousWasNewline) sb.Append('\n');
                    previousWasNewline = true;
                }
                else
                {
                    sb.Append(c);
                    previousWasNewline = false;
                }
            }
            text = sb.ToString();
        }

        // Strip leading/trailing newlines so a trailing break does not stack with
        // the inter-line separator the log adds between messages.
        int start = 0;
        int end = text.Length - 1;
        while (start <= end && text[start] == '\n') start++;
        while (end >= start && text[end] == '\n') end--;
        return start <= end ? text.Substring(start, end - start + 1) : string.Empty;
    }
}