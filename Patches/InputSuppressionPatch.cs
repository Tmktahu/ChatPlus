using ChatPlus.Services;
using ChatPlus.UI;
using HarmonyLib;
using Il2CppInterop.Runtime;
using ProjectM;
using ProjectM.Network;
using ProjectM.UI;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace ChatPlus.Patches;

// Applied manually from Plugin.Load. Do NOT add [HarmonyPatch] attributes to this class.
internal static class InputSuppressionPatch
{
    static EntityQuery _openMenuQuery;
    static EntityQuery _goToHudQuery;
    static bool _queriesReady;

    // Camera zoom suppression is kept STICKY: fast wheel bursts deliver scroll events
    // that the camera can see on frames where UnityEngine's aggregated delta reads 0,
    // or in an order relative to our prefix that races. So once the wheel is seen we
    // keep the camera systems suppressed for this window after the last scroll.
    const float WheelSuppressSeconds = 0.18f;
    static double _lastWheelScrollAt = -1e308;

    static bool ConsoleOpen => Services.ConsoleGateService.IsConsoleOpen;

    // The HUD canvas is required as well as the world. When the player leaves the
    // game the world can look alive for a frame while the HUD is already gone, and
    // suppressing the menu systems at that point can freeze the main menu.
    static bool WorldLive => Core.HasInitialized && Core.WorldIsAlive && Core.HasCanvas;

    static bool ShouldBlockSettings => WorldLive && !ConsoleOpen && ChatWindow.IsSettingsOpen && ChatWindow.IsPointerOverSettings;
    static bool ShouldBlockMenus => WorldLive && !ConsoleOpen && ChatWindow.IsModdedActive && (ChatWindow.IsTyping || ChatWindow.IsDragging || ShouldBlockSettings);

    // True while a wheel is scrolled or shortly after, so the camera never sees a
    // stray zoom notch between our check and its own read. Right-drag pan frames have
    // no wheel delta and fall outside the window, so they pass straight through.
    static bool WheelEngaged =>
        (Time.realtimeSinceStartupAsDouble - _lastWheelScrollAt) < WheelSuppressSeconds ||
        Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f;

    static bool ShouldBlockCameraZoom => WorldLive && !ConsoleOpen && ChatWindow.IsModdedActive &&
        WheelEngaged &&
        (ChatWindow.IsTyping || (ChatWindow.IsSettingsOpen && ChatWindow.IsPointerOverSettings));

    public static void Apply(Harmony harmony)
    {
        PatchOne(harmony, typeof(GameplayInputSystem), nameof(GameplayInputSystem.OnUpdate),
            nameof(GameplayInputSystem_OnUpdate_Prefix));
        PatchOne(harmony, typeof(OpenHUDMenuSystem), nameof(OpenHUDMenuSystem.OnUpdate),
            nameof(OpenHUDMenuSystem_OnUpdate_Prefix));
        // MenuInputSystem.OnUpdate is intentionally NOT patched. A Harmony prefix on that
        // method, including a completely empty void prefix, crashed the client on one
        // specific body about 10 to 20 seconds after it loaded. The other five system
        // patches are safe. Menu suppression relies on the registered ChatFocusedInputContext,
        // the OpenHUDMenuSystem patch, and the menu request entity drain instead.
        PatchOne(harmony, typeof(ActionWheelSystem), nameof(ActionWheelSystem.OnUpdate),
            nameof(ActionWheelSystem_OnUpdate_Prefix));
        PatchOne(harmony, typeof(TopdownCameraSystem), nameof(TopdownCameraSystem.OnUpdate),
            nameof(TopdownCameraSystem_OnUpdate_Prefix));
        PatchOne(harmony, typeof(HybridCameraSystem), nameof(HybridCameraSystem.OnUpdate),
            nameof(HybridCameraSystem_OnUpdate_Prefix));
    }

    static void PatchOne(Harmony harmony, Type type, string targetName, string prefixName)
    {
        try
        {
            MethodInfo? target = AccessTools.Method(type, targetName);
            MethodInfo? prefix = AccessTools.Method(typeof(InputSuppressionPatch), prefixName);
            if (target == null || prefix == null) return;

            harmony.Patch(target, prefix: new HarmonyMethod(prefix));
        }
        catch (Exception ex)
        {
            Core.Log.LogError($"[ChatPlus] Input suppression patch failed for {targetName}: {ex}");
        }
    }

