using ChatPlus.UI;
using ProjectM;
using ProjectM.UI;
using Unity.Entities;
using UnityEngine;

namespace ChatPlus.Services;

internal static class InputLockService
{
    static InputActionSystem? _ias;
    static World? _world;
    static ClientChatSystem.ChatFocusedInputContext? _contextObj;
    static IInputContext? _context;
    static bool _registered;
    static double _retryAt;

    public static void CaptureFrom(GameplayInputSystem gameplayInput)
    {
        if (_ias != null || gameplayInput == null) return;

        try
        {
            InputActionSystem ias = gameplayInput._InputActionSystem;
            if (ias == null) return;
            _ias = ias;
            _world = ias.World;
        }
        catch (Exception ex)
        {
            Core.Log.LogDebug($"[ChatPlus] InputActionSystem capture failed: {ex.Message}");
        }
    }

    public static void Tick()
    {
        try
        {
            InputActionSystem? ias = _ias;
            if (ias == null) return;
            if (Time.realtimeSinceStartupAsDouble < _retryAt) return;

            World? world = _world;
            bool worldAlive = world != null && SafeIsCreated(world);

            // The game's own chat input context consumes the whole keybind layer
            // (number slots, Ctrl/Alt, menus), which is what makes the normal chat
            // box airtight. Register it while typing OR while the Settings/Help page
            // is open so typed field input (numbers, modifiers) can never reach the
            // game. Removed as soon as both are inactive. The HUD canvas is required
            // so a stale typing flag cannot keep the keyboard locked after the window
            // is gone.
            bool desired = Core.HasInitialized
                && worldAlive
                && Core.HasCanvas
                && ChatWindow.IsModdedActive
                && (ChatWindow.IsTyping || ChatWindow.IsSettingsOpen);
            if (desired == _registered) return;

            if (!worldAlive)
            {
                OnWorldTeardown();
                return;
            }

            if (desired)
            {
                _contextObj ??= new ClientChatSystem.ChatFocusedInputContext();
                _context ??= _contextObj.Cast<IInputContext>();
                if (!ias.IsContextRegistered(_context))
                    ias.AddInputContext(_context, world, (int)InputContextOrder.ChatInput + 1);
                _registered = true;
            }
            else
            {
                if (_context != null && ias.IsContextRegistered(_context))
                    ias.RemoveInputContext(_context);
                _registered = false;
            }
        }
        catch (Exception ex)
        {
            Core.Log.LogDebug($"[ChatPlus] Input lock reconcile failed: {ex.Message}");
            _retryAt = Time.realtimeSinceStartupAsDouble + 0.5;
        }
    }

    // Best-effort removal while the world is still alive, then a full teardown. Used
    // when the local player is gone but the session is not over, so a disconnect that
    // leaves the client world running cannot keep the keyboard locked.
    public static void ReleaseNow()
    {
        try
        {
            InputActionSystem? ias = _ias;
            World? world = _world;
            if (ias != null && world != null && SafeIsCreated(world) &&
                _context != null && ias.IsContextRegistered(_context))
            {
                ias.RemoveInputContext(_context);
            }
        }
        catch (Exception ex)
        {
            Core.Log.LogDebug($"[ChatPlus] Input context release failed: {ex.Message}");
        }

        OnWorldTeardown();
    }

    static bool SafeIsCreated(World world)
    {
        try { return world.IsCreated; }
        catch { return false; }
    }

    public static void OnWorldTeardown()
    {
        _ias = null;
        _world = null;
        _contextObj = null;
        _context = null;
        _registered = false;
        _retryAt = 0;
    }
}
