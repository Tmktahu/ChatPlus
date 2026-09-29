using Il2CppInterop.Runtime;
using ProjectM.Network;
using ProjectM.UI;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace ChatPlus.Services;

// Shows the local player's own Local chat as the native over-head bubble.
//
// The server broadcasts a Local message to nearby clients but does not echo it back to its
// sender. The native ClientChatSystem therefore never receives a ChatMessageServerEvent for
// the local player, so its own bubble path never runs. ChatPlus also hides the native chat
// window, so the native input path (_OnInputEndEdit) that would add the local message never
// runs either.
//
// The bubble itself is created by ClientChatSystem.CreateLocalChatMessageSCT. That method is
// exposed as public by the interop assembly. This service calls it directly for the local
// character, using the same NetworkIdLookupMap the native receive job uses.
internal static class LocalBubbleService
{
    const int MaxPending = 8;

    static readonly List<string> Pending = new();

    static ClientChatSystem? _chatSystem;
    static EntityQuery _networkIdQuery;
    static World? _queryWorld;

    // The live ClientChatSystem is captured from ChatPlus's own Harmony patch, so the
    // service never has to resolve the managed system itself.
    public static void SetSystem(ClientChatSystem chat)
    {
        if (chat == null) return;
        _chatSystem = chat;
    }

    public static void Enqueue(string text)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (Pending.Count >= MaxPending) Pending.RemoveAt(0);
            Pending.Add(text);
        }
        catch
        {
        }
    }

    static void DisposeQuery()
    {
        try
        {
            if (_queryWorld != null && _queryWorld.IsCreated)
            {
                _networkIdQuery.Dispose();
            }
        }
        catch
        {
        }

        _networkIdQuery = default;
        _queryWorld = null;
    }

    static bool EnsureQuery(World world)
    {
        if (world == null) return false;
        if (_queryWorld == world && world.IsCreated) return true;

        DisposeQuery();

        try
        {
            EntityManager em = world.EntityManager;
            _networkIdQuery = em.CreateEntityQuery(ComponentType.ReadOnly(Il2CppType.Of<NetworkId>()));
            _queryWorld = world;
            return true;
        }
        catch
        {
            _networkIdQuery = default;
            _queryWorld = null;
            return false;
        }
    }

    // Drains the queued texts once per frame, from the ClientChatSystem update. Building the
    // NetworkId map costs a query plus one entry per networked entity, and chat is low rate,
    // so the work is only done when there is something to show.
    public static void Tick()
    {
        if (Pending.Count == 0) return;

        try
        {
            if (!Core.HasInitialized) return;
            if (_chatSystem == null) return;

            World? world = Core.EntityManager.World;
            if (world == null || !world.IsCreated) return;

            Entity character = Core.LocalCharacter;
            if (character == Entity.Null || !character.Exists()) return;

            NetworkId fromCharacterId = character.GetNetworkId();
            if (fromCharacterId == default) return;

            if (!EnsureQuery(world)) return;

            EntityManager em = Core.EntityManager;
            int count = _networkIdQuery.CalculateEntityCount();
            if (count <= 0) return;

            NativeParallelHashMap<NetworkId, Entity> map = new(count, Allocator.TempJob);
            NativeArray<Entity> entities = _networkIdQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (Entity entity in entities)
                {
                    if (!entity.Exists()) continue;
                    if (!em.TryGetComponentData<NetworkId>(entity, out NetworkId id)) continue;
                    if (id == default) continue;
                    map.TryAdd(id, entity);
                }

                NetworkIdLookupMap lookup = new(map);
                try
                {
                    for (int i = 0; i < Pending.Count; i++)
                    {
                        string text = Pending[i];
                        if (string.IsNullOrWhiteSpace(text)) continue;
                        _chatSystem.CreateLocalChatMessageSCT(ref lookup, fromCharacterId, text);
                    }
                }
                finally
                {
                    lookup.Dispose();
                }
            }
            finally
            {
                // lookup.Dispose() disposes the wrapped map. Guard so a failure before that
                // point cannot leak the TempJob allocation.
                if (map.IsCreated) map.Dispose();
                entities.Dispose();
            }

            Pending.Clear();
        }
        catch (Exception ex)
        {
            Core.Log.LogDebug($"[ChatPlus] Local bubble skipped: {ex.Message}");
            Pending.Clear();
        }
    }

    public static void Reset()
    {
        Pending.Clear();
        _chatSystem = null;
        DisposeQuery();
    }
}
