using BepInEx;
using ChatPlus.Models;
using System;
using System.IO;
using System.Text.Json;
using UnityEngine;

namespace ChatPlus.Services;

internal static class SettingsService
{
    const int SaveDebounceSec = 1;

    static readonly string SettingsDir = Path.Combine(Paths.ConfigPath, "ChatPlus");
    static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    static StoredSettings _settings = new();
    static bool _loaded;
    static bool _dirty;
    static DateTime _lastSave = DateTime.MinValue;

    public static float LogBackgroundOpacity
    {
        get => _settings.LogBackgroundOpacity;
        set
        {
            float clamped = Math.Clamp(value, 0f, 1f);
            if (Math.Abs(_settings.LogBackgroundOpacity - clamped) < 0.0001f) return;
            _settings.LogBackgroundOpacity = clamped;
            _dirty = true;
        }
    }

    public static float InputBackgroundOpacity
    {
        get => _settings.InputBackgroundOpacity;
        set
        {
            float clamped = Math.Clamp(value, 0f, 1f);
            if (Math.Abs(_settings.InputBackgroundOpacity - clamped) < 0.0001f) return;
            _settings.InputBackgroundOpacity = clamped;
            _dirty = true;
        }
    }

    public static float WindowX
    {
        get => _settings.WindowX ?? 16f;
        set
        {
            if (_settings.WindowX.HasValue && Math.Abs(_settings.WindowX.Value - value) < 0.01f) return;
            _settings.WindowX = value;
            _dirty = true;
        }
    }

    public static float WindowY
    {
        get => _settings.WindowY ?? 16f;
        set
        {
            if (_settings.WindowY.HasValue && Math.Abs(_settings.WindowY.Value - value) < 0.01f) return;
            _settings.WindowY = value;
            _dirty = true;
        }
    }

    public static float WindowWidth
    {
        get => _settings.WindowWidth ?? 460f;
        set
        {
            float clamped = Math.Clamp(value, 300f, 2000f);
            if (_settings.WindowWidth.HasValue && Math.Abs(_settings.WindowWidth.Value - clamped) < 0.01f) return;
            _settings.WindowWidth = clamped;
            _dirty = true;
        }
    }

    public static float WindowHeight
    {
        get => _settings.WindowHeight ?? 340f;
        set
        {
            float clamped = Math.Clamp(value, 200f, 1200f);
            if (_settings.WindowHeight.HasValue && Math.Abs(_settings.WindowHeight.Value - clamped) < 0.01f) return;
            _settings.WindowHeight = clamped;
            _dirty = true;
        }
    }

    public static float ClanListYOffset
    {
        get => _settings.ClanListYOffset ?? 0f;
        set
        {
            float clamped = Math.Clamp(value, -500f, 500f);
            if (_settings.ClanListYOffset.HasValue && Math.Abs(_settings.ClanListYOffset.Value - clamped) < 0.01f) return;
            _settings.ClanListYOffset = clamped;
            _dirty = true;
        }
    }

    public static bool MirrorLayout
    {
        get => _settings.MirrorLayout ?? false;
        set
        {
            if ((_settings.MirrorLayout ?? false) == value) return;
            _settings.MirrorLayout = value;
            _dirty = true;
        }
    }

    public static bool ShowTimestamp
    {
        get => _settings.ShowTimestamp ?? true;
        set
        {
            if ((_settings.ShowTimestamp ?? true) == value) return;
            _settings.ShowTimestamp = value;
            _dirty = true;
        }
    }

    public static bool ShowChannelIndicator
    {
        get => _settings.ShowChannelIndicator ?? false;
        set
        {
            if ((_settings.ShowChannelIndicator ?? false) == value) return;
            _settings.ShowChannelIndicator = value;
            _dirty = true;
        }
    }

    // Zoom capture: while the chat UI owns the wheel, the mouse wheel must not reach
    // the camera. Master kill-switch (default on), read every frame.
    public static bool ZoomCaptureEnabled
    {
        get => _settings.ZoomCaptureEnabled ?? true;
        set
        {
            if ((_settings.ZoomCaptureEnabled ?? true) == value) return;
            _settings.ZoomCaptureEnabled = value;
            _dirty = true;
        }
    }

