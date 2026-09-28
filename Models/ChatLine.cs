using ProjectM.Network;
using System;

namespace ChatPlus.Models;

internal struct ChatLine
{
    public ChatChannel Channel { get; private set; }
    public string Sender { get; private set; } = string.Empty;
    public string Text { get; private set; } = string.Empty;
    public DateTime ReceivedUtc { get; private set; }
    public NetworkId SenderId { get; private set; }
    public NetworkId Partner { get; private set; }
    public string PartnerName { get; private set; } = string.Empty;

    public ChatLine(ChatChannel channel, string sender, string text, NetworkId senderId)
    {
        Channel = channel;
        Sender = sender;
        Text = text;
        ReceivedUtc = DateTime.UtcNow;
        SenderId = senderId;
        Partner = default;
        PartnerName = string.Empty;
    }

    public ChatLine(ChatChannel channel, string sender, string text, NetworkId senderId, NetworkId partner, string partnerName)
    {
        Channel = channel;
        Sender = sender;
        Text = text;
        ReceivedUtc = DateTime.UtcNow;
        SenderId = senderId;
        Partner = partner;
        PartnerName = partnerName ?? string.Empty;
    }

    internal static ChatLine Restore(ChatChannel channel, string sender, string text, DateTime receivedUtc, NetworkId senderId)
    {
        return new ChatLine
        {
            Channel = channel,
            Sender = sender,
            Text = text,
            ReceivedUtc = receivedUtc,
            SenderId = senderId,
            Partner = default,
            PartnerName = string.Empty,
        };
    }
}