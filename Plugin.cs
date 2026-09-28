using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace ChatPlus;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
internal class Plugin : BasePlugin
{
    Harmony _harmony = null!;

    internal static Plugin Instance { get; private set; } = null!;
    public static ManualLogSource LogInstance => Instance.Log;
    public static bool IsClient { get; private set; }

    public override void Load()
    {
        Instance = this;

        IsClient = Application.productName.Equals("VRising", StringComparison.OrdinalIgnoreCase);
        if (!IsClient)
        {
            Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} [{MyPluginInfo.PLUGIN_VERSION}] is a client mod, skipping on {Application.productName}.");
            return;
        }

        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.CreateClassProcessor(typeof(Patches.InitializationPatch)).Patch();
        _harmony.CreateClassProcessor(typeof(Patches.ClientChatSystemPatch)).Patch();
        _harmony.CreateClassProcessor(typeof(Patches.NativeChatPatch)).Patch();
        Patches.InputSuppressionPatch.Apply(_harmony);

        Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} [{MyPluginInfo.PLUGIN_VERSION}] loaded.");
    }

    public override bool Unload()
    {
        Services.HistoryService.Flush();
        Services.InputHistoryService.Flush();
        Services.SettingsService.Flush();
        Behaviors.UpdateDriver.Shutdown();
        _harmony?.UnpatchSelf();
        return true;
    }
}
