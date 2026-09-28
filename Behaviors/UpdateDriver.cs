using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace ChatPlus.Behaviors;

public class UpdateDriver : MonoBehaviour
{
    public static readonly List<Action> Actions = new();

    // A signalled condition must hold for this long before the session resets, so the
    // load screen and a brief HUD rebuild cannot trip a reset.
    const float NoCanvasGraceSeconds = 1.5f;
    const float NoGameDataGraceSeconds = 1.5f;
    // Longer grace for a missing local player: a death or respawn removes and
    // recreates the local entities, and the load screen spawns late.
    const float PlayerLossGraceSeconds = 8.0f;

    static GameObject? _hostObject;
    static bool _registered;

    static double _noCanvasSince;
    static double _noGameDataSince;
    static double _noPlayerSince;
    static bool _releasedForMissingPlayer;

    public static void Setup()
    {
        if (_registered) return;

        ClassInjector.RegisterTypeInIl2Cpp<UpdateDriver>();
        _hostObject = new GameObject("ChatPlusUpdateDriver");
        UnityEngine.Object.DontDestroyOnLoad(_hostObject);
        _hostObject.hideFlags = HideFlags.HideAndDontSave;
        _hostObject.AddComponent<UpdateDriver>();
        _registered = true;
    }

    public static void Shutdown()
    {
        if (_hostObject != null) UnityEngine.Object.Destroy(_hostObject);
        _hostObject = null;
        _registered = false;
    }

    void Update()
    {
        // The deferred UI teardown must run even after Core is reset. It is the only
        // safe place to destroy the window, after the disposing world is gone.
        if (Patches.InitializationPatch.HasPendingUiTeardown)
            Patches.InitializationPatch.RunPendingUiTeardown();

        if (!Core.HasInitialized) return;

        // Watchdog: if the bound session ended without a clean teardown (drop, kick,
        // leave-to-menu where ClientBootstrapSystem.OnDestroy may not fire), reset the
        // whole session so the next join re-binds cleanly instead of freezing on
        // stale input state.
        if (NeedsSessionReset(out string reason))
        {
            Patches.InitializationPatch.ResetForReconnect(reason);
            return;
        }

        // A disconnect can leave the client world running with no local player. That
        // does not end the session, so only free the keyboard here.
        UpdatePlayerWatchdog();

        foreach (Action action in Actions.ToList())
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                Core.Log.LogError($"[ChatPlus] Update action failed: {ex}");
            }
        }
    }

    static bool NeedsSessionReset(out string reason)
    {
        reason = string.Empty;
        double now = Time.realtimeSinceStartupAsDouble;

        if (!Core.WorldIsAlive)
        {
            reason = "world dead";
            return true;
        }

        // Before the session is armed the window was never built, so there is nothing
        // to tear down and the load screen must not trip a reset.
        if (!Core.Armed)
        {
            _noCanvasSince = 0;
            _noGameDataSince = 0;
            _noPlayerSince = 0;
            _releasedForMissingPlayer = false;
            return false;
        }

        if (!Core.HasCanvas)
        {
            if (_noCanvasSince == 0) _noCanvasSince = now;
            if (now - _noCanvasSince >= NoCanvasGraceSeconds)
            {
                reason = "HUD canvas gone";
                return true;
            }
        }
        else
        {
            _noCanvasSince = 0;
        }

        if (!Core.GameDataReady)
        {
            if (_noGameDataSince == 0) _noGameDataSince = now;
            if (now - _noGameDataSince >= NoGameDataGraceSeconds)
            {
                reason = "game data gone";
                return true;
            }
        }
        else
        {
            _noGameDataSince = 0;
        }

        return false;
    }

    static void UpdatePlayerWatchdog()
    {
        if (Core.HasLocalPlayer)
        {
            _noPlayerSince = 0;
            _releasedForMissingPlayer = false;
            return;
        }

        double now = Time.realtimeSinceStartupAsDouble;
        if (_noPlayerSince == 0)
        {
            _noPlayerSince = now;
            return;
        }

        if (!_releasedForMissingPlayer && now - _noPlayerSince >= PlayerLossGraceSeconds)
        {
            _releasedForMissingPlayer = true;
            Core.Log.LogInfo($"[ChatPlus] Local player gone for {PlayerLossGraceSeconds:0}s, releasing input.");
            try { ChatPlus.UI.ChatWindow.ForceReleaseInput(); } catch { }
            try { Services.InputLockService.ReleaseNow(); } catch { }
        }
    }
}