    public static bool TryGetChannelHex(ChatChannel channel, out string hex)
    {
        hex = channel switch
        {
            ChatChannel.Global => _settings.ChannelGlobalHex ?? "5993FF",
            ChatChannel.Local => _settings.ChannelLocalHex ?? "FFFFFF",
            ChatChannel.Clan => _settings.ChannelClanHex ?? "59D973",
            ChatChannel.Whisper => _settings.ChannelWhisperHex ?? "AE7CEB",
            ChatChannel.System => _settings.ChannelSystemHex ?? "FF9933",
            _ => "F2EBDB",
        };
        return true;
    }

    public static void SetChannelHex(ChatChannel channel, string hex)
    {
        if (string.IsNullOrEmpty(hex)) return;
        switch (channel)
        {
            case ChatChannel.Global: _settings.ChannelGlobalHex = hex; break;
            case ChatChannel.Local: _settings.ChannelLocalHex = hex; break;
            case ChatChannel.Clan: _settings.ChannelClanHex = hex; break;
            case ChatChannel.Whisper: _settings.ChannelWhisperHex = hex; break;
            case ChatChannel.System: _settings.ChannelSystemHex = hex; break;
            default: return;
        }
        _dirty = true;
    }

    public static bool TryGetChannelLabelHex(ChatChannel channel, out string hex)
    {
        hex = channel switch
        {
            ChatChannel.Global => _settings.ChannelGlobalLabelHex ?? "5993FF",
            ChatChannel.Local => _settings.ChannelLocalLabelHex ?? "FFFFFF",
            ChatChannel.Clan => _settings.ChannelClanLabelHex ?? "59D973",
            ChatChannel.Whisper => _settings.ChannelWhisperLabelHex ?? "AE7CEB",
            ChatChannel.System => _settings.ChannelSystemLabelHex ?? "FF9933",
            _ => "F2EBDB",
        };
        return true;
    }

    public static void SetChannelLabelHex(ChatChannel channel, string hex)
    {
        if (string.IsNullOrEmpty(hex)) return;
        switch (channel)
        {
            case ChatChannel.Global: _settings.ChannelGlobalLabelHex = hex; break;
            case ChatChannel.Local: _settings.ChannelLocalLabelHex = hex; break;
            case ChatChannel.Clan: _settings.ChannelClanLabelHex = hex; break;
            case ChatChannel.Whisper: _settings.ChannelWhisperLabelHex = hex; break;
            case ChatChannel.System: _settings.ChannelSystemLabelHex = hex; break;
            default: return;
        }
        _dirty = true;
    }

    public static bool TryGetSelfHex(out string nameHex, out string messageHex)
    {
        nameHex = string.IsNullOrEmpty(_settings.SelfNameHex) ? "F2EBDB" : _settings.SelfNameHex!;
        messageHex = string.IsNullOrEmpty(_settings.SelfMessageHex) ? "F2EBDB" : _settings.SelfMessageHex!;
        return true;
    }

    public static void SetSelfHex(string nameHex, string messageHex)
    {
        _settings.SelfNameHex = nameHex;
        _settings.SelfMessageHex = messageHex;
        _dirty = true;
    }

    public static bool TryGetAdminHex(out string nameHex, out string messageHex)
    {
        nameHex = string.IsNullOrEmpty(_settings.AdminNameHex) ? "FFD24A" : _settings.AdminNameHex!;
        messageHex = string.IsNullOrEmpty(_settings.AdminMessageHex) ? "F2EBDB" : _settings.AdminMessageHex!;
        return true;
    }

    public static void SetAdminHex(string nameHex, string messageHex)
    {
        _settings.AdminNameHex = nameHex;
        _settings.AdminMessageHex = messageHex;
        _dirty = true;
    }

    public static bool IsColorScopeEnabled(ChatColorGroup group, ChatChannel channel)
    {
        string key = group + ":" + channel;
        if (_settings.ColorChannelScopes != null &&
            _settings.ColorChannelScopes.TryGetValue(key, out bool value))
        {
            return value;
        }
        return true;
    }

