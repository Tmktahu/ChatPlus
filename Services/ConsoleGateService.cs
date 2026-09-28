using Engine.Console;
using Engine.Console.PublicInterface;
using UnityEngine;

namespace ChatPlus.Services;

internal static class ConsoleGateService
{
    static ConsoleUIHandler? _ui;
    static bool _cached;
    static float _lastCheck;

    public static bool IsConsoleOpen
    {
        get
        {
            try
            {
                // Cache the handler; refresh check every 0.1s (the console may be created late).
                if (Time.realtimeSinceStartupAsDouble - _lastCheck > 0.1)
                {
                    _lastCheck = (float)Time.realtimeSinceStartupAsDouble;
                    try { _ui = Engine.Console.StunConsole.UI; }
                    catch { _ui = null; }
                }

                ConsoleUIHandler? ui = _ui;
                if (ui == null) return false;

                // IsVisible is the game's own open-state flag for the console overlay.
                try { _cached = ui.IsVisible; }
                catch { return false; }
                return _cached;
            }
            catch
            {
                return false;
            }
        }
    }

    public static void OnWorldTeardown()
    {
        _ui = null;
        _cached = false;
        _lastCheck = 0;
    }
}