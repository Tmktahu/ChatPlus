using ProjectM.UI;
using UnityEngine;

namespace ChatPlus.Services;

internal static class ClanHudService
{
    static RectTransform? _rect;
    static float _baseY;
    static bool _hasBase;

    public static void Tick()
    {
        try
        {
            if (_rect == null)
            {
                if (!Core.HasInitialized || !Core.HasCanvas) return;
                UICanvasBase canvas = Core.Canvas;
                if (canvas == null) return;
                _rect = canvas.HUDClanParent;
                _hasBase = false;
                if (_rect == null) return;
            }

            if (!_rect.gameObject.activeInHierarchy) return;

            if (!_hasBase)
            {
                _baseY = _rect.anchoredPosition.y;
                _hasBase = true;
            }

            float wantY = _baseY + SettingsService.ClanListYOffset;
            Vector2 pos = _rect.anchoredPosition;
            if (Mathf.Abs(pos.y - wantY) > 0.01f)
            {
                _rect.anchoredPosition = new Vector2(pos.x, wantY);
            }
        }
        catch (Exception ex)
        {
            Core.Log.LogDebug($"[ChatPlus] Clan HUD skipped: {ex.Message}");
            _rect = null;
            _hasBase = false;
        }
    }

    public static void Reset()
    {
        _rect = null;
        _hasBase = false;
    }
}