    public static void SetColorScope(ChatColorGroup group, ChatChannel channel, bool value)
    {
        _settings.ColorChannelScopes ??= new System.Collections.Generic.Dictionary<string, bool>();
        _settings.ColorChannelScopes[group + ":" + channel] = value;
        _dirty = true;
    }

    public static System.Collections.Generic.List<HotkeyEntry> Hotkeys => _settings.Hotkeys;

    public static void MarkDirty()
    {
        _dirty = true;
    }

    public static void Initialize()
    {
        if (_loaded) return;
        _loaded = true;

        try
        {
            Load();
        }
        catch (Exception ex)
        {
            Core.Log.LogError($"[ChatPlus] Settings load failed: {ex}");
        }
    }

    public static void Tick()
    {
        if (!_dirty) return;
        if ((DateTime.UtcNow - _lastSave).TotalSeconds < SaveDebounceSec) return;
        Save();
    }

    public static void Flush()
    {
        if (_dirty) Save();
    }

    static void Load()
    {
        if (!File.Exists(SettingsFile)) return;

        string json = File.ReadAllText(SettingsFile);
        if (string.IsNullOrWhiteSpace(json)) return;

        StoredSettings? stored = JsonSerializer.Deserialize<StoredSettings>(json);
        if (stored != null)
        {
            stored.LogBackgroundOpacity = Math.Clamp(stored.LogBackgroundOpacity, 0f, 1f);
            stored.InputBackgroundOpacity = Math.Clamp(stored.InputBackgroundOpacity, 0f, 1f);
            _settings = stored;

            // Migrate legacy single-message hotkeys to the command-list model.
            foreach (var hk in _settings.Hotkeys)
            {
                if (hk.Commands == null) hk.Commands = new System.Collections.Generic.List<string>();
                if (hk.Commands.Count == 0 && !string.IsNullOrEmpty(hk.Message))
                {
                    hk.Commands.Add(hk.Message);
                    hk.Message = string.Empty;
                }
            }
        }
    }

    static void Save()
    {
        try
        {
            if (!Directory.Exists(SettingsDir)) Directory.CreateDirectory(SettingsDir);

            string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFile, json);

            _dirty = false;
            _lastSave = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            Core.Log.LogError($"[ChatPlus] Settings save failed: {ex}");
        }
    }

    class StoredSettings
    {
        public float LogBackgroundOpacity { get; set; } = 0.70f;
        public float InputBackgroundOpacity { get; set; } = 0.75f;
        public float? WindowX { get; set; }
        public float? WindowY { get; set; }
        public float? WindowWidth { get; set; }
        public float? WindowHeight { get; set; }
        public float? ClanListYOffset { get; set; }
        public bool? MirrorLayout { get; set; }
        public bool? ShowTimestamp { get; set; }
        public bool? ShowChannelIndicator { get; set; }
        public bool? ZoomCaptureEnabled { get; set; }
        public string? ChannelGlobalHex { get; set; }
        public string? ChannelLocalHex { get; set; }
        public string? ChannelClanHex { get; set; }
        public string? ChannelWhisperHex { get; set; }
        public string? ChannelSystemHex { get; set; }
        public string? ChannelGlobalLabelHex { get; set; }
        public string? ChannelLocalLabelHex { get; set; }
        public string? ChannelClanLabelHex { get; set; }
        public string? ChannelWhisperLabelHex { get; set; }
        public string? ChannelSystemLabelHex { get; set; }
        public string? SelfNameHex { get; set; }
        public string? SelfMessageHex { get; set; }
        public string? AdminNameHex { get; set; }
        public string? AdminMessageHex { get; set; }
        public System.Collections.Generic.Dictionary<string, bool> ColorChannelScopes { get; set; } = new();
        public System.Collections.Generic.List<HotkeyEntry> Hotkeys { get; set; } = new();
    }

    public class HotkeyEntry
    {
        public string Message { get; set; } = string.Empty;
        public string Key { get; set; } = "None";
        public string Modifiers { get; set; } = string.Empty;
        public bool? Enabled { get; set; }
        public System.Collections.Generic.List<string> Commands { get; set; } = new();
    }
}