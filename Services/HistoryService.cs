using ChatPlus.Models;
using ProjectM.Network;
using BepInEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ChatPlus.Services;

internal static class HistoryService
{
    const int MaxStoredLines = 1000;
    const int SaveIntervalSec = 30;
    const int SeedTailCount = 1000;

    static readonly string HistoryDir = Path.Combine(Paths.ConfigPath, "ChatPlus");
    static readonly string HistoryFile = Path.Combine(HistoryDir, "chat_history.json");

    static readonly List<ChatLine> _pending = new();
    static DateTime _lastSave = DateTime.MinValue;
    static bool _dirty;
    static bool _loaded;

    public static bool IsSeeding { get; private set; }

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
            Core.Log.LogError($"[ChatPlus] History load failed: {ex}");
        }
    }

    public static void RecordLine(ChatLine line)
    {
        _pending.Add(line);
        if (_pending.Count > MaxStoredLines)
        {
            _pending.RemoveRange(0, _pending.Count - MaxStoredLines);
        }
        _dirty = true;
    }

    public static void Tick()
    {
        if (!_dirty) return;
        if ((DateTime.UtcNow - _lastSave).TotalSeconds < SaveIntervalSec) return;
        Save();
    }

    public static void Flush()
    {
        if (_dirty) Save();
    }

    static void Load()
    {
        IsSeeding = true;
        try
        {
            if (!File.Exists(HistoryFile)) return;

            string json = File.ReadAllText(HistoryFile);
            if (string.IsNullOrWhiteSpace(json)) return;

            StoredLine[]? stored = JsonSerializer.Deserialize<StoredLine[]>(json);
            if (stored == null || stored.Length == 0) return;

            // Older builds wrote the restored block in reverse, so the file on disk can
            // be out of order. Parse, detect that, and sort ascending before seeding.
            var parsed = new List<ChatLine>(stored.Length);
            bool repaired = false;
            DateTime previous = DateTime.MinValue;
            foreach (StoredLine s in stored)
            {
                if (!Enum.TryParse<ChatChannel>(s.Channel, out ChatChannel channel)) channel = ChatChannel.Other;
                DateTime ts = DateTime.TryParse(s.Timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsedTs)
                    ? parsedTs
                    : DateTime.UtcNow;
                if (ts < previous) repaired = true;
                previous = ts;
                parsed.Add(ChatLine.Restore(channel, s.Sender ?? string.Empty, s.Text ?? string.Empty, ts, default));
            }

            // Stable sort keeps equal timestamps in file order.
            var ordered = parsed.OrderBy(l => l.ReceivedUtc).ToList();

            // Seed only a small recent tail into the live log so a large cross-session
            // history can't flood the chat window. SeedLine prepends, so iterate
            // newest -> oldest to keep ascending display order.
            int start = Math.Max(0, ordered.Count - SeedTailCount);
            for (int i = ordered.Count - 1; i >= start; i--)
            {
                ChatLine line = ordered[i];
                ChatDataService.SeedLine(ChatLine.Restore(line.Channel, line.Sender, line.Text, line.ReceivedUtc, default));
            }

            // Build the persisted store from the same ascending list. Seeded lines are
            // no longer recorded through LineCaptured, so this is the only source of the
            // restored history in _pending. New lines append after it.
            _pending.Clear();
            for (int i = start; i < ordered.Count; i++)
            {
                _pending.Add(ordered[i]);
            }
            if (_pending.Count > MaxStoredLines)
            {
                _pending.RemoveRange(0, _pending.Count - MaxStoredLines);
            }

            // Rewrite a file that was out of order, so the damage does not persist.
            if (repaired) _dirty = true;

            Core.Log.LogInfo($"[ChatPlus] Restored {ordered.Count - start} chat lines from history (repaired={repaired}).");
        }
        finally
        {
            IsSeeding = false;
        }
    }

    static void Save()
    {
        try
        {
            if (!Directory.Exists(HistoryDir)) Directory.CreateDirectory(HistoryDir);

            var stored = _pending
                .Select(l => new StoredLine
                {
                    Channel = l.Channel.ToString(),
                    Sender = l.Sender,
                    Text = l.Text,
                    Timestamp = l.ReceivedUtc.ToString("O"),
                })
                .ToArray();

            string json = JsonSerializer.Serialize(stored, new JsonSerializerOptions { WriteIndented = false });
            File.WriteAllText(HistoryFile, json);

            _dirty = false;
            _lastSave = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            Core.Log.LogError($"[ChatPlus] History save failed: {ex}");
        }
    }

    class StoredLine
    {
        public string Channel { get; set; } = string.Empty;
        public string Sender { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
    }
}
