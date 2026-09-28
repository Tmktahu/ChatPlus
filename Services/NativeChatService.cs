using ProjectM.UI;
using UnityEngine;

namespace ChatPlus.Services;

internal static class NativeChatService
{
    static HUDChatWindow? _nativeChat;
    static double _nextFindAttempt;
    const double FindRetrySeconds = 1.0;

    public static bool ShouldBlockFocus => Core.HasInitialized && ChatPlus.UI.ChatWindow.IsModdedActive;

    // Returns the cached HUDChatWindow only when it is still a live Unity object. A
    // destroyed MonoBehaviour compares equal to null through Unity's own overload, so the
    // reference is re-validated on every call instead of being trusted across frames. A
    // failed lookup is throttled so FindObjectOfType does not run every frame.
    static bool TryGetNative(out HUDChatWindow? chat)
    {
        chat = null;
        try
        {
            HUDChatWindow? cached = _nativeChat;
            if (cached != null)
            {
                chat = cached;
                return true;
            }

            Invalidate();

            double now = Time.realtimeSinceStartupAsDouble;
            if (now < _nextFindAttempt) return false;
            _nextFindAttempt = now + FindRetrySeconds;

            HUDChatWindow? found = UnityEngine.Object.FindObjectOfType<HUDChatWindow>();
            if (found == null) return false;

            _nativeChat = found;
            chat = found;
            return true;
        }
        catch
        {
            Invalidate();
            chat = null;
            return false;
        }
    }

    static void Invalidate()
    {
        _nativeChat = null;
    }

    public static void ApplyHide()
    {
        try
        {
            if (!Core.HasInitialized) return;
            if (!ChatPlus.UI.ChatWindow.IsModdedActive) return;
            if (!TryGetNative(out HUDChatWindow? chat) || chat == null) return;

            CanvasGroup? cg = chat.ContentCanvasGroup;
            if (cg != null)
            {
                cg.alpha = 0f;
                cg.blocksRaycasts = false;
            }

            if (chat.IsChatFocused)
                chat.SetFocused(false);
        }
        catch
        {
            Invalidate();
        }
    }

    public static void RestoreNative()
    {
        try
        {
            if (!TryGetNative(out HUDChatWindow? chat) || chat == null) return;

            CanvasGroup? cg = chat.ContentCanvasGroup;
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.blocksRaycasts = true;
            }
        }
        catch
        {
            Invalidate();
        }
    }

    public static void Tick()
    {
        ApplyHide();
    }

    public static void Reset()
    {
        _nativeChat = null;
        _nextFindAttempt = 0;
    }
}
