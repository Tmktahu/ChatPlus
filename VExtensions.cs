using ProjectM.Network;
using System;
using Unity.Entities;

namespace ChatPlus;

internal static class VExtensions
{
    public static bool Has<T>(this Entity entity)
    {
        try
        {
            if (!Core.HasInitialized) return false;
            if (entity == Entity.Null) return false;
            World w = Core.EntityManager.World;
            if (w == null || !w.IsCreated) return false;
            if (!Core.EntityManager.Exists(entity)) return false;
            return Core.EntityManager.HasComponent<T>(entity);
        }
        catch { return false; }
    }

    public static bool Exists(this Entity entity)
    {
        try
        {
            if (entity == Entity.Null) return false;
            if (!Core.HasInitialized) return false;
            World w = Core.EntityManager.World;
            if (w == null || !w.IsCreated) return false;
            return Core.EntityManager.Exists(entity);
        }
        catch { return false; }
    }

    public static T Read<T>(this Entity entity) where T : struct
    {
        try
        {
            if (!Core.HasInitialized) return default;
            if (entity == Entity.Null) return default;
            World w = Core.EntityManager.World;
            if (w == null || !w.IsCreated) return default;
            return Core.EntityManager.TryGetComponentData<T>(entity, out T data) ? data : default;
        }
        catch { return default; }
    }

    public static NetworkId GetNetworkId(this Entity entity)
    {
        try
        {
            if (!Core.HasInitialized) return NetworkId.Empty;
            if (entity == Entity.Null) return NetworkId.Empty;
            World w = Core.EntityManager.World;
            if (w == null || !w.IsCreated) return NetworkId.Empty;
            return Core.EntityManager.TryGetComponentData<NetworkId>(entity, out NetworkId id) ? id : NetworkId.Empty;
        }
        catch { return NetworkId.Empty; }
    }
}
