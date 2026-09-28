using ChatPlus.Models;
using ChatPlus.Services;
using UnityEngine;

namespace ChatPlus.UI;

internal static class Theme
{
    public static readonly Color PanelBg = new(0.039f, 0.039f, 0.051f, 0.72f);
    public static readonly Color PanelBgFaded = new(0.039f, 0.039f, 0.051f, 0.35f);
    public static readonly Color SettingsBg = new(0.05f, 0.04f, 0.07f, 0.96f);
    public static readonly Color SettingsBgWarm = new(0.039f, 0.039f, 0.051f, 0.96f);
    public static readonly Color FieldBg = new(0.039f, 0.039f, 0.051f, 1f);
    public static readonly Color Crimson = new(0.549f, 0.047f, 0.059f, 1f);
    public static readonly Color ButtonBg = new(0.13f, 0.125f, 0.14f, 1f);
    public static readonly Color DividerRed = new(0.549f, 0.047f, 0.059f, 0.8f);
    public static readonly Color SettingsFieldBg = new(0.145f, 0.16f, 0.19f, 1f);
    public static readonly Color RowStripe = new(0.184f, 0.184f, 0.184f, 0.35f);
    public static readonly Color Steel = new(0.220f, 0.255f, 0.302f, 1f);
    public static readonly Color HeaderBg = new(0.10f, 0.08f, 0.14f, 0.75f);
    public static readonly Color TabBg = new(0.07f, 0.06f, 0.10f, 0.72f);
    public static readonly Color TabBgFaded = new(0.07f, 0.06f, 0.10f, 0.35f);
    public static readonly Color TabActiveBg = new(0.22f, 0.16f, 0.08f, 0.85f);
    public static readonly Color TabActiveBgFaded = new(0.22f, 0.16f, 0.08f, 0.45f);
    public static readonly Color InputBg = new(0.039f, 0.039f, 0.051f, 0.75f);
    public static readonly Color InputBgFaded = new(0.039f, 0.039f, 0.051f, 0.35f);
    public static readonly Color ScrollBg = new(0.039f, 0.039f, 0.051f, 0.70f);
    public static readonly Color ScrollBgFaded = new(0.039f, 0.039f, 0.051f, 0.32f);

    public static readonly Color TextPrimary = new(0.95f, 0.92f, 0.86f, 1f);
    public static readonly Color TextMuted = new(0.78f, 0.72f, 0.66f, 0.65f);
    public static readonly Color TextAccent = new(0.95f, 0.80f, 0.45f, 1f);

    public static readonly Color ChannelGlobal = new(0.35f, 0.60f, 1.00f, 1f);
    public static readonly Color ChannelLocal = new(1.00f, 1.00f, 1.00f, 1f);
    public static readonly Color ChannelClan = new(0.35f, 0.85f, 0.45f, 1f);
    public static readonly Color ChannelWhisper = new(0.682f, 0.486f, 0.922f, 1f);
    public static readonly Color ChannelSystem = new(1.00f, 0.60f, 0.20f, 1f);

    public const float FontSize = 14f;
    public const float FontSizeSmall = 12f;

    public static Color ForChannel(ChatChannel channel) => channel switch
    {
        ChatChannel.Global => Resolve(Services.SettingsService.TryGetChannelHex(channel, out string g) ? g : "5993FF", ChannelGlobal),
        ChatChannel.Local => Resolve(Services.SettingsService.TryGetChannelHex(channel, out string l) ? l : "FFFFFF", ChannelLocal),
        ChatChannel.Clan => Resolve(Services.SettingsService.TryGetChannelHex(channel, out string c) ? c : "59D973", ChannelClan),
        ChatChannel.Whisper => Resolve(Services.SettingsService.TryGetChannelHex(channel, out string w) ? w : "AE7CEB", ChannelWhisper),
        ChatChannel.System => Resolve(Services.SettingsService.TryGetChannelHex(channel, out string s) ? s : "FF9933", ChannelSystem),
        _ => TextPrimary,
    };

    public static Color SelfNameColor =>
        Resolve(Services.SettingsService.TryGetSelfHex(out string n, out _) ? n : "F2EBDB", TextPrimary);

    public static Color SelfMessageColor =>
        Resolve(Services.SettingsService.TryGetSelfHex(out _, out string m) ? m : "F2EBDB", TextPrimary);

    public static Color AdminNameColor =>
        Resolve(Services.SettingsService.TryGetAdminHex(out string n, out _) ? n : "FFD24A", TextPrimary);

    public static Color AdminMessageColor =>
        Resolve(Services.SettingsService.TryGetAdminHex(out _, out string m) ? m : "F2EBDB", TextPrimary);

    public static Color ChannelLabelColor(ChatChannel channel) => channel switch
    {
        ChatChannel.Global => Resolve(Services.SettingsService.TryGetChannelLabelHex(channel, out string g) ? g : "5993FF", ChannelGlobal),
        ChatChannel.Local => Resolve(Services.SettingsService.TryGetChannelLabelHex(channel, out string l) ? l : "FFFFFF", ChannelLocal),
        ChatChannel.Clan => Resolve(Services.SettingsService.TryGetChannelLabelHex(channel, out string c) ? c : "59D973", ChannelClan),
        ChatChannel.Whisper => Resolve(Services.SettingsService.TryGetChannelLabelHex(channel, out string w) ? w : "AE7CEB", ChannelWhisper),
        ChatChannel.System => Resolve(Services.SettingsService.TryGetChannelLabelHex(channel, out string s) ? s : "FF9933", ChannelSystem),
        _ => TextPrimary,
    };

    static Color Resolve(string hex, Color fallback)
    {
        if (hex.Length > 1 && hex[0] == '#') hex = hex.Substring(1);
        if (hex.Length == 6 && ColorUtility.TryParseHtmlString("#" + hex, out Color color)) return color;
        return fallback;
    }
}
