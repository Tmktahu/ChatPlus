using HarmonyLib;
using ProjectM;
using ProjectM.UI;
using Unity.Entities;

namespace ChatPlus.Patches;

[HarmonyPatch]
internal static class InitializationPatch
{
    static bool _canvasCaptured;
    static bool _pendingUiTeardown;

    public static bool HasPendingUiTeardown => _pendingUiTeardown;

    // Clears a teardown that was queued by a session where Setup() never ran, so the
    // pending reset cannot destroy the freshly built window of the next session.
    public static void ClearPendingUiTeardown() => _pendingUiTeardown = false;

    [HarmonyPatch(typeof(GameDataManager), nameof(GameDataManager.OnUpdate))]
    [HarmonyPostfix]
    static void GameDataManager_OnUpdate_Postfix(GameDataManager __instance)
    {
        // Mirror the game-data flag every frame, before any early return, so the
        // watchdog can see a falling edge when the player leaves the game.
        try { Core.SetGameDataReady(__instance != null && __instance.GameDataInitialized); } catch { }

        if (Core.HasInitialized) return;
        if (__instance == null) return;
        // Bind only in a live game. Without this the mod re-initializes on the main
        // menu canvas and can build its window there.
        if (!Core.GameDataReady) return;
        if (__instance.World == null || !__instance.World.IsCreated) return;
        try
        {
            Core.Initialize(__instance);
            Core.Log.LogInfo($"[ChatPlus] AddLocalMessage overload check: {ResolveAddLocalMessageSig()}");
        }
        catch (Exception ex) { Core.Log.LogError($"[ChatPlus] Initialize failed: {ex}"); }
    }

    static string ResolveAddLocalMessageSig()
    {
        try
        {
            var m = typeof(ClientSystemChatUtils).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            foreach (var mi in m)
            {
                if (mi.Name != "AddLocalMessage") continue;
                var ps = mi.GetParameters();
                return $"{mi.Name}({string.Join(", ", System.Array.ConvertAll(ps, p => p.ParameterType.Name + " " + p.Name))})";
            }
            return "AddLocalMessage not found";
        }
        catch (Exception ex) { return $"resolve failed: {ex.Message}"; }
    }

    [HarmonyPatch(typeof(UICanvasBase), nameof(UICanvasBase.Awake))]
    [HarmonyPostfix]
    static void UICanvasBase_Awake_Postfix(UICanvasBase __instance)
    {
        try
        {
            if (Core.HasInitialized) ClearPendingUiTeardown();
            Core.SetCanvas(__instance);
            ChatPlus.UI.ChatWindow.EnsureBuilt();
        }
        catch (Exception ex) { Core.Log.LogDebug($"[ChatPlus] Canvas capture skipped: {ex.Message}"); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UICanvasSystem), nameof(UICanvasSystem.UpdateHideIfDisabled))]
    static void UICanvasSystem_UpdateHideIfDisabled_Postfix(UICanvasBase canvas)
    {
        if (_canvasCaptured || !Core.HasInitialized) return;
        try
        {
            _canvasCaptured = true;
            ClearPendingUiTeardown();
            Core.SetCanvas(canvas);
            ChatPlus.UI.ChatWindow.EnsureBuilt();
        }
        catch (Exception ex)
        {
            Core.Log.LogError($"[ChatPlus] Canvas capture failed: {ex}");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CharacterHUDEntry), nameof(CharacterHUDEntry.Awake))]
    static void CharacterHUDEntry_Awake_Postfix(CharacterHUDEntry __instance)
    {
        try
        {
            Core.RaiseHudReady();
            ChatPlus.UI.ChatWindow.EnsureBuilt();
        }
        catch (Exception ex)
        {
            Core.Log.LogError($"[ChatPlus] HUD ready handler failed: {ex}");
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ClientBootstrapSystem), nameof(ClientBootstrapSystem.OnDestroy))]
    static void ClientBootstrapSystem_OnDestroy_Prefix()
    {
        ResetForReconnect("ClientBootstrapSystem.OnDestroy");
    }

    public static void ResetForReconnect(string reason)
    {
        // PURE field resets only. The ClientBootstrapSystem.OnDestroy hook fires while
        // the world is disposing, so no GameObject or TMP work may run here. The chat
        // window teardown is deferred to RunPendingUiTeardown on the next Update tick.
        _pendingUiTeardown = true;
        try { _canvasCaptured = false; } catch { }
        try { Services.NativeChatService.Reset(); } catch { }
        try { Services.InboundChatService.Reset(); } catch { }
        try { Services.ClanHudService.Reset(); } catch { }
        try { Services.ChatAvailabilityService.OnWorldTeardown(); } catch { }
        try { Services.AdminService.OnWorldTeardown(); } catch { }
        try { Services.ConsoleGateService.OnWorldTeardown(); } catch { }
        try { Patches.InputSuppressionPatch.OnWorldTeardown(); } catch { }
        try { Services.ChatDataService.Clear(); } catch { }
        try { Core.Reset(); } catch (Exception ex) { Core.Log.LogError($"[ChatPlus] Core reset failed: {ex}"); }
        Core.Log.LogInfo($"[ChatPlus] Session reset ({reason}).");
    }

    // Runs on the next Update tick, after the world is gone, where GameObject work is
    // safe. Called from UpdateDriver BEFORE its Core.HasInitialized guard.
    public static void RunPendingUiTeardown()
    {
        if (!_pendingUiTeardown) return;
        _pendingUiTeardown = false;
        // Best-effort native unregister while the world may still be alive, then drop
        // the refs. On a real teardown the world is already gone and the dying
        // InputActionSystem takes the registration with it.
        try { Services.InputLockService.ReleaseNow(); } catch { }
        try { ChatPlus.UI.ChatWindow.ResetSession(); }
        catch (Exception ex) { Core.Log.LogError($"[ChatPlus] ResetSession failed: {ex}"); }
    }
}
