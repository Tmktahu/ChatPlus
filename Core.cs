using BepInEx.Logging;
using ProjectM;
using ProjectM.UI;
using Stunlock.Core;
using Unity.Entities;
using UnityEngine;
using System;

namespace ChatPlus;

internal static class Core
{
    static World? _client;
    static Entity _localCharacter = Entity.Null;
    static Entity _localUser = Entity.Null;
    static bool _initialized;
    static bool _gameDataReady;
    static bool _armed;
    static bool _hasLocalPlayer;
    static double _nextPlayerCheck;

    public static ManualLogSource Log => Plugin.LogInstance;
    public static bool HasInitialized => _initialized;

    // GameDataManager.GameDataInitialized, mirrored every frame. The mod binds only
    // while this is true, and the watchdog treats a falling edge as a session end.
    public static bool GameDataReady => _gameDataReady;

    public static void SetGameDataReady(bool ready) => _gameDataReady = ready;

    // True once the client HUD canvas was captured for this session. The watchdog
    // stays quiet until the session is armed, so the load screen cannot trip a reset.
    public static bool Armed => _armed;

    // Throttled check for a live local player. A disconnect can leave the client
    // world running with no local entities, so this is the signal that frees the
    // keyboard without a full session reset.
    public static bool HasLocalPlayer
    {
        get
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < _nextPlayerCheck) return _hasLocalPlayer;
            _nextPlayerCheck = now + 0.5;
            try
            {
                _hasLocalPlayer = LocalUser != Entity.Null && LocalCharacter != Entity.Null;
            }
            catch
            {
                _hasLocalPlayer = false;
            }
            return _hasLocalPlayer;
        }
    }

    public static bool WorldIsAlive
    {
        get
        {
            if (_client == null) return false;
            try { return _client.IsCreated; }
            catch { return false; }
        }
    }

    public static EntityManager EntityManager => _client!.EntityManager;

    public static UICanvasBase Canvas { get; private set; } = null!;
    public static bool HasCanvas => Canvas != null;

    public static UnityEngine.Canvas UiCanvas { get; private set; } = null!;
    public static int UiLayer { get; private set; }

    public static event Action? HudReady;

    public static Entity LocalCharacter
    {
        get
        {
            if (!WorldIsAlive) return Entity.Null;
            if (_localCharacter.Exists()) return _localCharacter;

            try
            {
                bool ok = ConsoleShared.TryGetLocalCharacterInCurrentWorld(out _localCharacter, _client!);
                if (ok && _localCharacter.Exists()) return _localCharacter;

                _localCharacter = Entity.Null;
                return Entity.Null;
            }
            catch
            {
                _localCharacter = Entity.Null;
                return Entity.Null;
            }
        }
    }

    public static Entity LocalUser
    {
        get
        {
            if (!WorldIsAlive) return Entity.Null;
            if (_localUser.Exists()) return _localUser;

            try
            {
                bool ok = ConsoleShared.TryGetLocalUserInCurrentWorld(out _localUser, _client!);
                if (ok && _localUser.Exists()) return _localUser;

                _localUser = Entity.Null;
                return Entity.Null;
            }
            catch
            {
                _localUser = Entity.Null;
                return Entity.Null;
            }
        }
    }

    public static void Initialize(GameDataManager gameDataManager)
    {
        if (_initialized) return;

        try
        {
            _client = gameDataManager.World;
            _initialized = true;
            Log.LogInfo("[ChatPlus] Client world bound.");
        }
        catch
        {
            _client = null;
            throw;
        }
    }

    public static void SetCanvas(UICanvasBase canvas)
    {
        if (canvas == null) return;

        UICanvasBase? previous = Canvas;
        Canvas = canvas;

        // A new HUD canvas means the old native chat window was destroyed with it. Drop
        // the cached reference so the next ApplyHide re-resolves a live object.
        if (previous != canvas)
        {
            try { Services.NativeChatService.Reset(); } catch { }
        }

        if (canvas.BottomBarParent == null) return;

        UiCanvas = canvas.BottomBarParent.gameObject.GetComponent<UnityEngine.Canvas>();
        if (UiCanvas == null) return;

        UiLayer = UiCanvas.gameObject.layer;
        _armed = true;
        Behaviors.UpdateDriver.Setup();
        Log.LogInfo("[ChatPlus] Game canvas captured, update driver running.");
    }

    internal static void RaiseHudReady()
    {
        HudReady?.Invoke();
    }

    public static void Reset()
    {
        _client = null;
        _initialized = false;
        _localCharacter = Entity.Null;
        _localUser = Entity.Null;
        _gameDataReady = false;
        _armed = false;
        _hasLocalPlayer = false;
        _nextPlayerCheck = 0;
        Canvas = null!;
        UiCanvas = null!;
        UiLayer = 0;
    }
}
