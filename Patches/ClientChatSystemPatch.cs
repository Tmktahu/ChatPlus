using ChatPlus.Services;
using ChatPlus.UI;
using HarmonyLib;
using ProjectM.Network;
using ProjectM.UI;

namespace ChatPlus.Patches;

[HarmonyPatch]
internal static class ClientChatSystemPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ClientChatSystem), nameof(ClientChatSystem.OnUpdate))]
    static void OnUpdate_Prefix(ClientChatSystem __instance)
    {
        if (!Core.HasInitialized) return;
        if (__instance == null) return;
        if (__instance.World == null || !__instance.World.IsCreated) return;

        try { NativeChatService.ApplyHide(); }
        catch { }

        try
        {
            if (ChatWindow.IsModdedActive && !ChatWindow.IsTyping && __instance.IsChatOpen)
                __instance.ForceClose();
        }
        catch (Exception ex)
        {
            Core.Log.LogDebug($"[ChatPlus] ForceClose failed: {ex.Message}");
        }

        // Read the inbound chat events through ChatPlus's own query. We do not touch the
        // game's private ClientChatSystem._ReceiveChatMessagesQuery.
        InboundChatService.Scan();
    }

    [HarmonyPatch(typeof(ClientChatSystem), "FormatFullChatMessage")]
    [HarmonyPostfix]
    static void FormatFullChatMessage_Postfix(ServerChatMessageType messageType, string filteredText, string userName)
    {
        if (!Core.HasInitialized) return;
        try
        {
            ChatDataService.CaptureFormatted(messageType, userName, filteredText);
        }
        catch (Exception ex)
        {
            Core.Log.LogDebug($"[ChatPlus] Format capture skipped: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(ClientChatSystem), nameof(ClientChatSystem.OnUpdate))]
    [HarmonyPostfix]
    static void OnUpdate_Postfix(ClientChatSystem __instance)
    {
        if (!Core.HasInitialized) return;
        if (__instance == null) return;
        if (__instance.World == null || !__instance.World.IsCreated) return;

        try { NativeChatService.ApplyHide(); }
        catch { }
    }
}
