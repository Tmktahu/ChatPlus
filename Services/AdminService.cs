using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using System;
using System.Collections.Generic;
using Unity.Entities;

namespace ChatPlus.Services;

// Resolves whether a chat sender is an admin. The client does not keep User
// entities for remote players, but it does keep the server-pushed
// UserInfoElement buffer on the UserInfoBufferSingleton entity. Each element
// carries Name, NetworkId and IsAdmin, so this is the client-side source of
// truth. The buffer is read into a small cache that is refreshed on an
// interval, or sooner when a sender is not yet in the cache.
internal static class AdminService
{
    const double RefreshSeconds = 1.5;
    const double MissRefreshSeconds = 0.2;

    static readonly Dictionary<NetworkId, bool> _byId = new();
    static readonly Dictionary<string, bool> _byName = new(StringComparer.OrdinalIgnoreCase);

    static double _lastRefresh = -1000.0;

    public static bool IsAdmin(NetworkId id, string name)
    {
        try
        {
            if (!Core.HasInitialized) return false;

            bool hasId = id != default;
            string key = name ?? string.Empty;
            bool hasName = key.Length > 0;
            if (!hasId && !hasName) return false;

            double now = UnityEngine.Time.realtimeSinceStartupAsDouble;

            bool miss;
            bool isAdmin;
            if (hasId)
            {
                miss = !_byId.TryGetValue(id, out isAdmin);
            }
            else
            {
                miss = !_byName.TryGetValue(key, out isAdmin);
            }

            if (miss || now - _lastRefresh >= RefreshSeconds)
            {
                // A miss can repeat for a sender the buffer does not know, so
                // throttle miss-driven refreshes separately from the interval.
                if (!miss || now - _lastRefresh >= MissRefreshSeconds)
                {
                    Refresh(now);
                    if (hasId) _byId.TryGetValue(id, out isAdmin);
                    else _byName.TryGetValue(key, out isAdmin);
                }
            }

            return isAdmin;
        }
        catch
        {
            return false;
        }
    }

    static void Refresh(double now)
    {
        _lastRefresh = now;
        _byId.Clear();
        _byName.Clear();

        try
        {
            EntityManager em = Core.EntityManager;
            World w = em.World;
            if (w == null || !w.IsCreated) return;

            if (!SingletonAccessor<UserInfoBufferSingleton>.TryGetSingletonEntityWasteful(em, out Entity singleton)) return;
            if (singleton == Entity.Null || !em.HasBuffer<UserInfoElement>(singleton)) return;

            DynamicBuffer<UserInfoElement> buffer = em.GetBuffer<UserInfoElement>(singleton);
            for (int i = 0; i < buffer.Length; i++)
            {
                UserInfoElement info = buffer[i];
                if (info.NetworkId != default) _byId[info.NetworkId] = info.IsAdmin;

                string elementName = info.Name.ToString();
                if (!string.IsNullOrEmpty(elementName)) _byName[elementName] = info.IsAdmin;
            }
        }
        catch
        {
            _byId.Clear();
            _byName.Clear();
        }
    }

    public static void OnWorldTeardown()
    {
        _byId.Clear();
        _byName.Clear();
        _lastRefresh = -1000.0;
    }
}
