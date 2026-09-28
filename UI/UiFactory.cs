using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ChatPlus.UI;

internal static class UiFactory
{
    public static GameObject Create(string name, Transform parent, int layer)
    {
        GameObject go = new(name);
        go.layer = layer;
        go.AddComponent<RectTransform>();
        go.transform.SetParent(parent, false);
        return go;
    }

    public static void SetAnchors(GameObject go, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    public static void Stretch(GameObject go, float left, float right, float bottom, float top)
    {
        SetAnchors(go, Vector2.zero, Vector2.one, new Vector2(left, bottom), new Vector2(-right, -top));
    }

    public static void FillParent(GameObject go)
    {
        SetAnchors(go, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    public static void AnchorPoint(GameObject go, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    public static Image Background(GameObject go, Color color)
    {
        Image image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    public static TextMeshProUGUI Label(GameObject go, string text, float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        return tmp;
    }

    public static Button Button(GameObject go, System.Action onClick, Color? hoverTint = null)
    {
        Image image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(onClick));
        Color hover = hoverTint ?? new Color(0.62f, 0.8f, 1f, 1f);
        ColorBlock colors = button.colors;
        colors.highlightedColor = hover;
        colors.pressedColor = new Color(hover.r * 0.75f, hover.g * 0.75f, hover.b * 0.75f, 1f);
        button.colors = colors;
        return button;
    }

    public static Outline Frame(GameObject go, Color color)
    {
        Outline outline = go.GetComponent<Outline>() ?? go.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(1f, 1f);
        outline.useGraphicAlpha = false;
        return outline;
    }

    public static Image Hairline(GameObject go, Color color)
    {
        Image image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public static void AddSubmitListener(TMP_InputField field, System.Action onSubmitted)
    {
        field.onSubmit.AddListener(DelegateSupport.ConvertDelegate<UnityAction<string>>(new System.Action<string>(_ => onSubmitted())));
    }

    public static void AddEndEditListener(TMP_InputField field, System.Action<string> onEndEdit)
    {
        field.onEndEdit.AddListener(DelegateSupport.ConvertDelegate<UnityAction<string>>(onEndEdit));
    }

    public static TMP_InputField InputField(GameObject go, TextMeshProUGUI textComponent, TextMeshProUGUI placeholder, RectTransform viewport, Image targetGraphic)
    {
        // TMP_InputField toggles the placeholder's alpha via a CanvasGroup on the
        // placeholder GameObject. Without one the placeholder is never hidden and
        // overlaps typed text. Ensure the group exists (check, don't double-add).
        if (placeholder != null && placeholder.gameObject.GetComponent<CanvasGroup>() == null)
        {
            placeholder.gameObject.AddComponent<CanvasGroup>();
        }

        TMP_InputField field = go.AddComponent<TMP_InputField>();
        field.textViewport = viewport;
        field.textComponent = textComponent;
        field.placeholder = placeholder;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.contentType = TMP_InputField.ContentType.Standard;
        field.targetGraphic = targetGraphic;
        field.caretColor = new Color(0.85f, 0.25f, 0.28f, 1f);
        field.selectionColor = new Color(0.549f, 0.047f, 0.059f, 0.75f);
        return field;
    }

    public static Slider AddHorizontalSlider(GameObject go, RectTransform handleRect, RectTransform fillRect, Image handleImage, float value, System.Action<float> onChanged)
    {
        Slider slider = go.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = value;
        slider.handleRect = handleRect;
        slider.fillRect = fillRect;
        slider.targetGraphic = handleImage;
        slider.transition = Selectable.Transition.None;
        slider.onValueChanged.AddListener(DelegateSupport.ConvertDelegate<UnityAction<float>>(onChanged));
        return slider;
    }

    public static Slider AddRangeHorizontalSlider(GameObject go, RectTransform handleRect, RectTransform fillRect, Image handleImage, float value, float minValue, float maxValue, System.Action<float> onChanged)
    {
        Slider slider = go.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        slider.value = value;
        slider.handleRect = handleRect;
        slider.fillRect = fillRect;
        slider.targetGraphic = handleImage;
        slider.transition = Selectable.Transition.None;
        slider.onValueChanged.AddListener(DelegateSupport.ConvertDelegate<UnityAction<float>>(onChanged));
        return slider;
    }

    static readonly System.Collections.Generic.Dictionary<string, Sprite> _fadeSprites = new();

    public static Image LeftFadeBackground(GameObject go, Color color, float fadeEnd = 0.75f)
    {
        string key = "H" + (int)(fadeEnd * 100f);
        if (!_fadeSprites.TryGetValue(key, out Sprite? sprite))
        {
            sprite = BuildHorizontalFadeSprite(fadeEnd, false);
            _fadeSprites[key] = sprite;
        }
        return ApplyFadeSprite(go, sprite, color);
    }

    public static Image RightFadeBackground(GameObject go, Color color, float fadeEnd = 1f)
    {
        string key = "R" + (int)(fadeEnd * 100f);
        if (!_fadeSprites.TryGetValue(key, out Sprite? sprite))
        {
            sprite = BuildHorizontalFadeSprite(fadeEnd, true);
            _fadeSprites[key] = sprite;
        }
        return ApplyFadeSprite(go, sprite, color);
    }

    public static Image BottomFadeBackground(GameObject go, Color color)
    {
        const string key = "V";
        if (!_fadeSprites.TryGetValue(key, out Sprite? sprite))
        {
            sprite = BuildVerticalFadeSprite();
            _fadeSprites[key] = sprite;
        }
        return ApplyFadeSprite(go, sprite, color);
    }

    static Image ApplyFadeSprite(GameObject go, Sprite sprite, Color color)
    {
        Image image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static Sprite BuildHorizontalFadeSprite(float fadeEnd, bool mirrored)
    {
        const int w = 64;
        const int h = 1;
        Texture2D tex = new(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[w * h];
        for (int x = 0; x < w; x++)
        {
            float t = x / (float)(w - 1);
            if (mirrored) t = 1f - t;
            float a = t <= fadeEnd ? 1f - t / fadeEnd : 0f;
            px[x] = new Color(1f, 1f, 1f, a * a);
        }
        tex.SetPixels(px);
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite BuildVerticalFadeSprite()
    {
        const int w = 1;
        const int h = 64;
        Texture2D tex = new(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float a = 1f - y / (float)(h - 1);
            px[y] = new Color(1f, 1f, 1f, a * a);
        }
        tex.SetPixels(px);
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite? _wrenchSprite;

    public static Sprite WrenchSprite()
    {
        if (_wrenchSprite != null) return _wrenchSprite;

        // Texture is 9:16 to match the display size (RailW x InputH-2), so the icon is
        // scaled UNIFORMLY (~0.5x) and a radius of 14 renders as a 14px circle
        // (button width 18 - 4), with a solid 14px-thick U edge = the 7px bar half-width.
        const int w = 36;
        const int h = 64;
        Texture2D tex = new(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[w * h];

        float cx = w * 0.5f;   // 18

        float headR   = 10.0f;  // circle radius -> diameter 20 tex = 10px on screen
        float slotHW  = 4.0f;   // slot half-width -> full 8 tex = 4px opening (same as bar)
        float handleHW = 4.0f;  // middle line half-width -> full 8 tex = 4px bar

        float headTopY = 50f;   // top head center (keep LOWER half per current look)
        float headBotY = 14f;   // bottom head center (keep UPPER half per current look)

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float fx = x + 0.5f;
                float fy = y + 0.5f;

                float dTop = Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - headTopY) * (fy - headTopY));
                float discTop = Mathf.Clamp01((headR - dTop) / 1.5f);
                float slotT = Band(fx, cx - slotHW, cx + slotHW) * Band(fy, headTopY, 64f);
                float headTop = Mathf.Clamp01(discTop * (1f - slotT));

                float dBot = Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - headBotY) * (fy - headBotY));
                float discBot = Mathf.Clamp01((headR - dBot) / 1.5f);
                float slotB = Band(fx, cx - slotHW, cx + slotHW) * Band(fy, 0f, headBotY);
                float headBot = Mathf.Clamp01(discBot * (1f - slotB));

                // Middle bar: center-to-center (circle middle == slot base == bar end).
                float handle = Band(fx, cx - handleHW, cx + handleHW) * Band(fy, headBotY, headTopY);

                float alpha = Mathf.Clamp01(Mathf.Max(headTop, Mathf.Max(headBot, handle)));
                px[y * w + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(px);
        tex.Apply(false, false);
        _wrenchSprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
        return _wrenchSprite;
    }

    // 1 in a horizontal/vertical slab [min,max] on that axis (x or y), with ~1px soft edge.
    static float Band(float v, float lo, float hi)
    {
        float t = Mathf.Min(v - lo, hi - v);
        return Mathf.Clamp01(t);
    }
}
