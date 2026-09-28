using ChatPlus.Services;
using HarmonyLib;
using ProjectM.UI;

namespace ChatPlus.Patches;

[HarmonyPatch]
internal static class NativeChatPatch
{
    [HarmonyPatch(typeof(HUDChatWindow), nameof(HUDChatWindow.SetFocused))]
    [HarmonyPrefix]
    static bool SetFocused_Prefix(bool isFocused)
    {
        if (isFocused && NativeChatService.ShouldBlockFocus) return false;
        return true;
    }

    [HarmonyPatch(typeof(HUDChatWindow), nameof(HUDChatWindow.FocusInputField))]
    [HarmonyPrefix]
    static bool FocusInputField_Prefix()
    {
        if (NativeChatService.ShouldBlockFocus) return false;
        return true;
    }
}
