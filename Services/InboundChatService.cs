using Il2CppInterop.Runtime;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;

namespace ChatPlus.Services;

// Reads the inbound chat events that the server sends to the client and forwards their
// sender ids to ChatDataService. It owns its own EntityQuery instead of borrowing the
// game's private ClientChatSystem._ReceiveChatMessagesQuery.
internal static class InboundChatService
{
    static EntityQuery _query;
    static World? _queryWorld;
    static bool _queryReady;

    static void DisposeQuery()
    {
        try
        {
            if (_queryReady && _queryWorld != null && _queryWorld.IsCreated)
            {
                _query.Dispose();
            }
        }
        catch
        {
        }

        _query = default;
        _queryReady = false;
        _queryWorld = null;
    }

    static bool EnsureQuery(World world)
    {
        if (world == null) return false;
        if (_queryReady && _queryWorld == world && world.IsCreated) return true;

        DisposeQuery();

        try
        {
            EntityManager em = world.EntityManager;
            _query = em.CreateEntityQuery(ComponentType.ReadOnly(Il2CppType.Of<ChatMessageServerEvent>()));
            _queryWorld = world;
            _queryReady = true;
            return true;
        }
        catch
        {
            _query = default;
            _queryWorld = null;
            _queryReady = false;
            return false;
        }
    }

    public static void Scan()
    {
        try
        {
            if (!Core.HasInitialized) return;

            World? world = Core.EntityManager.World;
            if (world == null || !world.IsCreated)
            {
                DisposeQuery();
                return;
            }

            if (!EnsureQuery(world)) return;

            // Only build the native array when there is something to read.
            if (_query.CalculateEntityCount() <= 0) return;

            NativeArray<Entity> entities = _query.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (Entity entity in entities)
                {
                    if (!entity.Exists()) continue;
                    if (!entity.Has<ChatMessageServerEvent>()) continue;

                    ChatMessageServerEvent chatEvent = entity.Read<ChatMessageServerEvent>();
                    if (ChatDataService.IsSenderBearing(chatEvent.MessageType))
                    {
                        ChatDataService.EnqueueSenderId(chatEvent.FromUser);
                    }
                }
            }
            finally
            {
                entities.Dispose();
            }
        }
        catch
        {
        }
    }

    public static void Reset()
    {
        DisposeQuery();
    }
}
