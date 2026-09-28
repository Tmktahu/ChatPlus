using Il2CppInterop.Runtime;
using ProjectM;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;

namespace ChatPlus.Services;

internal static class ChatAvailabilityService
{
    static EntityQuery _settingsQuery;
    static bool _settingsQueryReady;
    static bool _cachedGlobal = true;
    static double _lastGlobalCheck;

    public static bool IsGlobalEnabled
    {
        get
        {
            try
            {
                double now = UnityEngine.Time.realtimeSinceStartupAsDouble;
                if (now - _lastGlobalCheck < 1.0) return _cachedGlobal;

                if (!Core.HasInitialized) return true;
                EntityManager em = Core.EntityManager;
                World w = em.World;
                if (w == null || !w.IsCreated)
                {
                    _settingsQueryReady = false;
                    return true;
                }

                if (!_settingsQueryReady)
                {
                    _settingsQuery = em.CreateEntityQuery(ComponentType.ReadOnly(Il2CppType.Of<ServerGameBalanceSettings>()));
                    _settingsQueryReady = true;
                }

                NativeArray<Entity> entities = _settingsQuery.ToEntityArray(Allocator.Temp);
                try
                {
                    if (entities.Length == 0)
                    {
                        _cachedGlobal = true;
                        _lastGlobalCheck = now;
                        return true;
                    }
                    _cachedGlobal = em.GetComponentData<ServerGameBalanceSettings>(entities[0]).AllowGlobalChat;
                    _lastGlobalCheck = now;
                    return _cachedGlobal;
                }
                finally
                {
                    entities.Dispose();
                }
            }
            catch
            {
                _settingsQueryReady = false;
                return true;
            }
        }
    }

    public static bool IsAdmin
    {
        get
        {
            try
            {
                if (!Core.HasInitialized) return false;
                Entity user = Core.LocalUser;
                if (user == Entity.Null || !user.Exists() || !user.Has<User>()) return false;
                return user.Read<User>().IsAdmin;
            }
            catch
            {
                return false;
            }
        }
    }

    public static void OnWorldTeardown()
    {
        _settingsQueryReady = false;
        _settingsQuery = default;
        _lastGlobalCheck = 0;
        _cachedGlobal = true;
    }
}