    // Gameplay and ability input are NOT skipped. The game's own InputActionSystem
    // builds a fresh per-frame input snapshot, and ChatFocusedInputContext (registered
    // by InputLockService while typing / settings open) filters movement axes, ability
    // casts, and hotkey bindings BEFORE GameplayInputSystem / AbilityInputSystem run.
    // Skipping those producers here used to freeze EntityInput at its last latched
    // value, so releasing W or a mouse button while chat was focused never got read and
    // the action kept going. Letting the game run means releases flow naturally.
    static bool GameplayInputSystem_OnUpdate_Prefix(GameplayInputSystem __instance)
    {
        try
        {
            // Stamp the wheel every frame HERE, before the camera systems run, so a
            // fast burst keeps the suppress window fresh even on notches that land on
            // frames where our camera prefix sees the aggregated delta as 0.
            if (Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f)
                _lastWheelScrollAt = Time.realtimeSinceStartupAsDouble;

            InputLockService.CaptureFrom(__instance);
            DrainMenuOpenRequests();
            SuppressConsoleKeybinds();
            return true;
        }
        catch
        {
            return true;
        }
    }

    static void DrainMenuOpenRequests()
    {
        try
        {
            if (!ShouldBlockMenus) return;
            if (!Core.HasInitialized) return;

            EntityManager em = Core.EntityManager;
            World world = em.World;
            if (world == null || !world.IsCreated)
            {
                _queriesReady = false;
                return;
            }

            if (!_queriesReady)
            {
                _openMenuQuery = em.CreateEntityQuery(ComponentType.ReadOnly(Il2CppType.Of<OpenMenuEvent>()));
                _goToHudQuery = em.CreateEntityQuery(ComponentType.ReadOnly(Il2CppType.Of<GoToHUDMenu>()));
                _queriesReady = true;
            }

            DrainNonNetworked(em, _openMenuQuery);
            DrainNonNetworked(em, _goToHudQuery);
        }
        catch
        {
            _queriesReady = false;
        }
    }

    static void DrainNonNetworked(EntityManager em, EntityQuery query)
    {
        NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (Entity entity in entities)
            {
                if (!entity.Exists()) continue;

                bool networked = true;
                try
                {
                    networked = em.HasComponent<NetworkId>(entity);
                }
                catch
                {
                }

                if (networked) continue;
                em.DestroyEntity(entity);
            }
        }
        finally
        {
            entities.Dispose();
        }
    }

    static void SuppressConsoleKeybinds()
    {
        try
        {
            if (!ShouldBlockMenus) return;
            var consoleUi = Engine.Console.StunConsole.UI;
            if (consoleUi != null) consoleUi.EnableKeybindingUpdates = false;
        }
        catch
        {
        }
    }

    public static void Tick()
    {
        SuppressConsoleKeybinds();
    }

    // Menu hotkey suppression while typing: the request entities for M/B/I/K/etc. are
    // created by an input reader that runs AFTER GameplayInputSystem (where we also drain),
    // so they slip past and OpenHUDMenuSystem opens the menu. Skip these consumer systems
    // directly while typing so no menu/wheel can open behind the chat box.
    static bool OpenHUDMenuSystem_OnUpdate_Prefix()
    {
        try
        {
            if (!ShouldBlockMenus) return true;
            DrainMenuOpenRequests();
            return false;
        }
        catch
        {
            return true;
        }
    }

    static bool ActionWheelSystem_OnUpdate_Prefix()
    {
        try { return !ShouldBlockMenus; }
        catch { return true; }
    }

    // Zoom-only suppression while the chat box is focused. The camera systems handle
    // BOTH wheel zoom and right-drag pan in the same update, so we only bail out on
    // frames where the wheel is actually scrolling; right-drag pan frames pass through.
    static bool TopdownCameraSystem_OnUpdate_Prefix()
    {
        try { return !ShouldBlockCameraZoom; }
        catch { return true; }
    }

    static bool HybridCameraSystem_OnUpdate_Prefix()
    {
        try { return !ShouldBlockCameraZoom; }
        catch { return true; }
    }

    public static void OnWorldTeardown()
    {
        _openMenuQuery = default;
        _goToHudQuery = default;
        _queriesReady = false;
        _lastWheelScrollAt = -1e308;
    }
}
