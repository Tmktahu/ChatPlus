using BepInEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ChatPlus.Services;

internal static class InputHistoryService
{
    const int MaxEntries = 1000;

    static readonly string HistoryDir = Path.Combine(Paths.ConfigPath, "ChatPlus");
    static readonly string HistoryFile = Path.Combine(HistoryDir, "input_history.json");

    static readonly List<string> _entries = new();
    static bool _loaded;

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
            Core.Log.LogError($"[ChatPlus] Input history load failed: {ex}");
        }
    }

    public static IReadOnlyList<string> Entries => _entries;

    public static void Push(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (_entries.Count > 0 && _entries[^1] == text) return;

        _entries.Add(text);
        if (_entries.Count > MaxEntries)
        {
            _entries.RemoveRange(0, _entries.Count - MaxEntries);
        }

        Save();
    }

    public static void Flush()
    {
        Save();
    }

    static void Load()
    {
        if (!File.Exists(HistoryFile)) return;

        string json = File.ReadAllText(HistoryFile);
        if (string.IsNullOrWhiteSpace(json)) return;

        string[]? stored = JsonSerializer.Deserialize<string[]>(json);
        if (stored == null || stored.Length == 0) return;

        int start = Math.Max(0, stored.Length - MaxEntries);
        for (int i = start; i < stored.Length; i++)
        {
            if (!string.IsNullOrEmpty(stored[i])) _entries.Add(stored[i]);
        }

        Core.Log.LogInfo($"[ChatPlus] Restored {_entries.Count} sent lines to input recall.");
    }

    static void Save()
    {
        try
        {
            if (!Directory.Exists(HistoryDir)) Directory.CreateDirectory(HistoryDir);

            string json = JsonSerializer.Serialize(_entries.ToArray(), new JsonSerializerOptions { WriteIndented = false });
            File.WriteAllText(HistoryFile, json);
        }
        catch (Exception ex)
        {
            Core.Log.LogError($"[ChatPlus] Input history save failed: {ex}");
        }
    }
}
