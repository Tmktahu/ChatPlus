using ChatPlus.Behaviors;
using ChatPlus.Messaging;
using ChatPlus.Models;
using ChatPlus.Services;
using ProjectM.Network;
using System;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChatPlus.UI;

internal static class ChatWindow
{
    const float DefaultWidth = 460f;
    const float DefaultHeight = 340f;
    const float InputH = 34f;
    const float RailW = 18f;
    const float LabelH = 18f;
    const float LabelFontSize = 13f;
    const int MaxVisibleLines = 300;

    static float WinW => Services.SettingsService.WindowWidth;
    static float WinH => Services.SettingsService.WindowHeight;
    static bool Mirrored => Services.SettingsService.MirrorLayout;
    static float ContentWidth => WinW - RailW;

    const double IdleVisibleSeconds = 15.0;
    const float FadeInRate = 4f;
    const float FadeOutRate = 1.67f;
    const float WheelScrollStep = 0.08f;

    static bool _subscribed;
    static bool _built;
    static bool _open;
    static bool _logDirty;

    static GameObject? _root;
    static RectTransform? _rootRect;
    static CanvasGroup? _fade;
    static float _alpha;
    static double _lastActivityUtc = -1000.0;
    static double _recalcAfterOpen = -1.0;

    static bool _chatActive;
    static bool _viewSettings;
    static bool _viewHelp;

    static GameObject? _chatView;
    static GameObject? _settingsView;
    static GameObject? _helpView;
    static GameObject? _helpBtn;
    static GameObject? _helpBackBtn;
    static GameObject? _copyBtn;
    static GameObject? _bottomBar;
    static RectTransform? _hkContainer;
    static RectTransform? _hkAddBtnRt;
    static TextMeshProUGUI? _hkHint;
    static ScrollRect? _settingsScroll;
    static Scrollbar? _settingsScrollbar;
    static RectTransform? _settingsScrollbarRect;
    static GameObject? _settingsHandleGo;
    static ScrollRect? _helpScroll;
    static Scrollbar? _helpScrollbar;
    static RectTransform? _helpScrollbarRect;
    static GameObject? _helpHandleGo;
    static bool _settingsScrollbarDragging;
    static int _hkArmedIndex = -1;

    static TextMeshProUGUI? _log;
    static TMP_InputField? _compose;
    static ScrollRect? _scroll;
    static RectTransform? _scrollContent;
    static Scrollbar? _scrollbar;
    static RectTransform? _scrollbarRect;
    static GameObject? _scrollbarHandleGo;
    static bool _scrollbarDragging;
    // Display size of the rail scrollbar handle, smoothed so it does not pop when the
    // rendered window changes. The ScrollRect does not manage this bar.
    static float _displayScrollSize = 0.2f;
    static RectTransform? _gripRect;
    static Image? _gripBg;
    static bool _dragging;
    static Vector2 _dragStartMouse;
    static Vector2 _dragStartPos;
    static TextMeshProUGUI? _channelLabel;
    static GameObject? _channelBg;
    static RectTransform? _channelLabelRect;
    static GameObject? _composeBar;
    static GameObject? _hintBar;
    static bool _wasTypingLastFrame;
    static bool _focusPending;

    static Slider? _opacitySlider;
    static TextMeshProUGUI? _opacityValue;
    static Slider? _inputOpacitySlider;
    static TextMeshProUGUI? _inputOpacityValue;
    static TMP_InputField? _geomX;
    static TMP_InputField? _geomY;
    static TMP_InputField? _geomW;
    static TMP_InputField? _geomH;
    static TMP_InputField? _geomClan;
    static Slider? _geomWSlider;
    static Slider? _geomHSlider;
    static Slider? _geomClanSlider;
    static bool _sliderSyncGuard;

    static Image? _scrollBg;
    static Image? _composeBarBg;
    static Image? _inputBg;
    static TextMeshProUGUI? _placeholderTmp;
    static RectTransform? _composeBarRect;
    static RectTransform? _logScrollRect;
    static RectTransform? _textAreaRect;
    static RectTransform? _settingsBtnRt;
    static Image? _settingsBtnBg;
    static RectTransform? _railRt;
    static float _composeH = InputH;

    static int _pendingHkIndex = -1;
    static int _pendingCmdIndex = -1;
    static readonly System.Collections.Generic.List<System.Collections.Generic.List<TMP_InputField?>> _hkCommandFields = new();
    static bool _forceHoverRecompute;
    static int _periodicRefreshCounter;
    static bool _scrollToBottomPending;
    static double _orderDiagAt;

    // Rendered-window state (sliding virtualization). The mesh holds one slice
    // [winStart .. winEnd] of AllLines; winEnd lags the newest when scrolled up so
    // recent lines unload from the mesh. Tick re-derives the slice from the visible
    // range, then an anchor keeps the top line fixed across the mesh rebuild.
    static int _winStart;
    static int _winEnd;
    static int _appliedTrim;
    static bool _tailRequested;
    static bool _followTail = true;
    static bool _anchorTopPending;
    static int _anchorAllLines = -1;
    // Sub-line pixel offset of the viewport top inside the anchor line. Preserved
    // across a rebuild so the view does not snap to the top of the line.
    static float _anchorSubPixel;
    // Tick leaves a target slice pending. RebuildLog applies it, so a deferred
    // build never desyncs the stored window from the lines the mesh holds.
    static bool _windowMoved;
    static int _pendingWinStart;
    static int _pendingWinEnd;
    static float _lastScrollNorm = -1f;
    static int _ignoreScrollFrames;
    // Inertial glide state for the log, in content pixels from the top.
    static float _logOffsetTarget;
    static float _logOffsetApplied;
    // Set by a scrollbar seek so the loader does not override the seek window.
    static bool _seekPending;
    static double _shiftDiagAt;
    static double _rebuildDiagLogAt;
    static double _rebuildFailLogAt;
    static double _rebuildBackoffUntil;
    static int _rebuildFailCount;

    static readonly System.Collections.Generic.List<GameObject> _colorSwatchRows = new();
    static readonly Color[] _colorPalette =
    [
        new(0.949f, 0.922f, 0.859f, 1f),   // cream
        new(1f, 1f, 1f, 1f),               // white
        new(0.85f, 0.15f, 0.12f, 1f),   // red (bright, reads on dark)
        new(0.725f, 0.788f, 0.827f, 1f),   // steel
        new(0.35f, 0.85f, 0.45f, 1f),      // green
        new(1f, 0.60f, 0.20f, 1f),         // orange
        new(0.35f, 0.60f, 1f, 1f),         // blue
        new(0.682f, 0.486f, 0.922f, 1f),   // purple
        new(1f, 0.824f, 0.29f, 1f),        // gold
    ];
    static readonly string[] _colorRowNames =
    [
        "Global label", "Global text",
        "Local label", "Local text",
        "Clan label", "Clan text",
        "Whisper label", "Whisper text",
        "System label", "System text",
        "Your name", "Your messages",
        "Admin name", "Admin messages",
    ];
    static readonly ChatChannel[] _colorRowChannels =
    [
        ChatChannel.Global, ChatChannel.Global,
        ChatChannel.Local, ChatChannel.Local,
        ChatChannel.Clan, ChatChannel.Clan,
        ChatChannel.Whisper, ChatChannel.Whisper,
        ChatChannel.System, ChatChannel.System,
    ]; // indices 10, 11 are self name/messages; 12, 13 are admin name/messages; even rows are channel labels, odd are text

    const float MaxComposeH = 120f;
    const int MaxRenderChars = 16000;
    // Lines loaded beyond the visible range on each window move. A large chunk
    // lets a fast scroll cross the margin in fewer mesh rebuilds.
    const int LoadChunk = 80;
    // Inertial wheel glide. The log ScrollRect no longer moves on wheel, so the view
    // glides toward a pixel target we own. Raise the pixels per notch to scroll
    // further, raise the smoothing for a snappier glide.
    const float LogScrollPixelsPerNotch = 60f;
    const float LogScrollSmoothing = 25f;
    // Debug logging. Keep both false in normal play.
    const bool ScrollDiag = false;
    const bool RebuildDiag = false;
    // TEMP diagnostic: log the first and last rendered lines so the log order can be
    // read directly. Set to false to remove the log spam.
    const bool OrderDiag = true;

    struct SendTarget
    {
        public ChatChannel Channel;
        public NetworkId Target;
        public string Label;

        public SendTarget(ChatChannel channel, NetworkId target, string label)
        {
            Channel = channel;
            Target = target;
            Label = label;
        }
    }

    static readonly System.Collections.Generic.List<SendTarget> _cycle = new();
    static string _cycleSignature = string.Empty;
    static int _cycleIndex;

    static readonly System.Collections.Generic.List<string> _history = new();
    static int _historyIndex = -1;

    static readonly System.Collections.Generic.List<ChatLine> _renderedLines = new();
    static readonly System.Collections.Generic.List<int> _lineOffsets = new();
    static readonly System.Collections.Generic.List<int> _lineVisLens = new();
    static readonly System.Collections.Generic.List<int> _lineRawStarts = new();
    static bool _logClickArmed;
    static Vector2 _logDownPos;
    static TextMeshProUGUI? _copyLabel;
    static double _copyFeedbackUntil;
    static GameObject? _highlightObj;
    static Image? _highlight;
    static RectTransform? _highlightRect;
    static int _hoverLineIdx = -1;
    static double _highlightFlashUntil;
    static double _channelLabelFeedbackUntil;

    public static bool IsOpen => _open;
    public static bool IsTyping => _chatActive;

    static bool _moddedActive = true;
    public static bool IsModdedActive => _moddedActive;

    public static void ToggleModded() => SetModdedActive(!_moddedActive);

    public static void SetModdedActive(bool modded)
    {
        _moddedActive = modded;
        if (modded)
        {
            _chatActive = false;
            _viewSettings = false;
            _viewHelp = false;
            _hkArmedIndex = -1;
            SetOpen(true);
            MarkActivity();
        }
        else
        {
            SetOpen(false);
            NativeChatService.RestoreNative();
        }
    }

    public static bool IsPointerOverWindow
    {
        get
        {
            if (!_open || _rootRect == null) return false;
            Camera camera = Core.UiCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null!
                : Core.UiCanvas.worldCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(_rootRect, Input.mousePosition, camera);
        }
    }

    public static bool IsSettingsOpen => _viewSettings && _open && _moddedActive;
    public static bool IsPointerOverSettings => IsSettingsOpen && IsPointerOverWindow;
    public static bool IsScrollbarEngaged => _open && _moddedActive && _chatActive && !_viewSettings && _alpha > 0.01f && (IsPointerOverScrollbar() || _scrollbarDragging);

    // True when the modded UI owns the mouse wheel: the window is visible, the chat UI
    // is focused (typing) or showing Settings/Help, and the pointer is over the window.
    // The capture region is the whole window, in every view. Outside the window the
    // wheel belongs to the camera. This is the single gate for zoom suppression.
    public static bool CapturesWheel
    {
        get
        {
            try
            {
                if (!Core.HasInitialized) return false;
                if (Services.ConsoleGateService.IsConsoleOpen) return false;
                if (!_moddedActive || !_open || _alpha <= 0.01f) return false;
                if (!(_chatActive || IsSettingsOpen)) return false;
                return IsPointerOverWindow;
            }
            catch
            {
                return false;
            }
        }
    }

    public static void EnsureBuilt()
    {
        if (_built || !Core.HasInitialized || !Core.HasCanvas) return;

        try
        {
            if (!_subscribed)
            {
                _subscribed = true;
                UpdateDriver.Actions.Add(Tick);
                UpdateDriver.Actions.Add(InputLockService.Tick);
                UpdateDriver.Actions.Add(NativeChatService.Tick);
                UpdateDriver.Actions.Add(HistoryService.Tick);
                UpdateDriver.Actions.Add(Patches.InputSuppressionPatch.Tick);
                UpdateDriver.Actions.Add(SettingsService.Tick);
                UpdateDriver.Actions.Add(Services.ClanHudService.Tick);
                ChatDataService.LineCaptured += OnLineCaptured;
                HistoryService.Initialize();
                SettingsService.Initialize();
                Services.InputHistoryService.Initialize();
                _history.AddRange(Services.InputHistoryService.Entries);
            }

            Build();
            _built = true;
            Core.Log.LogInfo("[ChatPlus] Chat window built.");
            ApplyLayout();
            if (_moddedActive)
            {
                _chatActive = false;
                _focusPending = false;
                SetOpen(true);
            }
            _logDirty = true;
            _tailRequested = true;
            _recalcAfterOpen = Time.realtimeSinceStartupAsDouble + 0.2;
        }
        catch (Exception ex)
        {
            Core.Log.LogError($"[ChatPlus] Chat window build failed: {ex}");
        }
    }

    public static void ResetSession()
    {
        // Capture the field before it is nulled below, so teardown can deactivate it.
        TMP_InputField? compose = _compose;

        if (_root != null)
        {
            try { UnityEngine.Object.Destroy(_root); }
            catch { }
        }

        _root = null;
        _rootRect = null;
        _fade = null;
        _chatView = null;
        _settingsView = null;
        _helpView = null;
        _helpBtn = null;
        _helpBackBtn = null;
        _copyBtn = null;
        _bottomBar = null;
        _hkContainer = null;
        _hkHint = null;
        _log = null;
        _compose = null;
        _scroll = null;
        _scrollContent = null;
        _scrollbar = null;
        _scrollbarRect = null;
        _channelLabel = null;
        _channelBg = null;
        _composeBar = null;
        _hintBar = null;
        _railRt = null;
        _settingsBtnRt = null;
        _settingsBtnBg = null;
        _opacitySlider = null;
        _opacityValue = null;
        _inputOpacitySlider = null;
        _inputOpacityValue = null;
        _geomX = null;
        _geomY = null;
        _geomW = null;
        _geomH = null;
        _geomClan = null;
        _geomWSlider = null;
        _geomHSlider = null;
        _geomClanSlider = null;
        _sliderSyncGuard = false;
        _renderedLines.Clear();
        _lineOffsets.Clear();
        _lineVisLens.Clear();
        _lineRawStarts.Clear();
        _logClickArmed = false;
        _copyLabel = null;
        _copyFeedbackUntil = 0;
        _highlightObj = null;
        _highlight = null;
        _highlightRect = null;
_hoverLineIdx = -1;
        _forceHoverRecompute = false;
        _highlightFlashUntil = 0;
        _channelLabelFeedbackUntil = 0;

        _scrollBg = null;
        _composeBarBg = null;
        _inputBg = null;
        _placeholderTmp = null;
        _composeBarRect = null;
        _logScrollRect = null;
        _textAreaRect = null;
        _channelLabelRect = null;
        _composeH = InputH;
        _cycle.Clear();
        _cycleIndex = 0;
        _cycleSignature = string.Empty;

        _built = false;
        _open = false;
        _logDirty = false;
        _recalcAfterOpen = -1.0;
        _alpha = 0f;
        _lastActivityUtc = -1000.0;
        _chatActive = false;
        _viewSettings = false;
        _viewHelp = false;
        _wasTypingLastFrame = false;
        _hkArmedIndex = -1;
        _focusPending = false;
        _scrollbarDragging = false;
        _dragging = false;
        _gripRect = null;
        _gripBg = null;
        _hkContainer = null;
        _hkAddBtnRt = null;
        _hkHint = null;
        _settingsScroll = null;
        _settingsScrollbar = null;
        _settingsScrollbarRect = null;
        _settingsHandleGo = null;
        _helpScroll = null;
        _helpScrollbar = null;
        _helpScrollbarRect = null;
        _helpHandleGo = null;
        _settingsScrollbarDragging = false;
        _scrollbarHandleGo = null;
        _colorSwatchRows.Clear();
        _pendingHkIndex = -1;
        _pendingCmdIndex = -1;
        _hkCommandFields.Clear();

        // Window + rebuild state must reset with the session. Without this a stale
        // window or backoff from the previous session leaks into the next one.
        _winStart = 0;
        _winEnd = 0;
        _appliedTrim = 0;
        _tailRequested = true;
        _followTail = true;
        _anchorTopPending = false;
        _anchorAllLines = -1;
        _anchorSubPixel = 0f;
        _windowMoved = false;
        _pendingWinStart = 0;
        _pendingWinEnd = 0;
        _displayScrollSize = 0.2f;
        _lastScrollNorm = -1f;
        _ignoreScrollFrames = 0;
        _logOffsetTarget = 0f;
        _logOffsetApplied = 0f;
        _seekPending = false;
        _shiftDiagAt = 0;
        _scrollToBottomPending = false;
        _periodicRefreshCounter = 0;
        _rebuildDiagLogAt = 0;
        _rebuildFailLogAt = 0;
        _rebuildBackoffUntil = 0;
        _rebuildFailCount = 0;

        // Risky UI calls LAST, each isolated, so a throw during teardown can never
        // leave _chatActive/_built/_viewSettings stale for the next session.
        // `compose` was captured before _compose was nulled, so deactivation is real.
        // The Unity null check skips an object that was already destroyed with the HUD.
        try { if (compose != null) compose.DeactivateInputField(); } catch { }
        try
        {
            EventSystem es = EventSystem.current;
            if (es != null) es.SetSelectedGameObject(null);
        }
        catch { }
    }

    static void Build()
    {
        Transform parent = Core.UiCanvas.transform;
        int layer = Core.UiLayer;

        _root = UiFactory.Create("ChatPlusWindow", parent, layer);
        UiFactory.AnchorPoint(_root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(Services.SettingsService.WindowX, Services.SettingsService.WindowY), new Vector2(WinW, WinH));

        _chatView = UiFactory.Create("ChatView", _root.transform, layer);
        UiFactory.FillParent(_chatView);
        BuildChatView(_chatView.transform, layer);

        _settingsView = UiFactory.Create("SettingsView", _root.transform, layer);
        UiFactory.FillParent(_settingsView);
        BuildSettingsView(_settingsView.transform, layer);
        _settingsView.SetActive(false);

        _helpView = UiFactory.Create("HelpView", _root.transform, layer);
        UiFactory.FillParent(_helpView);
        BuildHelpView(_helpView.transform, layer);
        _helpView.SetActive(false);

        _bottomBar = UiFactory.Create("BottomBar", _root.transform, layer);
        UiFactory.FillParent(_bottomBar);
        BuildBottomBar(_bottomBar.transform, layer);
        _bottomBar.SetActive(false);

        _root.transform.SetAsLastSibling();
        Canvas topCanvas = _root.AddComponent<Canvas>();
        topCanvas.overrideSorting = true;
        topCanvas.sortingOrder = 5000;
        _root.AddComponent<GraphicRaycaster>();

        _fade = _root.AddComponent<CanvasGroup>();
        _fade.alpha = 0f;
        _fade.interactable = false;
        _fade.blocksRaycasts = false;

        _rootRect = _root.GetComponent<RectTransform>();
        _root.SetActive(false);
    }

    static void BuildChatView(Transform parent, int layer)
    {
        Scrollbar scrollbar = BuildLeftRail(parent, layer);
        BuildContentArea(parent, layer, scrollbar);
        BuildBottomStrip(parent, layer);
    }

    static Scrollbar BuildLeftRail(Transform parent, int layer)
    {
        GameObject rail = UiFactory.Create("LeftRail", parent, layer);
        RectTransform railRt = rail.GetComponent<RectTransform>();
        float railSide = Mirrored ? 1f : 0f;
        railRt.anchorMin = new Vector2(railSide, 0f);
        railRt.anchorMax = new Vector2(railSide, 1f);
        railRt.pivot = new Vector2(railSide, 0.5f);
        railRt.sizeDelta = new Vector2(RailW, -LabelH);
        railRt.anchoredPosition = new Vector2(0f, LabelH * 0.5f);

        GameObject railGrad = UiFactory.Create("RailGradient", rail.transform, layer);
        UiFactory.Stretch(railGrad, 0f, 0f, 0f, 0f);
        UiFactory.BottomFadeBackground(railGrad, new Color(0.039f, 0.039f, 0.051f, 0.85f));

        GameObject settingsBtn = UiFactory.Create("SettingsBtn", rail.transform, layer);
        RectTransform sbrt = settingsBtn.GetComponent<RectTransform>();
        sbrt.anchorMin = new Vector2(0f, 0f);
        sbrt.anchorMax = new Vector2(1f, 0f);
        sbrt.pivot = new Vector2(0.5f, 0f);
        sbrt.anchoredPosition = Vector2.zero;
        sbrt.sizeDelta = new Vector2(0f, InputH);
        _settingsBtnBg = UiFactory.Background(settingsBtn, Theme.ButtonBg);
        UiFactory.Button(settingsBtn, ToggleSettingsView);
        _settingsBtnRt = sbrt;

        GameObject iconGroup = UiFactory.Create("SettingsIcon", settingsBtn.transform, layer);
        Image wrench = UiFactory.Background(iconGroup, Theme.TextPrimary);
        wrench.sprite = UiFactory.WrenchSprite();
        wrench.type = Image.Type.Simple;
        wrench.raycastTarget = false;
        RectTransform ict = iconGroup.GetComponent<RectTransform>();
        ict.anchorMin = new Vector2(0.5f, 0.5f);
        ict.anchorMax = new Vector2(0.5f, 0.5f);
        ict.pivot = new Vector2(0.5f, 0.5f);
        ict.anchoredPosition = Vector2.zero;
        ict.sizeDelta = new Vector2(RailW, InputH - 2f);

        GameObject sb = UiFactory.Create("RailScrollbar", rail.transform, layer);
        RectTransform srt = sb.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 1f);
        srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot = new Vector2(0.5f, 1f);
        srt.anchoredPosition = Vector2.zero;
        srt.sizeDelta = new Vector2(0f, WinH - LabelH - InputH);
        _railRt = railRt;

        CanvasGroup cg = sb.AddComponent<CanvasGroup>();
        cg.ignoreParentGroups = true;
        cg.interactable = true;
        cg.blocksRaycasts = true;

        Image track = UiFactory.Background(sb, new Color(0f, 0f, 0f, 0f));
        track.raycastTarget = false;

        GameObject railLine = UiFactory.Create("RailLine", sb.transform, layer);
        RectTransform rlrt = railLine.GetComponent<RectTransform>();
        rlrt.anchorMin = new Vector2(0.5f, 0f);
        rlrt.anchorMax = new Vector2(0.5f, 1f);
        rlrt.pivot = new Vector2(0.5f, 0.5f);
        rlrt.sizeDelta = new Vector2(4f, 0f);
        Image railImg = UiFactory.Background(railLine, new Color(0.184f, 0.184f, 0.184f, 0.6f));
        railImg.raycastTarget = false;

        GameObject handleGo = UiFactory.Create("Handle", sb.transform, layer);
        RectTransform hrt = handleGo.GetComponent<RectTransform>();
        hrt.anchorMin = Vector2.zero;
        hrt.anchorMax = Vector2.one;
        hrt.offsetMin = new Vector2(4f, 0f);
        hrt.offsetMax = new Vector2(-4f, 0f);
        Image handleImg = UiFactory.Background(handleGo, new Color(0.45f, 0.45f, 0.45f, 0.55f));
        handleImg.raycastTarget = false;
        _scrollbarHandleGo = handleGo;

        Scrollbar scrollbar = sb.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = hrt;
        scrollbar.targetGraphic = handleImg;
        scrollbar.value = 1f;
        scrollbar.interactable = false;
        scrollbar.transition = Selectable.Transition.None;

        settingsBtn.transform.SetAsLastSibling();

        _scrollbarRect = srt;
        return scrollbar;
    }

    static void BuildContentArea(Transform parent, int layer, Scrollbar scrollbar)
    {
        GameObject area = UiFactory.Create("ContentArea", parent, layer);
        RectTransform art = area.GetComponent<RectTransform>();
        art.anchorMin = new Vector2(0f, 0f);
        art.anchorMax = new Vector2(1f, 1f);
        art.pivot = new Vector2(0.5f, 0.5f);
        art.offsetMin = Mirrored ? new Vector2(0f, LabelH) : new Vector2(RailW, LabelH);
        art.offsetMax = Mirrored ? new Vector2(-RailW, 0f) : new Vector2(0f, 0f);

        BuildLog(area.transform, layer, ContentWidth, scrollbar);
        BuildCompose(area.transform, layer, ContentWidth);
    }

    static void BuildBottomStrip(Transform parent, int layer)
    {
        GameObject strip = UiFactory.Create("BottomStrip", parent, layer);
        RectTransform srt = strip.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 0f);
        srt.anchorMax = new Vector2(1f, 0f);
        srt.pivot = new Vector2(0.5f, 0f);
        srt.anchoredPosition = Vector2.zero;
        srt.sizeDelta = new Vector2(0f, LabelH);

        BuildGrip(strip.transform, layer);
        BuildChannelLabel(strip.transform, layer, WinW);
    }

    static void BuildGrip(Transform parent, int layer)
    {
        GameObject grip = UiFactory.Create("DragGrip", parent, layer);
        RectTransform grt = grip.GetComponent<RectTransform>();
        float gripSide = Mirrored ? 1f : 0f;
        grt.anchorMin = new Vector2(gripSide, 0f);
        grt.anchorMax = new Vector2(gripSide, 0f);
        grt.pivot = new Vector2(gripSide, 0f);
        grt.anchoredPosition = Vector2.zero;
        grt.sizeDelta = new Vector2(RailW, LabelH);
        _gripRect = grt;

        GameObject face = UiFactory.Create("DragGripFace", grip.transform, layer);
        RectTransform frt = face.GetComponent<RectTransform>();
        frt.anchorMin = new Vector2(0.5f, 0.5f);
        frt.anchorMax = new Vector2(0.5f, 0.5f);
        frt.pivot = new Vector2(0.5f, 0.5f);
        frt.anchoredPosition = Vector2.zero;
        frt.sizeDelta = new Vector2(RailW - 4f, LabelH - 4f);
        _gripBg = UiFactory.Background(face, new Color(1f, 1f, 1f, 0.08f));
        _gripBg.raycastTarget = false;

        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 2; col++)
            {
                GameObject dot = UiFactory.Create("Dot", face.transform, layer);
                RectTransform drt = dot.GetComponent<RectTransform>();
                drt.anchorMin = new Vector2(0.5f, 0.5f);
                drt.anchorMax = new Vector2(0.5f, 0.5f);
                drt.pivot = new Vector2(0.5f, 0.5f);
                drt.anchoredPosition = new Vector2((col - 0.5f) * 5f, (row - 1f) * 4f);
                drt.sizeDelta = new Vector2(3f, 3f);
                Image dotImg = UiFactory.Background(dot, new Color(1f, 1f, 1f, 1f));
                dotImg.raycastTarget = false;
            }
        }
    }

    static void BuildLog(Transform parent, int layer, float width, Scrollbar scrollbar)
    {
        GameObject scrollRoot = UiFactory.Create("Log", parent, layer);
        UiFactory.AnchorPoint(scrollRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(width, WinH - InputH - LabelH));
        _scrollBg = UiFactory.Background(scrollRoot, new Color(0.039f, 0.039f, 0.051f, SettingsService.LogBackgroundOpacity));
        _scrollBg.raycastTarget = false;

        GameObject viewport = UiFactory.Create("Viewport", scrollRoot.transform, layer);
        UiFactory.FillParent(viewport);
        Image viewportImage = UiFactory.Background(viewport, new Color(0f, 0f, 0f, 0f));
        viewportImage.raycastTarget = false;
        viewport.AddComponent<RectMask2D>();

        _logScrollRect = scrollRoot.GetComponent<RectTransform>();

        GameObject content = UiFactory.Create("Content", viewport.transform, layer);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Mirrored ? new Vector2(6f, 0f) : new Vector2(8f, 0f);
        contentRect.offsetMax = Mirrored ? new Vector2(-8f, 0f) : new Vector2(-6f, 0f);
        contentRect.anchoredPosition = Vector2.zero;

        _log = UiFactory.Label(content, string.Empty, Theme.FontSize, Theme.TextPrimary);
        _log.enableWordWrapping = true;
        _log.alignment = TextAlignmentOptions.TopLeft;
        _log.margin = new Vector4(0f, 0f, 0f, 12f);

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollRoot.AddComponent<ScrollRect>();
        scroll.content = contentRect;
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        // The wheel is handled manually (the log glides toward a pixel target), so the
        // native ScrollRect must not also move the content. Clamping and content drag
        // still run.
        scroll.scrollSensitivity = 0f;
        // The rail scrollbar is driven manually from the global line position, so the
        // ScrollRect must not manage it. The ScrollRect still scrolls its own content.
        scroll.verticalScrollbar = null;

        _scroll = scroll;
        _scrollContent = contentRect;
        _scrollbar = scrollbar;

        _highlightObj = UiFactory.Create("HoverHighlight", _log.rectTransform.transform, layer);
        RectTransform hrt = _highlightObj.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0f, 1f);
        hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot = new Vector2(0.5f, 1f);
        hrt.anchoredPosition = Vector2.zero;
        hrt.sizeDelta = Vector2.zero;
        _highlight = UiFactory.Background(_highlightObj, new Color(1f, 1f, 1f, 0.09f));
        _highlight.raycastTarget = false;
        _highlightRect = hrt;
        _highlightObj.transform.SetAsFirstSibling();
        _highlightObj.SetActive(false);
    }

    static void BuildCompose(Transform parent, int layer, float width)
    {
        _composeBar = UiFactory.Create("Compose", parent, layer);
        UiFactory.AnchorPoint(_composeBar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(width, InputH));
        _composeBarBg = UiFactory.Background(_composeBar, Theme.InputBg);
        _composeBarBg.raycastTarget = false;
        _composeBarRect = _composeBar.GetComponent<RectTransform>();

        GameObject inputRoot = UiFactory.Create("Input", _composeBar.transform, layer);
        UiFactory.Stretch(inputRoot, 6, 4, 4, 4);
        Image inputImage = UiFactory.Background(inputRoot, Theme.InputBg);
        inputImage.raycastTarget = false;
        _inputBg = inputImage;

        GameObject textArea = UiFactory.Create("TextArea", inputRoot.transform, layer);
        UiFactory.Stretch(textArea, 8, 8, 3, 3);
        _textAreaRect = textArea.GetComponent<RectTransform>();

        GameObject placeholderGO = UiFactory.Create("Placeholder", textArea.transform, layer);
        UiFactory.FillParent(placeholderGO);
        TextMeshProUGUI placeholder = UiFactory.Label(placeholderGO, "Type a message", Theme.FontSizeSmall, Theme.TextMuted);
        placeholder.enableWordWrapping = true;
        _placeholderTmp = placeholder;

        GameObject textGO = UiFactory.Create("Text", textArea.transform, layer);
        UiFactory.FillParent(textGO);
        TextMeshProUGUI text = UiFactory.Label(textGO, string.Empty, Theme.FontSizeSmall, Theme.TextPrimary);
        text.enableWordWrapping = true;

        _compose = UiFactory.InputField(inputRoot, text, placeholder, textArea.GetComponent<RectTransform>(), inputImage);
        _compose.lineType = TMP_InputField.LineType.MultiLineSubmit;
        UiFactory.AddSubmitListener(_compose, SubmitCompose);

        _composeBar.SetActive(false);

        _hintBar = UiFactory.Create("HintBar", parent, layer);
        UiFactory.AnchorPoint(_hintBar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(width, InputH));
        if (Mirrored)
            UiFactory.RightFadeBackground(_hintBar, new Color(0.039f, 0.039f, 0.051f, 0.85f));
        else
            UiFactory.LeftFadeBackground(_hintBar, new Color(0.039f, 0.039f, 0.051f, 0.85f));

        GameObject hintText = UiFactory.Create("HintText", _hintBar.transform, layer);
        TextMeshProUGUI hint;
        if (Mirrored)
        {
            UiFactory.AnchorPoint(hintText, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(width - 28f, InputH - 8f));
            hint = UiFactory.Label(hintText, "Press Enter to send a message", Theme.FontSizeSmall, Theme.TextMuted, TextAlignmentOptions.Right);
        }
        else
        {
            UiFactory.AnchorPoint(hintText, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(width - 28f, InputH - 8f));
            hint = UiFactory.Label(hintText, "Press Enter to send a message", Theme.FontSizeSmall, Theme.TextMuted, TextAlignmentOptions.Left);
        }
        hint.verticalAlignment = VerticalAlignmentOptions.Middle;
        hint.raycastTarget = false;
        hint.enableWordWrapping = false;

        _hintBar.SetActive(false);
    }

    static void BuildChannelLabel(Transform parent, int layer, float width)
    {
        GameObject bg = UiFactory.Create("SendChannelBg", parent, layer);
        UiFactory.AnchorPoint(bg, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(width, LabelH));
        if (Mirrored)
            UiFactory.RightFadeBackground(bg, new Color(0.039f, 0.039f, 0.051f, 0.85f), 1f);
        else
            UiFactory.LeftFadeBackground(bg, new Color(0.039f, 0.039f, 0.051f, 0.85f), 1f);
        _channelBg = bg;

        GameObject label = UiFactory.Create("SendChannelLabel", parent, layer);
        if (Mirrored)
        {
            UiFactory.AnchorPoint(label, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-(RailW + 8f), 0f), new Vector2(width - RailW - 24f, LabelH));
            _channelLabel = UiFactory.Label(label, string.Empty, LabelFontSize, Theme.TextAccent, TextAlignmentOptions.Right);
        }
        else
        {
            UiFactory.AnchorPoint(label, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(RailW + 8f, 0f), new Vector2(width - RailW - 24f, LabelH));
            _channelLabel = UiFactory.Label(label, string.Empty, LabelFontSize, Theme.TextAccent, TextAlignmentOptions.Left);
        }
        _channelLabel.verticalAlignment = VerticalAlignmentOptions.Middle;
        _channelLabelRect = label.GetComponent<RectTransform>();
        UpdateChannelLabel();
        bg.SetActive(false);
        label.SetActive(false);
    }

    static void BuildSettingsView(Transform parent, int layer)
    {
        GameObject bg = UiFactory.Create("SettingsBg", parent, layer);
        UiFactory.FillParent(bg);
        Image bgImage = UiFactory.Background(bg, Theme.SettingsBgWarm);
        bgImage.raycastTarget = true;

        GameObject title = UiFactory.Create("SettingsTitle", parent, layer);
        UiFactory.AnchorPoint(title, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -12f), new Vector2(200f, 22f));
        UiFactory.Label(title, "Settings", 16f, Theme.TextPrimary);

        GameObject titleRule = UiFactory.Create("SettingsRule", parent, layer);
        UiFactory.AnchorPoint(titleRule, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -40f), new Vector2(200f, 1f));
        UiFactory.Hairline(titleRule, Theme.DividerRed);

        GameObject mirrorBtn = UiFactory.Create("MirrorToggle", parent, layer);
        UiFactory.AnchorPoint(mirrorBtn, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -8f), new Vector2(120f, 20f));
        UiFactory.Background(mirrorBtn, Theme.ButtonBg);
        UiFactory.Button(mirrorBtn, ToggleMirrorLayout);

        GameObject mirrorLabel = UiFactory.Create("MirrorLabel", mirrorBtn.transform, layer);
        UiFactory.FillParent(mirrorLabel);
        UiFactory.Label(mirrorLabel, MirrorToggleText(), Theme.FontSizeSmall, Theme.TextPrimary, TextAlignmentOptions.Center);

        // Scroll body between the title and the bottom button bar.
        GameObject scrollRoot = UiFactory.Create("SettingsScroll", parent, layer);
        RectTransform srt = scrollRoot.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 0f);
        srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot = new Vector2(0.5f, 0.5f);
        srt.offsetMin = new Vector2(0f, 40f);
        srt.offsetMax = new Vector2(0f, -46f);

        GameObject viewport = UiFactory.Create("SettingsViewport", scrollRoot.transform, layer);
        UiFactory.FillParent(viewport);
        Image vpImg = UiFactory.Background(viewport, new Color(0f, 0f, 0f, 0f));
        vpImg.raycastTarget = false;
        viewport.AddComponent<RectMask2D>();

        GameObject content = UiFactory.Create("SettingsContent", viewport.transform, layer);
        RectTransform cRt = content.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0f, 1f);
        cRt.anchorMax = new Vector2(1f, 1f);
        cRt.pivot = new Vector2(0f, 1f);
        cRt.offsetMin = new Vector2(10f, 0f);
        cRt.offsetMax = new Vector2(-10f, 0f);
        cRt.anchoredPosition = Vector2.zero;
        cRt.sizeDelta = new Vector2(0f, 1200f);

        ScrollRect scroll = scrollRoot.AddComponent<ScrollRect>();
        scroll.content = cRt;
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 20f;

        _settingsScroll = scroll;
        BuildScrollbarTrack(scrollRoot.transform, viewport.transform, scroll, layer, out _settingsHandleGo, out _settingsScrollbarRect, out _settingsScrollbar);

        // --- Fixed rows (top-anchored inside content) ---
        GameObject opacityLabel = UiFactory.Create("OpacityLabel", content.transform, layer);
        UiFactory.AnchorPoint(opacityLabel, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(8f, -14f), new Vector2(100f, 18f));
        UiFactory.Label(opacityLabel, "Log opacity", Theme.FontSizeSmall, Theme.TextMuted);

        GameObject sliderRoot = UiFactory.Create("OpacitySlider", content.transform, layer);
        UiFactory.AnchorPoint(sliderRoot, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(110f, -14f), new Vector2(160f, 14f));
        UiFactory.Background(sliderRoot, Theme.FieldBg);

        GameObject fill = UiFactory.Create("Fill", sliderRoot.transform, layer);
        RectTransform fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        Image fillImg = UiFactory.Background(fill, Theme.Steel);
        fillImg.raycastTarget = false;

        GameObject handle = UiFactory.Create("Handle", sliderRoot.transform, layer);
        RectTransform handleRt = handle.GetComponent<RectTransform>();
        handleRt.anchorMin = new Vector2(0f, 0.5f);
        handleRt.anchorMax = new Vector2(0f, 0.5f);
        handleRt.pivot = new Vector2(0.5f, 0.5f);
        handleRt.sizeDelta = new Vector2(6f, 8f);
        handleRt.anchoredPosition = Vector2.zero;
        Image handleImg = UiFactory.Background(handle, new Color(0.725f, 0.788f, 0.827f, 1f));
        handleImg.raycastTarget = true;

        _opacitySlider = UiFactory.AddHorizontalSlider(sliderRoot, handleRt, fillRt, handleImg, Services.SettingsService.LogBackgroundOpacity, OnOpacityChanged);

        GameObject pct = UiFactory.Create("OpacityPct", content.transform, layer);
        UiFactory.AnchorPoint(pct, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(276f, -14f), new Vector2(44f, 18f));
        _opacityValue = UiFactory.Label(pct, FormatPercent(Services.SettingsService.LogBackgroundOpacity), Theme.FontSizeSmall, Theme.TextPrimary, TextAlignmentOptions.Left);

        GameObject inputOpacityLabel = UiFactory.Create("InputOpacityLabel", content.transform, layer);
        UiFactory.AnchorPoint(inputOpacityLabel, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(8f, -40f), new Vector2(100f, 18f));
        UiFactory.Label(inputOpacityLabel, "Input opacity", Theme.FontSizeSmall, Theme.TextMuted);

        GameObject inputSliderRoot = UiFactory.Create("InputOpacitySlider", content.transform, layer);
        UiFactory.AnchorPoint(inputSliderRoot, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(110f, -40f), new Vector2(160f, 14f));
        UiFactory.Background(inputSliderRoot, Theme.FieldBg);

        GameObject inputFill = UiFactory.Create("InputFill", inputSliderRoot.transform, layer);
        RectTransform inputFillRt = inputFill.GetComponent<RectTransform>();
        inputFillRt.anchorMin = Vector2.zero;
        inputFillRt.anchorMax = Vector2.one;
        inputFillRt.offsetMin = Vector2.zero;
        inputFillRt.offsetMax = Vector2.zero;
        Image inputFillImg = UiFactory.Background(inputFill, Theme.Steel);
        inputFillImg.raycastTarget = false;

        GameObject inputHandle = UiFactory.Create("InputHandle", inputSliderRoot.transform, layer);
        RectTransform inputHandleRt = inputHandle.GetComponent<RectTransform>();
        inputHandleRt.anchorMin = new Vector2(0f, 0.5f);
        inputHandleRt.anchorMax = new Vector2(0f, 0.5f);
        inputHandleRt.pivot = new Vector2(0.5f, 0.5f);
        inputHandleRt.sizeDelta = new Vector2(6f, 8f);
        inputHandleRt.anchoredPosition = Vector2.zero;
        Image inputHandleImg = UiFactory.Background(inputHandle, new Color(0.725f, 0.788f, 0.827f, 1f));
        inputHandleImg.raycastTarget = true;

        _inputOpacitySlider = UiFactory.AddHorizontalSlider(inputSliderRoot, inputHandleRt, inputFillRt, inputHandleImg, Services.SettingsService.InputBackgroundOpacity, OnInputOpacityChanged);

        GameObject inputPct = UiFactory.Create("InputOpacityPct", content.transform, layer);
        UiFactory.AnchorPoint(inputPct, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(276f, -40f), new Vector2(44f, 18f));
        _inputOpacityValue = UiFactory.Label(inputPct, FormatPercent(Services.SettingsService.InputBackgroundOpacity), Theme.FontSizeSmall, Theme.TextPrimary, TextAlignmentOptions.Left);

        _geomX = BuildGeometryRow(content.transform, layer, "Pos X", -72f, () => Services.SettingsService.WindowX, v => { Services.SettingsService.WindowX = v; ApplyLayout(); });
        _geomY = BuildGeometryRow(content.transform, layer, "Pos Y", -98f, () => Services.SettingsService.WindowY, v => { Services.SettingsService.WindowY = v; ApplyLayout(); });
        _geomW = BuildGeometrySliderRow(content.transform, layer, "Width", -124f, () => Services.SettingsService.WindowWidth, v => { Services.SettingsService.WindowWidth = v; ApplyLayout(); }, 300f, 2000f, ref _geomWSlider);
        _geomH = BuildGeometrySliderRow(content.transform, layer, "Height", -150f, () => Services.SettingsService.WindowHeight, v => { Services.SettingsService.WindowHeight = v; ApplyLayout(); }, 200f, 1200f, ref _geomHSlider);
        _geomClan = BuildGeometrySliderRow(content.transform, layer, "Clan up", -176f, () => Services.SettingsService.ClanListYOffset, v => { Services.SettingsService.ClanListYOffset = v; }, -500f, 500f, ref _geomClanSlider);

        // --- Colors section (inline) ---
        GameObject colorHint = UiFactory.Create("ColorsHint", content.transform, layer);
        UiFactory.AnchorPoint(colorHint, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(8f, -202f), new Vector2(200f, 18f));
        UiFactory.Label(colorHint, "Colors", 13f, Theme.TextPrimary);

        GameObject colorRule = UiFactory.Create("ColorsRule", content.transform, layer);
        UiFactory.AnchorPoint(colorRule, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(8f, -216f), new Vector2(200f, 1f));
        UiFactory.Hairline(colorRule, Theme.DividerRed);

        _colorSwatchRows.Clear();
        float colorY = -230f;
        float lastStripY = colorY;
        for (int row = 0; row < _colorRowNames.Length; row++)
        {
            if (ColorGroupForRow(row) is ChatColorGroup scopeGroup)
            {
                float stripY = colorY - 21f;
                BuildColorGroupBackground(content.transform, layer, colorY, stripY);
                BuildColorSwatchRow(content.transform, layer, row, colorY);
                lastStripY = stripY;
                BuildColorChannelStrip(content.transform, layer, scopeGroup, stripY);
                colorY -= 52f;
            }
            else
            {
                BuildColorSwatchRow(content.transform, layer, row, colorY);
                colorY -= 24f;
            }
        }
        RefreshColorSelection();

        // The Display header starts 22 pixels below the last color strip.
        float displayHintY = lastStripY - 22f;

        // --- Display section ---
        GameObject dispHint = UiFactory.Create("DisplayHint", content.transform, layer);
        UiFactory.AnchorPoint(dispHint, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(8f, displayHintY), new Vector2(200f, 18f));
        UiFactory.Label(dispHint, "Display", 13f, Theme.TextPrimary);

        GameObject dispRule = UiFactory.Create("DisplayRule", content.transform, layer);
        UiFactory.AnchorPoint(dispRule, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(8f, displayHintY - 14f), new Vector2(200f, 1f));
        UiFactory.Hairline(dispRule, Theme.DividerRed);

        BuildToggleRow(content.transform, layer, "Timestamp", displayHintY - 28f,
            () => Services.SettingsService.ShowTimestamp,
            v => { Services.SettingsService.ShowTimestamp = v; Services.SettingsService.MarkDirty(); RebuildLog(); });
        BuildToggleRow(content.transform, layer, "Channel tag", displayHintY - 52f,
            () => Services.SettingsService.ShowChannelIndicator,
            v => { Services.SettingsService.ShowChannelIndicator = v; Services.SettingsService.MarkDirty(); RebuildLog(); });

        // --- Hotkeys section (inline) ---
        _hkHint = UiFactory.Label(
            UiFactory.Create("HotkeysHint", content.transform, layer),
            "Hotkeys", 13f, Theme.TextPrimary);
        RectTransform hkrt = _hkHint.GetComponent<RectTransform>();
        hkrt.anchorMin = new Vector2(0f, 1f);
        hkrt.anchorMax = new Vector2(1f, 1f);
        hkrt.pivot = new Vector2(0f, 0.5f);
        hkrt.anchoredPosition = new Vector2(8f, -(HKFixedTop - 22f));
        hkrt.sizeDelta = new Vector2(WinW - 36f, 18f);

        GameObject hkRule = UiFactory.Create("HotkeysRule", content.transform, layer);
        UiFactory.AnchorPoint(hkRule, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(8f, -(HKFixedTop - 8f)), new Vector2(200f, 1f));
        UiFactory.Hairline(hkRule, Theme.DividerRed);

        GameObject rowsContainer = UiFactory.Create("HotkeysRows", content.transform, layer);
        RectTransform rowsRt = rowsContainer.GetComponent<RectTransform>();
        rowsRt.anchorMin = new Vector2(0f, 1f);
        rowsRt.anchorMax = new Vector2(1f, 1f);
        rowsRt.pivot = new Vector2(0f, 1f);
        rowsRt.anchoredPosition = new Vector2(8f, -HKFixedTop);
        rowsRt.sizeDelta = new Vector2(-16f, 0f);
        _hkContainer = rowsRt;

        GameObject addBtn = UiFactory.Create("AddHotkey", content.transform, layer);
        RectTransform addRt = addBtn.GetComponent<RectTransform>();
        addRt.anchorMin = new Vector2(0f, 1f);
        addRt.anchorMax = new Vector2(0f, 1f);
        addRt.pivot = new Vector2(0f, 1f);
        addRt.anchoredPosition = new Vector2(8f, 0f);
        addRt.sizeDelta = new Vector2(110f, 24f);
        UiFactory.Background(addBtn, Theme.ButtonBg);
        UiFactory.Button(addBtn, AddHotkey);
        _hkAddBtnRt = addRt;
        GameObject addLabel = UiFactory.Create("AddHotkeyLabel", addBtn.transform, layer);
        UiFactory.FillParent(addLabel);
        UiFactory.Label(addLabel, "+ Add Hotkey", Theme.FontSizeSmall, Theme.TextPrimary, TextAlignmentOptions.Center);

        RebuildHotkeyRows();
    }

    static void BuildBottomBar(Transform parent, int layer)
    {
        _helpBtn = UiFactory.Create("HelpBtn", parent, layer);
        UiFactory.AnchorPoint(_helpBtn, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, 8f), new Vector2(76f, 24f));
        UiFactory.Background(_helpBtn, Theme.ButtonBg);
        UiFactory.Button(_helpBtn, ShowHelpPage);
        GameObject helpLabel = UiFactory.Create("HelpBtnLabel", _helpBtn.transform, layer);
        UiFactory.FillParent(helpLabel);
        UiFactory.Label(helpLabel, "Help", Theme.FontSizeSmall, Theme.TextPrimary, TextAlignmentOptions.Center);

        _helpBackBtn = UiFactory.Create("HelpBackBtn", parent, layer);
        UiFactory.AnchorPoint(_helpBackBtn, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, 8f), new Vector2(76f, 24f));
        UiFactory.Background(_helpBackBtn, Theme.ButtonBg);
        UiFactory.Button(_helpBackBtn, ShowSettingsPage);
        GameObject backLabel = UiFactory.Create("HelpBackLabel", _helpBackBtn.transform, layer);
        UiFactory.FillParent(backLabel);
        UiFactory.Label(backLabel, "Back", Theme.FontSizeSmall, Theme.TextPrimary, TextAlignmentOptions.Center);
        _helpBackBtn.SetActive(false);

        _copyBtn = UiFactory.Create("CopyChat", parent, layer);
        UiFactory.AnchorPoint(_copyBtn, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(94f, 8f), new Vector2(110f, 24f));
        UiFactory.Background(_copyBtn, Theme.ButtonBg);
        UiFactory.Button(_copyBtn, CopyAllToClipboard);
        GameObject copyLabel = UiFactory.Create("CopyChatLabel", _copyBtn.transform, layer);
        UiFactory.FillParent(copyLabel);
        _copyLabel = UiFactory.Label(copyLabel, "Copy chat", Theme.FontSizeSmall, Theme.TextPrimary, TextAlignmentOptions.Center);

        GameObject done = UiFactory.Create("Done", parent, layer);
        UiFactory.AnchorPoint(done, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-10f, 8f), new Vector2(56f, 24f));
        UiFactory.Background(done, Theme.ButtonBg);
        UiFactory.Button(done, ToggleSettingsView);
        GameObject doneLabel = UiFactory.Create("DoneLabel", done.transform, layer);
        UiFactory.FillParent(doneLabel);
        UiFactory.Label(doneLabel, "Done", Theme.FontSizeSmall, Theme.TextPrimary, TextAlignmentOptions.Center);
    }

    static void BuildHelpView(Transform parent, int layer)
    {
        GameObject bg = UiFactory.Create("HelpBg", parent, layer);
        UiFactory.FillParent(bg);
        Image bgImage = UiFactory.Background(bg, Theme.SettingsBgWarm);
        bgImage.raycastTarget = true;

        GameObject title = UiFactory.Create("HelpTitle", parent, layer);
        UiFactory.AnchorPoint(title, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -12f), new Vector2(200f, 22f));
        UiFactory.Label(title, "Help", 16f, Theme.TextPrimary);

        GameObject titleRule = UiFactory.Create("HelpRule", parent, layer);
        UiFactory.AnchorPoint(titleRule, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -40f), new Vector2(200f, 1f));
        UiFactory.Hairline(titleRule, Theme.DividerRed);

        GameObject scrollRoot = UiFactory.Create("HelpScroll", parent, layer);
        RectTransform srt = scrollRoot.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 0f);
        srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot = new Vector2(0.5f, 0.5f);
        srt.offsetMin = new Vector2(0f, 40f);
        srt.offsetMax = new Vector2(0f, -46f);

        GameObject viewport = UiFactory.Create("HelpViewport", scrollRoot.transform, layer);
        UiFactory.FillParent(viewport);
        Image vpImg = UiFactory.Background(viewport, new Color(0f, 0f, 0f, 0f));
        vpImg.raycastTarget = false;
        viewport.AddComponent<RectMask2D>();

        GameObject content = UiFactory.Create("HelpContent", viewport.transform, layer);
        RectTransform cRt = content.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0f, 1f);
        cRt.anchorMax = new Vector2(1f, 1f);
        cRt.pivot = new Vector2(0f, 1f);
        cRt.offsetMin = new Vector2(10f, 0f);
        cRt.offsetMax = new Vector2(-10f, 0f);
        cRt.anchoredPosition = Vector2.zero;

        ScrollRect scroll = scrollRoot.AddComponent<ScrollRect>();
        scroll.content = cRt;
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 20f;
        _helpScroll = scroll;
        BuildScrollbarTrack(scrollRoot.transform, viewport.transform, scroll, layer, out _helpHandleGo, out _helpScrollbarRect, out _helpScrollbar);

        float y = -8f;
        AddHelpHeader(content.transform, layer, "Chat", ref y);
        AddHelpBody(content.transform, layer, "Enter focuses the box and sends. Empty Enter or Escape defocuses without closing. F6 swaps between ChatPlus and the vanilla chat.", ref y);
        AddHelpHeader(content.transform, layer, "Channels", ref y);
        AddHelpBody(content.transform, layer, "TAB cycles the send channel. The label below the input always shows the active one. Some servers restrict Global chat. Messages are capped at 490 bytes, roughly 480 normal characters; accented letters, emoji, and other symbols count as more.", ref y);
        AddHelpHeader(content.transform, layer, "Recall", ref y);
        AddHelpBody(content.transform, layer, "Up and Down cycle your sent lines, newest first. Recall persists across sessions up to 1000 lines.", ref y);
        AddHelpHeader(content.transform, layer, "Hotkeys", ref y);
        AddHelpBody(content.transform, layer, "Add entries in Settings. Click the key box and press a key or Ctrl, Alt, Shift combo. Each entry can hold a list of commands or messages; they fire to the current channel, one after another, while the chat box is not focused. The checkbox right of the key box toggles the entry on and off (on by default). Use \"+ Add command\" for more lines and the small X to remove one. The right-side X deletes the whole entry.", ref y);
        AddHelpHeader(content.transform, layer, "Copy", ref y);
        AddHelpBody(content.transform, layer, "Click any chat line to copy it with its timestamp. Click \"Copy chat\" to copy the whole log.", ref y);
        AddHelpHeader(content.transform, layer, "Admins", ref y);
        AddHelpBody(content.transform, layer, "A player who is an admin shows an [ADMIN] tag after their name. The admin name and admin message colors are set with the \"Admin name\" and \"Admin messages\" swatches in Settings. The admin colors also apply to your own lines when you are an admin. Each special color row has an \"Applies to\" switch row for Global, Local, Clan, and Whisper. A channel with a switch off falls back to the next color, then to the channel color.", ref y);
        AddHelpHeader(content.transform, layer, "Look and layout", ref y);
        AddHelpBody(content.transform, layer, "Sliders set log and input transparency. The position fields move and size the window. Grab the dotted grip in either bottom corner to drag. The \"Right side\" button mirrors the layout. Grab the chat history and drag to scroll it; the wheel also scrolls. Timestamps and channel tags (like [L], [G]) can be toggled in Settings.", ref y);
        AddHelpHeader(content.transform, layer, "Miscellaneous", ref y);
        AddHelpBody(content.transform, layer, "The window fades after 15 seconds idle.", ref y);

        cRt.sizeDelta = new Vector2(0f, -y + 16f);
    }

    static void AddHelpHeader(Transform content, int layer, string text, ref float y)
    {
        y -= 10f;
        GameObject go = UiFactory.Create("HelpHeader", content, layer);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(8f, y);
        rt.sizeDelta = new Vector2(-16f, 20f);
        UiFactory.Label(go, text, 13f, Theme.TextPrimary);
        y -= 22f;
    }

    static void AddHelpBody(Transform content, int layer, string text, ref float y)
    {
        GameObject go = UiFactory.Create("HelpBody", content, layer);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(8f, y);
        TextMeshProUGUI tmp = UiFactory.Label(go, text, Theme.FontSizeSmall, Theme.TextMuted);
        tmp.enableWordWrapping = true;
        float height = tmp.GetPreferredValues(text, WinW - 36f, 0f).y + 4f;
        rt.sizeDelta = new Vector2(-16f, height);
        y -= height + 4f;
    }

    static void OnOpacityChanged(float value)
    {
        SettingsService.LogBackgroundOpacity = value;
        if (_opacityValue != null) _opacityValue.text = FormatPercent(value);
    }

    static void OnInputOpacityChanged(float value)
    {
        SettingsService.InputBackgroundOpacity = value;
        if (_inputOpacityValue != null) _inputOpacityValue.text = FormatPercent(value);
    }

    // Builds a track + handle pair (and a Scrollbar wired to `scroll`) at the inner
    // edge of the settings scroll area. Track always visible; handle toggled by
    // UpdateEmptyScrollbarHandles when the content has no overflow.
    static void BuildScrollbarTrack(Transform parent, Transform viewport, ScrollRect scroll, int layer, out GameObject? handleGo, out RectTransform? barRect, out Scrollbar? scrollbarOut)
    {
        handleGo = null;
        barRect = null;
        scrollbarOut = null;
        float side = Mirrored ? 0f : 1f;
        GameObject bar = UiFactory.Create("SettingsTrack", parent, layer);
        RectTransform brt = bar.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(side, 0f);
        brt.anchorMax = new Vector2(side, 1f);
        brt.pivot = new Vector2(side, 0.5f);
        brt.anchoredPosition = new Vector2(side == 0f ? 2f : -2f, 0f);
        brt.sizeDelta = new Vector2(6f, 0f);
        Image trackImg = UiFactory.Background(bar, new Color(0.184f, 0.184f, 0.184f, 0.4f));
        trackImg.raycastTarget = false;
        barRect = brt;

        GameObject hnd = UiFactory.Create("SettingsHandle", bar.transform, layer);
        RectTransform hrt = hnd.GetComponent<RectTransform>();
        hrt.anchorMin = Vector2.zero;
        hrt.anchorMax = Vector2.one;
        hrt.offsetMin = Vector2.zero;
        hrt.offsetMax = Vector2.zero;
        Image hImg = UiFactory.Background(hnd, new Color(0.45f, 0.45f, 0.45f, 0.55f));
        hImg.raycastTarget = false;

        Scrollbar sbar = bar.AddComponent<Scrollbar>();
        sbar.direction = Scrollbar.Direction.BottomToTop;
        sbar.handleRect = hrt;
        sbar.targetGraphic = hImg;
        sbar.interactable = true;

        scroll.verticalScrollbar = sbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        handleGo = hnd;
        scrollbarOut = sbar;
    }

    static string FormatPercent(float value)
        => $"{Mathf.RoundToInt(value * 100f)}%";

    static void AddHotkey()
    {
        SettingsService.Hotkeys.Add(new Services.SettingsService.HotkeyEntry());
        SettingsService.MarkDirty();
        RebuildHotkeyRows();
    }

    static void RemoveHotkey(int index)
    {
        if (index < 0 || index >= SettingsService.Hotkeys.Count) return;
        if (_hkArmedIndex == index) _hkArmedIndex = -1;
        else if (_hkArmedIndex > index) _hkArmedIndex--;
        SettingsService.Hotkeys.RemoveAt(index);
        SettingsService.MarkDirty();
        RebuildHotkeyRows();
    }

    const float HKHeaderH = 26f;
    const float HkCmdH = 26f;
    const float HKAddRowH = 24f;
    const float HkGapH = 8f;
    // Y of the Hotkeys rows container. Must match the color section plus the
    // Display section heights in BuildSettingsView.
    const float HKFixedTop = 767f;

    static float HkEntryHeight(Services.SettingsService.HotkeyEntry entry)
    {
        int n = entry.Commands == null ? 0 : entry.Commands.Count;
        return HKHeaderH + n * HkCmdH + HKAddRowH + HkGapH;
    }

    static float HkContentHeight()
    {
        // Fixed rows above hotkeys (headers + geometry) occupy `HKFixedTop`; each
        // hotkey block is one header + its command rows + an add row, plus the global
        // add button (34) and a bottom margin.
        float h = HKFixedTop;
        foreach (var entry in Services.SettingsService.Hotkeys)
            h += HkEntryHeight(entry);
        return h + 34f + 24f;
    }

    static void CommitHotkeyFields()
    {
        // Persist any in-progress command text before a rebuild so a destroy while a
        // field is focused cannot lose what was being typed.
        for (int h = 0; h < _hkCommandFields.Count && h < Services.SettingsService.Hotkeys.Count; h++)
        {
            var entry = Services.SettingsService.Hotkeys[h];
            var row = _hkCommandFields[h];
            for (int c = 0; c < row.Count; c++)
            {
                var field = row[c];
                if (field == null) continue;
                if (entry.Commands != null && c < entry.Commands.Count)
                    entry.Commands[c] = field.text;
            }
        }
    }

    static void RebuildHotkeyRows()
    {
        if (_hkContainer == null) return;
        CommitHotkeyFields();
        for (int i = _hkContainer.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(_hkContainer.GetChild(i).gameObject);
        _hkArmedIndex = -1;
        _hkCommandFields.Clear();

        var hotkeys = Services.SettingsService.Hotkeys;

        // Size the scroll content so all blocks fit.
        RectTransform? content = _hkContainer.parent as RectTransform;
        if (content != null)
            content.sizeDelta = new Vector2(0f, HkContentHeight());

        float cursor = 0f;
        for (int i = 0; i < hotkeys.Count; i++)
        {
            BuildHotkeyBlock(i, -cursor);
            cursor += HkEntryHeight(hotkeys[i]);
        }

        // Position the "+ Add Hotkey" button below the last block.
        if (_hkAddBtnRt != null)
            _hkAddBtnRt.anchoredPosition = new Vector2(8f, -(HKFixedTop + cursor + 10f));
    }

    static void BuildHotkeyBlock(int hkIndex, float yTop)
    {
        var entry = Services.SettingsService.Hotkeys[hkIndex];
        var cmds = entry.Commands ?? new System.Collections.Generic.List<string>();
        Transform container = _hkContainer!;

        // Header: key select on the left, delete-entry X on the right.
        GameObject hdr = UiFactory.Create("HkHdr" + hkIndex, container, Core.UiLayer);
        RectTransform hrt = hdr.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0f, 0f);
        hrt.anchorMax = new Vector2(1f, 0f);
        hrt.pivot = new Vector2(0f, 1f);
        hrt.anchoredPosition = new Vector2(0f, yTop);
        hrt.sizeDelta = new Vector2(0f, HKHeaderH);

        GameObject del = UiFactory.Create("HkDel" + hkIndex, hdr.transform, Core.UiLayer);
        UiFactory.AnchorPoint(del, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(18f, 22f));
        UiFactory.Background(del, Theme.Crimson);
        UiFactory.Button(del, () => RemoveHotkey(hkIndex), new Color(1.25f, 0.45f, 0.45f, 1f));
        GameObject delLabel = UiFactory.Create("HkDelLabel", del.transform, Core.UiLayer);
        UiFactory.FillParent(delLabel);
        UiFactory.Label(delLabel, "X", Theme.FontSizeSmall, Theme.TextPrimary, TextAlignmentOptions.Center);

        // "Hotkey:" label left of the select button.
        GameObject hotkeyLab = UiFactory.Create("HkLabel" + hkIndex, hdr.transform, Core.UiLayer);
        UiFactory.AnchorPoint(hotkeyLab, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(50f, 18f));
        UiFactory.Label(hotkeyLab, "Hotkey:", Theme.FontSizeSmall, Theme.TextMuted);

        GameObject keyBtn = UiFactory.Create("HkKey" + hkIndex, hdr.transform, Core.UiLayer);
        UiFactory.AnchorPoint(keyBtn, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(62f, 0f), new Vector2(140f, 22f));
        UiFactory.Background(keyBtn, Theme.ButtonBg);
        UiFactory.Button(keyBtn, () => ArmedKey(hkIndex));
        GameObject keyLabel = UiFactory.Create("HkKeyLabel", keyBtn.transform, Core.UiLayer);
        UiFactory.FillParent(keyLabel);
        UiFactory.Label(keyLabel, HotkeyDisplay(entry), Theme.FontSizeSmall, Theme.TextPrimary, TextAlignmentOptions.Center);

        // Enabled checkbox right of the select button (default enabled).
        GameObject enBtn = UiFactory.Create("HkEn" + hkIndex, hdr.transform, Core.UiLayer);
        UiFactory.AnchorPoint(enBtn, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(206f, 0f), new Vector2(18f, 18f));
        bool enabled = entry.Enabled ?? true;
        Image enImg = UiFactory.Background(enBtn, enabled ? Theme.Steel : Theme.FieldBg);
        UiFactory.Button(enBtn, () =>
        {
            if (hkIndex >= Services.SettingsService.Hotkeys.Count) return;
            var e = Services.SettingsService.Hotkeys[hkIndex];
            bool next = !(e.Enabled ?? true);
            e.Enabled = next;
            Services.SettingsService.MarkDirty();
            Image img = enBtn.GetComponent<Image>();
            if (img != null) img.color = next ? Theme.Steel : Theme.FieldBg;
        });
        GameObject enMark = UiFactory.Create("HkEnMark" + hkIndex, enBtn.transform, Core.UiLayer);
        UiFactory.FillParent(enMark);
        UiFactory.Label(enMark, enabled ? "X" : string.Empty, Theme.FontSizeSmall, Theme.TextPrimary, TextAlignmentOptions.Center);

        // Numbered command rows.
        var fieldRow = new System.Collections.Generic.List<TMP_InputField?>();
        for (int c = 0; c < cmds.Count; c++)
        {
            float y = yTop - HKHeaderH - c * HkCmdH;
            fieldRow.Add(BuildHotkeyCommandRow(hkIndex, c, y));
        }
        _hkCommandFields.Add(fieldRow);

        // "+ Add command" row.
        float yAdd = yTop - HKHeaderH - cmds.Count * HkCmdH;
        GameObject addRow = UiFactory.Create("HkAddRow" + hkIndex, container, Core.UiLayer);
        RectTransform art = addRow.GetComponent<RectTransform>();
        art.anchorMin = new Vector2(0f, 0f);
        art.anchorMax = new Vector2(1f, 0f);
        art.pivot = new Vector2(0f, 1f);
        art.anchoredPosition = new Vector2(0f, yAdd);
        art.sizeDelta = new Vector2(0f, HKAddRowH);
        GameObject addBtnGo = UiFactory.Create("AddCmdBtn", addRow.transform, Core.UiLayer);
        UiFactory.AnchorPoint(addBtnGo, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(130f, 22f));
        UiFactory.Background(addBtnGo, Theme.ButtonBg);
        UiFactory.Button(addBtnGo, () => AddCommand(hkIndex));
        GameObject addLabel = UiFactory.Create("AddCmdLabel", addBtnGo.transform, Core.UiLayer);
        UiFactory.FillParent(addLabel);
        UiFactory.Label(addLabel, "+ Add command", Theme.FontSizeSmall, Theme.TextPrimary, TextAlignmentOptions.Center);
    }

    static TMP_InputField BuildHotkeyCommandRow(int hkIndex, int cmdIndex, float y)
    {
        var entry = Services.SettingsService.Hotkeys[hkIndex];
        Transform container = _hkContainer!;

        GameObject row = UiFactory.Create("HkCmd" + hkIndex + "_" + cmdIndex, container, Core.UiLayer);
        RectTransform rrt = row.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0f, 0f);
        rrt.anchorMax = new Vector2(1f, 0f);
        rrt.pivot = new Vector2(0f, 1f);
        rrt.anchoredPosition = new Vector2(0f, y);
        rrt.sizeDelta = new Vector2(0f, HkCmdH);

        GameObject num = UiFactory.Create("Num" + hkIndex + "_" + cmdIndex, row.transform, Core.UiLayer);
        UiFactory.AnchorPoint(num, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(18f, 18f));
        UiFactory.Label(num, $"{cmdIndex + 1}.", Theme.FontSizeSmall, Theme.TextMuted);

        GameObject del = UiFactory.Create("CmdDel" + hkIndex + "_" + cmdIndex, row.transform, Core.UiLayer);
        UiFactory.AnchorPoint(del, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(16f, 20f));
        UiFactory.Background(del, Theme.Crimson);
        UiFactory.Button(del, () => RemoveCommand(hkIndex, cmdIndex), new Color(1.25f, 0.45f, 0.45f, 1f));
        GameObject delLabel = UiFactory.Create("CmdDelLabel", del.transform, Core.UiLayer);
        UiFactory.FillParent(delLabel);
        UiFactory.Label(delLabel, "X", Theme.FontSizeSmall, Theme.TextPrimary, TextAlignmentOptions.Center);

        GameObject msgRoot = UiFactory.Create("CmdInput" + hkIndex + "_" + cmdIndex, row.transform, Core.UiLayer);
        UiFactory.Stretch(msgRoot, 30, 22, 2, 2);
        Image msgBg = UiFactory.Background(msgRoot, Theme.SettingsFieldBg);
        GameObject msgTa = UiFactory.Create("TextArea", msgRoot.transform, Core.UiLayer);
        UiFactory.Stretch(msgTa, 6, 6, 2, 2);
        GameObject msgTextGo = UiFactory.Create("Text", msgTa.transform, Core.UiLayer);
        UiFactory.FillParent(msgTextGo);
        TextMeshProUGUI msgText = UiFactory.Label(msgTextGo, string.Empty, Theme.FontSizeSmall, Theme.TextPrimary);
        msgText.enableWordWrapping = false;
        GameObject msgPhGo = UiFactory.Create("Placeholder", msgTa.transform, Core.UiLayer);
        UiFactory.FillParent(msgPhGo);
        TextMeshProUGUI msgPh = UiFactory.Label(msgPhGo, cmdIndex == 0 ? "Command or message" : "Next", Theme.FontSizeSmall, Theme.TextMuted);
        msgPh.enableWordWrapping = false;
        TMP_InputField field = UiFactory.InputField(msgRoot, msgText, msgPh, msgTa.GetComponent<RectTransform>(), msgBg);
        field.text = entry.Commands != null && cmdIndex < entry.Commands.Count ? entry.Commands[cmdIndex] : string.Empty;
        UiFactory.AddEndEditListener(field, s =>
        {
            if (hkIndex >= Services.SettingsService.Hotkeys.Count) return;
            var e = Services.SettingsService.Hotkeys[hkIndex];
            if (e.Commands != null && cmdIndex < e.Commands.Count)
                e.Commands[cmdIndex] = s ?? string.Empty;
            Services.SettingsService.MarkDirty();
        });
        return field;
    }

    static void AddCommand(int hkIndex)
    {
        if (hkIndex >= Services.SettingsService.Hotkeys.Count) return;
        var entry = Services.SettingsService.Hotkeys[hkIndex];
        if (entry.Commands == null) entry.Commands = new System.Collections.Generic.List<string>();
        entry.Commands.Add(string.Empty);
        Services.SettingsService.MarkDirty();
        RebuildHotkeyRows();
    }

    static void RemoveCommand(int hkIndex, int cmdIndex)
    {
        if (hkIndex >= Services.SettingsService.Hotkeys.Count) return;
        var entry = Services.SettingsService.Hotkeys[hkIndex];
        if (entry.Commands == null || cmdIndex < 0 || cmdIndex >= entry.Commands.Count) return;
        entry.Commands.RemoveAt(cmdIndex);
        Services.SettingsService.MarkDirty();
        RebuildHotkeyRows();
    }

    static string HotkeyDisplay(Services.SettingsService.HotkeyEntry e)
    {
        string k = string.IsNullOrEmpty(e.Key) || e.Key == "None" ? "Press key..." : e.Key;
        if (string.IsNullOrEmpty(e.Modifiers)) return k;
        return e.Modifiers + "+" + k;
    }

    static void ArmedKey(int index)
    {
        if (_hkArmedIndex == index)
        {
            _hkArmedIndex = -1;
            if (_hkHint != null) _hkHint.text = "Hotkeys";
        }
        else
        {
            _hkArmedIndex = index;
            if (_hkHint != null) _hkHint.text = "Press key (with modifiers)...";
        }
    }

    static bool IsValidHotkey(KeyCode key)
    {
        if (key == KeyCode.None) return false;
        if (key == KeyCode.Escape || key == KeyCode.BackQuote) return false;
        if (IsModifier(key)) return false;
        int v = (int)key;
        if (v >= (int)KeyCode.Mouse0) return false;
        return true;
    }

    static bool IsModifier(KeyCode key) =>
        key is KeyCode.LeftControl or KeyCode.RightControl or KeyCode.LeftAlt or KeyCode.RightAlt or KeyCode.LeftShift or KeyCode.RightShift;

    static bool HeldModifier(KeyCode mod)
    {
        return mod switch
        {
            KeyCode.LeftControl or KeyCode.RightControl => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl),
            KeyCode.LeftAlt or KeyCode.RightAlt => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt),
            KeyCode.LeftShift or KeyCode.RightShift => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
            _ => false,
        };
    }

    static string CurrentModifiers()
    {
        var sb = new System.Text.StringBuilder();
        if (HeldModifier(KeyCode.LeftControl)) sb.Append(sb.Length > 0 ? "+Ctrl" : "Ctrl");
        if (HeldModifier(KeyCode.LeftAlt)) sb.Append(sb.Length > 0 ? "+Alt" : "Alt");
        if (HeldModifier(KeyCode.LeftShift)) sb.Append(sb.Length > 0 ? "+Shift" : "Shift");
        return sb.ToString();
    }

    static bool ModifiersMatch(string stored)
    {
        stored ??= string.Empty;
        var parts = stored.Split('+', System.StringSplitOptions.RemoveEmptyEntries);
        foreach (string mod in parts)
        {
            if (mod == "Ctrl" && !HeldModifier(KeyCode.LeftControl)) return false;
            if (mod == "Alt" && !HeldModifier(KeyCode.LeftAlt)) return false;
            if (mod == "Shift" && !HeldModifier(KeyCode.LeftShift)) return false;
        }
        // Exact match: no unbound modifier may be held.
        if (!parts.Contains("Ctrl")) { if (HeldModifier(KeyCode.LeftControl)) return false; }
        if (!parts.Contains("Alt")) { if (HeldModifier(KeyCode.LeftAlt)) return false; }
        if (!parts.Contains("Shift")) { if (HeldModifier(KeyCode.LeftShift)) return false; }
        return true;
    }

    static void TickHotkeyCapture()
    {
        if (_hkArmedIndex < 0) return;
        if (_hkArmedIndex >= Services.SettingsService.Hotkeys.Count)
        {
            _hkArmedIndex = -1;
            return;
        }

        // Never capture while the user is typing in a command field: a stray keystroke
        // would be hijacked as a binding and RebuildHotkeyRows would destroy the field
        // mid-edit. Also disarm so the armed state can't linger invisibly.
        for (int h = 0; h < _hkCommandFields.Count; h++)
        {
            var row = _hkCommandFields[h];
            for (int c = 0; c < row.Count; c++)
            {
                var field = row[c];
                if (field != null && field.isFocused)
                {
                    _hkArmedIndex = -1;
                    if (_hkHint != null) _hkHint.text = "Hotkeys";
                    return;
                }
            }
        }

        foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
        {
            if (IsValidHotkey(key) && Input.GetKeyDown(key))
            {
                var entry = Services.SettingsService.Hotkeys[_hkArmedIndex];
                entry.Key = key.ToString();
                entry.Modifiers = CurrentModifiers();
                Services.SettingsService.MarkDirty();
                if (_hkHint != null) _hkHint.text = "Hotkeys";
                _hkArmedIndex = -1;
                RebuildHotkeyRows();
                return;
            }
        }
    }

    static void FireHotkey()
    {
        var hotkeys = Services.SettingsService.Hotkeys;
        for (int i = 0; i < hotkeys.Count; i++)
        {
            var entry = hotkeys[i];
            if ((entry.Enabled ?? true) == false) continue;
            if (string.IsNullOrEmpty(entry.Key) || entry.Key == "None") continue;
            if (System.Enum.TryParse<KeyCode>(entry.Key, out KeyCode key) &&
                IsValidHotkey(key) &&
                ModifiersMatch(entry.Modifiers) &&
                Input.GetKeyDown(key))
            {
                _pendingHkIndex = i;
                _pendingCmdIndex = 0;
                return;
            }
        }
    }

    static bool CanFireHotkeys() =>
        !_viewSettings && !_chatActive && _hkArmedIndex < 0 &&
        !Services.ConsoleGateService.IsConsoleOpen && ChatSender.CanSend;

    // Sends one queued hotkey command per frame so a multi-command hotkey fires its
    // commands one after another instead of in a same-frame burst.
    static void TickPendingHotkey()
    {
        if (_pendingHkIndex < 0) return;
        var hotkeys = Services.SettingsService.Hotkeys;
        if (_pendingHkIndex >= hotkeys.Count)
        {
            _pendingHkIndex = -1;
            return;
        }
        var entry = hotkeys[_pendingHkIndex];
        if ((entry.Enabled ?? true) == false)
        {
            _pendingHkIndex = -1;
            return;
        }
        var cmds = entry.Commands;
        if (cmds == null || cmds.Count == 0)
        {
            _pendingHkIndex = -1;
            return;
        }
        while (_pendingCmdIndex < cmds.Count)
        {
            string cmd = cmds[_pendingCmdIndex].Trim();
            _pendingCmdIndex++;
            if (cmd.Length == 0) continue;
            SendTextWithCurrentChannel(cmd);
            break;
        }
        if (_pendingCmdIndex >= cmds.Count) _pendingHkIndex = -1;
    }

    static void SendTextWithCurrentChannel(string text)
    {
        if (_cycle.Count == 0) return;
        text = text.Trim();
        if (text.Length == 0) return;

        if (TryHandleWhisperCommand(text)) return;

        SendTarget target = _cycle[Math.Clamp(_cycleIndex, 0, _cycle.Count - 1)];
        if (target.Channel == ChatChannel.Whisper)
        {
            ChatSender.SendWhisper(text, target.Target);
            ChatDataService.AddWhisperEcho(target.Target, target.Label, text);
        }
        else
        {
            ChatMessageType type = target.Channel switch
            {
                ChatChannel.Global => ChatMessageType.Global,
                ChatChannel.Clan => ChatMessageType.Team,
                ChatChannel.System => ChatMessageType.System,
                _ => ChatMessageType.Local,
            };
            ChatSender.SendChat(text, type);
            if (target.Channel == ChatChannel.System)
                ChatDataService.AddSystemEcho(text);
            else
                ChatDataService.AddLocalEcho(target.Channel, text);
        }
        PushHistory(text);
    }

    static void DefocusInput()
    {
        _chatActive = false;
        _focusPending = false;
        if (_compose != null)
        {
            _compose.DeactivateInputField();
            EventSystem.current?.SetSelectedGameObject(null);
        }
        MarkActivity();
    }

    static void HideNow()
    {
        _lastActivityUtc = Time.realtimeSinceStartupAsDouble - IdleVisibleSeconds - 1.0;
        _alpha = 0f;
        if (_fade != null)
        {
            _fade.alpha = 0f;
            _fade.interactable = false;
            _fade.blocksRaycasts = false;
        }
    }

    static void HideNowAndClear()
    {
        DefocusInput();
        if (_compose != null)
        {
            _compose.text = string.Empty;
            _compose.textComponent.color = Theme.TextPrimary;
        }
        _historyIndex = -1;
        HideNow();
    }

    static bool TryHandleWhisperCommand(string text)
    {
        if (text.StartsWith("/whisper ", StringComparison.OrdinalIgnoreCase))
            return SendWhisperCommand(text.Substring(9));
        if (text.StartsWith("/w ", StringComparison.OrdinalIgnoreCase))
            return SendWhisperCommand(text.Substring(3));
        return false;
    }

    static bool SendWhisperCommand(string rest)
    {
        rest = rest.Trim();
        int space = rest.IndexOf(' ');
        if (space <= 0)
        {
            ChatDataService.AddSystemEcho("Usage: /whisper <name> <message>");
            return true;
        }

        string name = rest.Substring(0, space);
        string message = rest.Substring(space + 1).Trim();
        if (message.Length == 0)
        {
            ChatDataService.AddSystemEcho("Usage: /whisper <name> <message>");
            return true;
        }

        if (!ChatDataService.TryResolvePlayer(name, out NetworkId target))
        {
            ChatDataService.AddSystemEcho($"Can't whisper \"{name}\" (unknown player). They need to have spoken in chat first.");
            return true;
        }

        ChatSender.SendWhisper(message, target);
        ChatDataService.AddWhisperEcho(target, name, message);
        SelectWhisperPartner(target);
        return true;
    }

    static void SubmitCompose()
    {
        if (_compose == null) return;
        if (_cycle.Count == 0) return;
        _forceHoverRecompute = true;

        string text = _compose.text.Trim();
        if (text.Length == 0)
        {
            _compose.text = string.Empty;
            DefocusInput();
            return;
        }

        if (TryHandleWhisperCommand(text))
        {
            PushHistory(text);
            _compose.text = string.Empty;
            _compose.textComponent.color = Theme.TextPrimary;
            DefocusInput();
            return;
        }

        SendTarget target = _cycle[Math.Clamp(_cycleIndex, 0, _cycle.Count - 1)];

        if (target.Channel == ChatChannel.Whisper)
        {
            ChatSender.SendWhisper(text, target.Target);
            ChatDataService.AddWhisperEcho(target.Target, target.Label, text);
        }
        else
        {
            ChatMessageType type = target.Channel switch
            {
                ChatChannel.Global => ChatMessageType.Global,
                ChatChannel.Clan => ChatMessageType.Team,
                ChatChannel.System => ChatMessageType.System,
                _ => ChatMessageType.Local,
            };
            ChatSender.SendChat(text, type);
            if (target.Channel == ChatChannel.System)
                ChatDataService.AddSystemEcho(text);
            else
                ChatDataService.AddLocalEcho(target.Channel, text);
        }

        PushHistory(text);
        _compose.text = string.Empty;
        _compose.textComponent.color = Theme.TextPrimary;
        DefocusInput();
    }

    static void UpdateCycle()
    {
        if (_cycle.Count == 0) RebuildCycle();

        string signature;
        try
        {
            signature = $"{Services.ChatAvailabilityService.IsGlobalEnabled}|{Services.ChatAvailabilityService.IsAdmin}|{Services.ChatDataService.PartnersSignature}";
        }
        catch
        {
            signature = string.Empty;
        }

        if (signature != _cycleSignature)
        {
            _cycleSignature = signature;
            RebuildCycle();
        }
    }

    static void RebuildCycle()
    {
        bool hadPrevious = _cycle.Count > 0;
        SendTarget previous = hadPrevious ? _cycle[Math.Clamp(_cycleIndex, 0, _cycle.Count - 1)] : default;

        _cycle.Clear();

        if (Services.ChatAvailabilityService.IsGlobalEnabled)
            _cycle.Add(new SendTarget(ChatChannel.Global, default, "Global"));
        _cycle.Add(new SendTarget(ChatChannel.Local, default, "Local"));
        _cycle.Add(new SendTarget(ChatChannel.Clan, default, "Clan"));

        foreach (ChatDataService.WhisperPartner p in Services.ChatDataService.WhisperPartners)
            _cycle.Add(new SendTarget(ChatChannel.Whisper, p.Id, string.IsNullOrEmpty(p.Name) ? "?" : p.Name));

        if (Services.ChatAvailabilityService.IsAdmin)
            _cycle.Add(new SendTarget(ChatChannel.System, default, "System"));

        if (_cycle.Count == 0)
            _cycle.Add(new SendTarget(ChatChannel.Local, default, "Local"));

        int preferredNew = -1;
        if (hadPrevious)
        {
            // Keep the exact whisper partner, not only the channel, so a later rebuild
            // does not move the selection to a different person.
            preferredNew = previous.Channel == ChatChannel.Whisper
                ? _cycle.FindIndex(t => t.Channel == ChatChannel.Whisper && t.Target == previous.Target)
                : _cycle.FindIndex(t => t.Channel == previous.Channel);
        }
        _cycleIndex = preferredNew >= 0 ? preferredNew : 0;
        UpdateChannelLabel();
    }

    static void CycleSendChannel()
    {
        if (_cycle.Count == 0) return;
        _cycleIndex = (_cycleIndex + 1) % _cycle.Count;
        UpdateChannelLabel();
    }

    // After a whisper command, make the whisper channel for that player the active send
    // channel. A brand new partner is registered by the echo but is not in the cycle
    // until the next rebuild, so rebuild once when the partner is not found.
    static void SelectWhisperPartner(NetworkId id)
    {
        int index = _cycle.FindIndex(t => t.Channel == ChatChannel.Whisper && t.Target == id);
        if (index < 0)
        {
            RebuildCycle();
            index = _cycle.FindIndex(t => t.Channel == ChatChannel.Whisper && t.Target == id);
        }
        if (index < 0) return;
        _cycleIndex = index;
        UpdateChannelLabel();
    }

    static void UpdateChannelLabel()
    {
        if (_channelLabel == null) return;
        if (_cycle.Count == 0)
        {
            _channelLabel.text = "Chat";
            return;
        }
        SendTarget target = _cycle[Math.Clamp(_cycleIndex, 0, _cycle.Count - 1)];
        _channelLabel.text = target.Label;
        _channelLabel.color = Theme.ChannelLabelColor(target.Channel);
    }

    static void UpdateFadedState()
    {
        Color scrollColor = new(0.039f, 0.039f, 0.051f, SettingsService.LogBackgroundOpacity);
        if (_scrollBg != null)
        {
            _scrollBg.color = scrollColor;
        }

        if (_settingsBtnBg != null) _settingsBtnBg.color = (_chatActive || _viewSettings) ? Theme.ButtonBg : new(0f, 0f, 0f, 0f);

        Color inputColor = new(0.039f, 0.039f, 0.051f, SettingsService.InputBackgroundOpacity);
        Color inputIdle = new(inputColor.r, inputColor.g, inputColor.b, inputColor.a * 0.45f);
        if (_composeBarBg != null) _composeBarBg.color = _chatActive ? inputColor : inputIdle;
        if (_inputBg != null) _inputBg.color = _chatActive ? inputColor : inputIdle;
    }

    static void RefocusCompose()
    {
        if (_compose == null) return;
        _compose.ActivateInputField();
        EventSystem.current?.SetSelectedGameObject(_compose.gameObject);
    }

    static void UpdateInputHeight()
    {
        if (_compose == null || _composeBarRect == null || _logScrollRect == null) return;

        float h;
        float pvY = 0f;
        int lines = 0;
        if (string.IsNullOrEmpty(_compose.text))
        {
            h = InputH;
        }
        else try
        {
            _compose.textComponent.ForceMeshUpdate(false);
            float w = _compose.textComponent.rectTransform.rect.width;
            if (w < 1f) w = ContentWidth - 16f;
            Vector2 pv = _compose.textComponent.GetPreferredValues(w, 0f);
            pvY = pv.y;
            lines = _compose.textComponent.textInfo.lineCount;
            float measured = pvY > 1f ? pvY : lines * _compose.textComponent.fontSize * 1.4f + 10f;
            float growMax = Math.Min(MaxComposeH, WinH - LabelH - 60f);
            h = Mathf.Clamp(measured + 18f, InputH, Math.Max(InputH, growMax));
        }
        catch (Exception)
        {
            h = InputH;
        }

        if (Mathf.Abs(h - _composeH) < 0.5f) return;

        _composeH = h;
        _composeBarRect!.sizeDelta = new Vector2(ContentWidth, h);
        _logScrollRect!.sizeDelta = new Vector2(ContentWidth, WinH - h - LabelH);

        _logDirty = true;
    }

    static void MarkActivity()
    {
        _lastActivityUtc = Time.realtimeSinceStartupAsDouble;
    }

    static void PushHistory(string text)
    {
        if (_history.Count == 0 || _history[^1] != text)
        {
            _history.Add(text);
            if (_history.Count > 1000) _history.RemoveAt(0);
            Services.InputHistoryService.Push(text);
        }
        _historyIndex = -1;
    }

    static void NavigateHistory(int direction)
    {
        if (_compose == null || _history.Count == 0) return;

        if (direction < 0)
        {
            _historyIndex = _historyIndex < 0 ? _history.Count - 1 : Math.Max(0, _historyIndex - 1);
        }
        else
        {
            if (_historyIndex < 0) return;
            _historyIndex++;
            if (_historyIndex > _history.Count - 1)
            {
                _historyIndex = -1;
                _compose.text = string.Empty;
                return;
            }
        }

        _compose.text = _history[_historyIndex];
        _compose.textComponent.color = Theme.TextPrimary;
        _compose.MoveTextEnd(false);
    }

    static void Tick()
    {
        if (!_built) return;

        // Apply a deferred scroll-to-bottom from a PREVIOUS frame's rebuild. Running
        // here (top of Update) is after ScrollRect.LateUpdate has refreshed its content
        // bounds, so position 0 lands on the true newest line instead of a stale bottom.
        if (_scrollToBottomPending && _scroll != null)
        {
            _scrollToBottomPending = false;
            _anchorTopPending = false;
            _scroll.verticalNormalizedPosition = 0f;
            _lastScrollNorm = 0f;
        }
        else if (_anchorTopPending && _scroll != null && _renderedLines.Count > 0)
        {
            _anchorTopPending = false;
            SetViewportTopToAllLines(_anchorAllLines);
            _lastScrollNorm = _scroll.verticalNormalizedPosition;
        }

        if (Input.GetKeyDown(KeyCode.BackQuote))
        {
            HideNowAndClear();
        }

        if (Services.ConsoleGateService.IsConsoleOpen)
        {
            _wasTypingLastFrame = false;
            return;
        }

        bool pendingFocusAtEntry = _focusPending;

        if (Input.GetKeyDown(KeyCode.F6))
        {
            ToggleModded();
            _wasTypingLastFrame = _chatActive;
            return;
        }

        if (!_moddedActive)
        {
            _wasTypingLastFrame = false;
            return;
        }

        if (CanFireHotkeys())
        {
            try { FireHotkey(); }
            catch (Exception ex) { Core.Log.LogDebug($"[ChatPlus] Hotkey send skipped: {ex.Message}"); }
            try { TickPendingHotkey(); }
            catch (Exception ex) { Core.Log.LogDebug($"[ChatPlus] Hotkey sequence skipped: {ex.Message}"); _pendingHkIndex = -1; }
        }

        if (!_open) SetOpen(true);

        bool enterDown = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);

        if (_dragging && Input.GetMouseButtonUp(0))
        {
            EndDrag();
        }

        if (_open)
        {
            if (_viewSettings)
            {
                TickHotkeyCapture();
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    if (_viewHelp) ShowSettingsPage();
                    else ToggleSettingsView();
                }
            }
            else
            {
                UpdateCycle();

                if (_chatActive && Input.GetKeyDown(KeyCode.Tab))
                {
                    CycleSendChannel();
                    RefocusCompose();
                }

                if (_chatActive && !_dragging && Input.GetMouseButtonDown(0) && IsPointerOverGrip())
                {
                    BeginDrag();
                }
                else if (_dragging && Input.GetMouseButton(0))
                {
                    DragMove();
                }

                if (_compose != null && (_compose.text.Contains("\t") || _compose.text.Contains("\n") || _compose.text.Contains("\r")))
                {
                    bool wasFocused = _chatActive;
                    int caret = wasFocused ? _compose.caretPosition : 0;
                    _compose.text = _compose.text.Replace("\t", string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
                    if (wasFocused) _compose.caretPosition = System.Math.Min(caret, _compose.text.Length);
                }

                if (_compose != null && !string.IsNullOrEmpty(_compose.text) &&
                    Messaging.ChatSender.Utf8ByteLength(_compose.text) > Messaging.ChatSender.MaxChatBytes)
                {
                    bool wasFocused = _chatActive;
                    int caret = wasFocused ? _compose.caretPosition : 0;
                    string clamped = Messaging.ChatSender.ClampUtf8(_compose.text, Messaging.ChatSender.MaxChatBytes);
                    _compose.text = clamped;
                    if (wasFocused) _compose.caretPosition = Math.Min(caret, clamped.Length);
                }

                if (_chatActive)
                {
                    if (Input.GetKeyDown(KeyCode.UpArrow)) NavigateHistory(-1);
                    else if (Input.GetKeyDown(KeyCode.DownArrow)) NavigateHistory(1);
                    if (Input.GetKeyDown(KeyCode.Escape)) DefocusInput();
                    else if (enterDown) SubmitCompose();
                    else if (_compose != null && !_compose.isFocused && !IsPointerOverScrollbar())
                        RefocusCompose();

                    if (Input.GetMouseButtonDown(0) && IsPointerOverLogViewport())
                    {
                        _logClickArmed = true;
                        _logDownPos = Input.mousePosition;
                    }
                }
                else
                {
                    if (enterDown && !_wasTypingLastFrame)
                    {
                        FocusInput();
                    }
                    else if (Input.GetKeyDown(KeyCode.Escape))
                    {
                        HideNow();
                    }
                }

                if (_logClickArmed && Input.GetMouseButtonUp(0))
                {
                    _logClickArmed = false;
                    if (_chatActive && IsPointerOverLogViewport() &&
                        ((Vector2)Input.mousePosition - _logDownPos).magnitude < 6f)
                    {
                        CopyLineAtPoint(Input.mousePosition);
                        FlashHighlight();
                    }
                }

                if (pendingFocusAtEntry && _focusPending)
                {
                    _focusPending = false;
                    RefocusCompose();
                }

                if (_chatActive) UpdateInputHeight();
                else if (_composeH != InputH)
                {
                    _composeH = InputH;
                    if (_composeBarRect != null) _composeBarRect.sizeDelta = new Vector2(ContentWidth, InputH);
                    if (_logScrollRect != null) _logScrollRect.sizeDelta = new Vector2(ContentWidth, WinH - InputH - LabelH);
                }

                // Sliding window. Keep the mesh to a bounded slice of AllLines. At the
                // bottom on the newest line, follow the tail. Otherwise, when the
                // viewport nears a window edge, re-derive the slice around the visible
                // range and keep the top-visible line anchored.
                // Front trimming at the line cap shifts every AllLines index. Move the
                // stored window indices by the same amount so the window still covers
                // the same chat lines.
                int trimDelta = ChatDataService.TrimOffset - _appliedTrim;
                if (trimDelta != 0)
                {
                    _appliedTrim = ChatDataService.TrimOffset;
                    _winStart = Math.Max(0, _winStart - trimDelta);
                    _winEnd = Math.Max(0, _winEnd - trimDelta);
                    if (_anchorAllLines >= 0) _anchorAllLines = Math.Max(0, _anchorAllLines - trimDelta);
                }

                int newest = ChatDataService.AllLines.Count - 1;
                bool overLog = IsPointerOverLogViewport();
                // The wheel belongs to the chat log only when the chat UI captures it:
                // the compose field is focused AND the pointer is over the window. The
                // capture region is the whole window, not just the log viewport.
                bool wheelForLog = _chatActive && !_viewSettings && CapturesWheel;

                float contentH = _scrollContent != null ? _scrollContent.rect.height : 0f;
                float vpH = (_scroll != null && _scroll.viewport != null) ? _scroll.viewport.rect.height : 0f;
                bool scrollable = contentH > vpH + 1f;
                float range = Mathf.Max(0f, contentH - vpH);

                // Inertial wheel glide. The log ScrollRect no longer moves on wheel, so
                // drive the view here and glide toward a pixel target. The target is not
                // clamped to the current range, so momentum carries through a chunk load.
                // The wheel is only ours while the box is focused; otherwise it belongs
                // to the camera.
                float wheel = Input.mouseScrollDelta.y;
                if (wheelForLog && Mathf.Abs(wheel) > 0.01f)
                {
                    _logOffsetTarget = Mathf.Clamp(
                        _logOffsetTarget - wheel * LogScrollPixelsPerNotch,
                        -vpH, range + vpH);
                }

                if (!_chatActive) _logOffsetTarget = _logOffsetApplied;

                bool gliding = Mathf.Abs(_logOffsetApplied - _logOffsetTarget) > 0.5f;
                if (gliding)
                {
                    float k = 1f - Mathf.Exp(-LogScrollSmoothing * Mathf.Max(0.0001f, Time.unscaledDeltaTime));
                    _logOffsetApplied = Mathf.Lerp(_logOffsetApplied, _logOffsetTarget, k);
                    if (_scroll != null && range > 0f)
                    {
                        float applied = Mathf.Clamp(_logOffsetApplied, 0f, range);
                        _scroll.verticalNormalizedPosition = Mathf.Clamp01(1f - applied / range);
                    }
                }
                else if (!_scrollbarDragging)
                {
                    // Idle: follow whatever moved the view (content drag, seek, loader).
                    float p = ViewportTopPixels();
                    _logOffsetTarget = p;
                    _logOffsetApplied = p;
                }

                float curScroll = _scroll != null ? _scroll.verticalNormalizedPosition : 0f;
                bool atBottom = curScroll <= 0.02f;

                // Scroll intent comes from input, not only from the position delta.
                // When the view is clamped at the top or bottom, further wheel input
                // does not move the position, so a delta-only test misses the scroll
                // exactly when a window move is needed.
                bool settle = _ignoreScrollFrames > 0;
                bool wheelUp = wheelForLog && wheel > 0.01f;
                bool wheelDown = wheelForLog && wheel < -0.01f;
                bool deltaUp = !settle && _lastScrollNorm >= 0f && curScroll - _lastScrollNorm > 0.0005f;
                bool deltaDown = !settle && _lastScrollNorm >= 0f && _lastScrollNorm - curScroll > 0.0005f;
                bool intentUp = wheelUp || deltaUp;
                bool intentDown = wheelDown || deltaDown;
                bool userScrolled = intentUp || intentDown;
                if (_ignoreScrollFrames > 0) _ignoreScrollFrames--;
                _lastScrollNorm = curScroll;

                // Compute the viewport bounds at most once per frame, and only when the
                // loader may need them. This scan is the hot path while scrolled up.
                int topIdx = -1;
                int bottomIdx = -1;
                bool haveBounds = (intentUp || intentDown || atBottom) &&
                    !_scrollbarDragging && _renderedLines.Count > 0 &&
                    TryGetViewportLineBounds(out topIdx, out bottomIdx);

                // Throttled diagnostic for the sliding window. Off by default.
                if (ScrollDiag && (overLog || userScrolled) &&
                    Time.realtimeSinceStartupAsDouble - _shiftDiagAt > 0.5)
                {
                    _shiftDiagAt = Time.realtimeSinceStartupAsDouble;
                    bool ok = haveBounds || TryGetViewportLineBounds(out _, out _);
                    Core.Log.LogInfo(
                        $"[ChatPlus][Scroll] newest={newest} win=[{_winStart}..{_winEnd}] rendered={_renderedLines.Count} " +
                        $"norm={curScroll:0.###} atBottom={atBottom} up={intentUp} down={intentDown} wheel={wheel:0.###} " +
                        $"overLog={overLog} follow={_followTail} scrollable={scrollable} contentH={contentH:0} vpH={vpH:0} " +
                        $"bounds={ok} top={(haveBounds ? topIdx : -1)} bot={(haveBounds ? bottomIdx : -1)} " +
                        $"above={(haveBounds ? topIdx - _winStart : -999)} below={(haveBounds ? _winEnd - bottomIdx : -999)}");
                }

                // Persistent tail follow. A new message raises `newest` before the
                // content resizes, so `atBottom` can read false on that same frame. A
                // memory flag keeps the follow alive so new messages never drop, and it
                // clears only on a real upward scroll. The follow has no distance limit,
                // so the mesh always jumps to the true newest.
                if (!intentUp && atBottom && !settle) _followTail = true;
                else if (intentUp && !atBottom) _followTail = false;

                if (_followTail)
                {
                    // A tail rebuild supersedes any pending window move, and stops the
                    // glide so the view stays pinned to the newest line.
                    _windowMoved = false;
                    _logOffsetTarget = _logOffsetApplied;
                    _seekPending = false;
                    _tailRequested = true;
                    // A tail rebuild runs only on a dirty flag. Force one when the
                    // window still lags the newest line, or the gap would wait for the
                    // next incoming message.
                    if (_winEnd < newest) _logDirty = true;
                }
                else
                {
                    // Clear any tail request left over from when the view was at the
                    // bottom, or the next rebuild would snap straight back down.
                    _tailRequested = false;

                    bool seeking = _seekPending;
                    _seekPending = false;

                    if (!seeking && haveBounds)
                    {
                        // Scale the load chunk to the rendered window, so a short window
                        // (long lines) still moves but cannot overrun the character
                        // budget by more than the viewport plus one chunk.
                        int chunk = Math.Min(LoadChunk, Math.Max(4, _renderedLines.Count / 3));
                        int start = _winStart;
                        int end = _winEnd;
                        bool nearTop = (topIdx - _winStart) < chunk;
                        bool nearBottom = (_winEnd - bottomIdx) < chunk;

                        if (intentUp && nearTop && _winStart > 0)
                        {
                            // Load older lines above the viewport, then drop the newest
                            // lines that fall outside the budget. Never cut the viewport.
                            start = Math.Max(0, topIdx - chunk);
                            end = OldestFitEnd(ChatDataService.AllLines, start, MaxRenderChars);
                            if (end < bottomIdx) end = bottomIdx;
                        }
                        else if ((intentDown || atBottom) && nearBottom && _winEnd < newest)
                        {
                            // Load newer lines below the viewport. Clamp the start to the
                            // viewport top, so a tight character budget can never drop the
                            // anchored line and deadlock the window.
                            end = Math.Min(newest, bottomIdx + chunk);
                            start = NewestFitStart(ChatDataService.AllLines, end, MaxRenderChars);
                            if (start > topIdx) start = topIdx;
                        }

                        if (start != _winStart || end != _winEnd)
                        {
                            _anchorAllLines = topIdx;
                            _anchorSubPixel = 0f;
                            if (TryGetChatLinePixelTop(topIdx, out float oldPixelTop))
                                _anchorSubPixel = ViewportTopPixels() - oldPixelTop;
                            _windowMoved = true;
                            _pendingWinStart = start;
                            _pendingWinEnd = end;
                            _logDirty = true;
                        }
                        else
                        {
                            _windowMoved = false;
                        }
                    }
                }

                if (_logDirty)
                {
                    // Clear BEFORE the call so a failure can never wedge the frame loop.
                    _logDirty = false;
                    float beforeOffset = ViewportTopPixels();
                    RebuildLog();
                    // A rebuild can shift the content coordinate space (lines added or
                    // removed at the top). Move the glide with it, so the motion and the
                    // remaining distance stay on the same visual spot.
                    float afterOffset = ViewportTopPixels();
                    _logOffsetTarget += afterOffset - beforeOffset;
                    _logOffsetApplied = afterOffset;
                }

                if (_recalcAfterOpen >= 0.0 && Time.realtimeSinceStartupAsDouble >= _recalcAfterOpen)
                {
                    _recalcAfterOpen = -1.0;
                    RebuildLog();
                }
            }
        }

        UpdateScrollbarInteraction();
        UpdateSettingsScrollInteraction();
        UpdateHandleVisibility();
        UpdateHoverHighlight();

        // Throttled safety net: re-derive mesh + layout geometry ~every 0.5s while the
        // window is actually visible so hover bounds track the rendered lines even if
        // a dirty-flag trigger was ever missed. No string rebuild, no scroll reset.
        _periodicRefreshCounter++;
        if (_periodicRefreshCounter >= 30)
        {
            _periodicRefreshCounter = 0;
            if (_open && _alpha > 0.01f)
                RefreshLogGeometry();
        }

        UpdateFade();
        UpdateFadedState();
        UpdateComposeVisibility();

        if (_copyLabel != null && _copyFeedbackUntil > 0 &&
            Time.realtimeSinceStartupAsDouble >= _copyFeedbackUntil)
        {
            _copyFeedbackUntil = 0;
            _copyLabel.text = "Copy chat";
        }

        if (_channelLabel != null && _channelLabelFeedbackUntil > 0 &&
            Time.realtimeSinceStartupAsDouble >= _channelLabelFeedbackUntil)
        {
            _channelLabelFeedbackUntil = 0;
            UpdateChannelLabel();
        }

        _wasTypingLastFrame = _chatActive;
    }

    static void RefreshGeometryFields()
    {
        try
        {
            if (_geomX != null) _geomX.text = ((int)Services.SettingsService.WindowX).ToString();
            if (_geomY != null) _geomY.text = ((int)Services.SettingsService.WindowY).ToString();
            if (_geomW != null) _geomW.text = ((int)Services.SettingsService.WindowWidth).ToString();
            if (_geomH != null) _geomH.text = ((int)Services.SettingsService.WindowHeight).ToString();
            if (_geomClan != null) _geomClan.text = ((int)Services.SettingsService.ClanListYOffset).ToString();
            _sliderSyncGuard = true;
            try
            {
                if (_geomWSlider != null) _geomWSlider.value = Math.Clamp(Services.SettingsService.WindowWidth, 300f, 2000f);
                if (_geomHSlider != null) _geomHSlider.value = Math.Clamp(Services.SettingsService.WindowHeight, 200f, 1200f);
                if (_geomClanSlider != null) _geomClanSlider.value = Math.Clamp(Services.SettingsService.ClanListYOffset, -500f, 500f);
            }
            finally
            {
                _sliderSyncGuard = false;
            }
        }
        catch (Exception ex)
        {
            Core.Log.LogDebug($"[ChatPlus] Refresh geometry failed: {ex.Message}");
        }
    }

    static TMP_InputField? BuildGeometryRow(Transform parent, int layer, string name, float y, System.Func<float> getCurrent, System.Action<float> onApply)
    {
        try
        {
            GameObject lab = UiFactory.Create(name + "Label", parent, layer);
            UiFactory.AnchorPoint(lab, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(8f, y), new Vector2(70f, 18f));
            UiFactory.Label(lab, name, Theme.FontSizeSmall, Theme.TextMuted);

            GameObject root = UiFactory.Create(name + "Field", parent, layer);
            UiFactory.AnchorPoint(root, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(88f, y), new Vector2(90f, 20f));
            Image bgImg = UiFactory.Background(root, Theme.SettingsFieldBg);

            GameObject ta = UiFactory.Create("TextArea", root.transform, layer);
            UiFactory.Stretch(ta, 6, 6, 2, 2);
            GameObject textGO = UiFactory.Create("Text", ta.transform, layer);
            UiFactory.FillParent(textGO);
            TextMeshProUGUI text = UiFactory.Label(textGO, ((int)getCurrent()).ToString(), Theme.FontSizeSmall, Theme.TextPrimary);
            text.enableWordWrapping = false;
            GameObject phGO = UiFactory.Create("Placeholder", ta.transform, layer);
            UiFactory.FillParent(phGO);
            TextMeshProUGUI ph = UiFactory.Label(phGO, string.Empty, Theme.FontSizeSmall, Theme.TextMuted);
            ph.enableWordWrapping = false;

            TMP_InputField field = UiFactory.InputField(root, text, ph, ta.GetComponent<RectTransform>(), bgImg);
            field.contentType = TMP_InputField.ContentType.IntegerNumber;
            field.characterLimit = 5;
            UiFactory.AddEndEditListener(field, s =>
            {
                if (float.TryParse(s, out float v))
                {
                    onApply(v);
                }
                field.text = ((int)getCurrent()).ToString();
            });
            return field;
        }
        catch (Exception ex)
        {
            Core.Log.LogDebug($"[ChatPlus] Geometry row {name} skipped: {ex.Message}");
            return null;
        }
    }

    // Numeric field plus a ranged slider on the same row (Width / Height / Clan up).
    static TMP_InputField? BuildGeometrySliderRow(Transform parent, int layer, string name, float y,
        System.Func<float> getCurrent, System.Action<float> onApply, float minV, float maxV, ref Slider? storeSlider)
    {
        try
        {
            GameObject lab = UiFactory.Create(name + "Label", parent, layer);
            UiFactory.AnchorPoint(lab, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(8f, y), new Vector2(70f, 18f));
            UiFactory.Label(lab, name, Theme.FontSizeSmall, Theme.TextMuted);

            GameObject root = UiFactory.Create(name + "Field", parent, layer);
            UiFactory.AnchorPoint(root, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(88f, y), new Vector2(90f, 20f));
            Image bgImg = UiFactory.Background(root, Theme.SettingsFieldBg);

            GameObject ta = UiFactory.Create("TextArea", root.transform, layer);
            UiFactory.Stretch(ta, 6, 6, 2, 2);
            GameObject textGO = UiFactory.Create("Text", ta.transform, layer);
            UiFactory.FillParent(textGO);
            TextMeshProUGUI text = UiFactory.Label(textGO, ((int)getCurrent()).ToString(), Theme.FontSizeSmall, Theme.TextPrimary);
            text.enableWordWrapping = false;
            GameObject phGO = UiFactory.Create("Placeholder", ta.transform, layer);
            UiFactory.FillParent(phGO);
            TextMeshProUGUI ph = UiFactory.Label(phGO, string.Empty, Theme.FontSizeSmall, Theme.TextMuted);
            ph.enableWordWrapping = false;

            TMP_InputField field = UiFactory.InputField(root, text, ph, ta.GetComponent<RectTransform>(), bgImg);
            field.contentType = TMP_InputField.ContentType.IntegerNumber;
            field.characterLimit = 5;

            GameObject sliderRoot = UiFactory.Create(name + "Slider", parent, layer);
            UiFactory.AnchorPoint(sliderRoot, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(186f, y), new Vector2(132f, 14f));
            UiFactory.Background(sliderRoot, Theme.FieldBg);

            GameObject fill = UiFactory.Create("SliderFill", sliderRoot.transform, layer);
            RectTransform fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            Image fillImg = UiFactory.Background(fill, Theme.Steel);
            fillImg.raycastTarget = false;

            GameObject handle = UiFactory.Create("SliderHandle", sliderRoot.transform, layer);
            RectTransform handleRt = handle.GetComponent<RectTransform>();
            handleRt.anchorMin = new Vector2(0f, 0.5f);
            handleRt.anchorMax = new Vector2(0f, 0.5f);
            handleRt.pivot = new Vector2(0.5f, 0.5f);
            handleRt.sizeDelta = new Vector2(6f, 8f);
            handleRt.anchoredPosition = Vector2.zero;
            Image handleImg = UiFactory.Background(handle, new Color(0.725f, 0.788f, 0.827f, 1f));
            handleImg.raycastTarget = true;

            Slider slider = UiFactory.AddRangeHorizontalSlider(sliderRoot, handleRt, fillRt, handleImg,
                Math.Clamp(getCurrent(), minV, maxV), minV, maxV, v =>
                {
                    if (_sliderSyncGuard) return;
                    onApply(Math.Clamp(v, minV, maxV));
                });

            UiFactory.AddEndEditListener(field, s =>
            {
                if (float.TryParse(s, out float v))
                {
                    onApply(v);
                }
                field.text = ((int)getCurrent()).ToString();
                SyncSlider(slider, getCurrent(), minV, maxV);
            });

            storeSlider = slider;
            return field;
        }
        catch (Exception ex)
        {
            Core.Log.LogDebug($"[ChatPlus] Geometry slider row {name} skipped: {ex.Message}");
            return null;
        }
    }

    static void SyncSlider(Slider slider, float value, float minV, float maxV)
    {
        if (slider == null) return;
        _sliderSyncGuard = true;
        try { slider.value = Math.Clamp(value, minV, maxV); }
        finally { _sliderSyncGuard = false; }
    }

    static string MirrorToggleText()
        => "Right side: " + (Mirrored ? "On" : "Off");

    static string CurrentColorForRow(int row)
    {
        if (row >= 0 && row < 10)
        {
            ChatChannel c = _colorRowChannels[row];
            string hex = "F2EBDB";
            if ((row & 1) == 0)
                Services.SettingsService.TryGetChannelLabelHex(c, out hex);
            else
                Services.SettingsService.TryGetChannelHex(c, out hex);
            return hex;
        }
        if (row == 10)
        {
            Services.SettingsService.TryGetSelfHex(out string n, out _);
            return n;
        }
        if (row == 11)
        {
            Services.SettingsService.TryGetSelfHex(out _, out string m);
            return m;
        }
        if (row == 12)
        {
            Services.SettingsService.TryGetAdminHex(out string n, out _);
            return n;
        }
        if (row == 13)
        {
            Services.SettingsService.TryGetAdminHex(out _, out string m);
            return m;
        }
        return "F2EBDB";
    }

    static Color SwatchContrast(Color c)
    {
        float lum = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
        return lum > 0.5f ? new Color(0.16f, 0.16f, 0.16f, 1f) : new Color(1f, 1f, 1f, 1f);
    }

    // A faint full-width panel behind one special color row and its "Applies to" strip.
    // It ties the strip to its row so the switches are not read as belonging to the
    // next row down.
    static void BuildColorGroupBackground(Transform parent, int layer, float rowY, float stripY)
    {
        float top = rowY + 12f;
        float bottom = stripY - 10f;
        GameObject bg = UiFactory.Create("ColorGroupBg", parent, layer);
        RectTransform rt = bg.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, top);
        rt.sizeDelta = new Vector2(-8f, top - bottom);
        Image img = UiFactory.Background(bg, Theme.RowStripe);
        img.raycastTarget = false;
    }

    static void BuildColorSwatchRow(Transform parent, int layer, int row, float y)
    {
        GameObject lab = UiFactory.Create("ColorLabel" + row, parent, layer);
        UiFactory.AnchorPoint(lab, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(8f, y), new Vector2(112f, 18f));
        UiFactory.Label(lab, _colorRowNames[row], Theme.FontSizeSmall, Theme.TextMuted);

        for (int s = 0; s < _colorPalette.Length; s++)
        {
            int idx = s;
            GameObject swatch = UiFactory.Create("ColorSwatch" + row + "_" + s, parent, layer);
            UiFactory.AnchorPoint(swatch, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(124f + s * 26f, y), new Vector2(20f, 20f));
            UiFactory.Background(swatch, _colorPalette[s]);
            UiFactory.Button(swatch, () => OnColorPicked(row, idx));

            // Selection marker: an X drawn in a contrasting color so the active swatch
            // is always obvious regardless of how light or dark the color is.
            GameObject sel = UiFactory.Create("Sel" + row + "_" + s, swatch.transform, layer);
            UiFactory.FillParent(sel);
            UiFactory.Label(sel, "X", 10f, SwatchContrast(_colorPalette[s]), TextAlignmentOptions.Center);
            sel.SetActive(false);
            _colorSwatchRows.Add(sel);
        }
    }

    static ChatColorGroup? ColorGroupForRow(int row) => row switch
    {
        10 => ChatColorGroup.SelfName,
        11 => ChatColorGroup.SelfMessage,
        12 => ChatColorGroup.AdminName,
        13 => ChatColorGroup.AdminMessage,
        _ => null,
    };

    // A small channel switch row under a special color row. Each switch turns that
    // group's color on or off for one channel, so the line falls back to the next
    // tier and then to the channel color.
    static void BuildColorChannelStrip(Transform parent, int layer, ChatColorGroup group, float y)
    {
        GameObject lab = UiFactory.Create("ScopeLabel" + group, parent, layer);
        UiFactory.AnchorPoint(lab, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(18f, y), new Vector2(100f, 14f));
        TextMeshProUGUI labTmp = UiFactory.Label(lab, "Applies to", 10f, Theme.TextMuted);
        labTmp.enableWordWrapping = false;

        ChatChannel[] channels = [ChatChannel.Global, ChatChannel.Local, ChatChannel.Clan, ChatChannel.Whisper];
        string[] words = ["Global", "Local", "Clan", "Whisper"];
        for (int i = 0; i < channels.Length; i++)
        {
            ChatChannel channel = channels[i];
            GameObject btn = UiFactory.Create("Scope" + group + "_" + words[i], parent, layer);
            UiFactory.AnchorPoint(btn, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(124f + i * 58f, y), new Vector2(54f, 16f));
            Image bg = UiFactory.Background(btn, Theme.ButtonBg);
            GameObject labelGo = UiFactory.Create("Label", btn.transform, layer);
            UiFactory.FillParent(labelGo);
            TextMeshProUGUI tmp = UiFactory.Label(labelGo, words[i], 10f, Theme.TextPrimary, TextAlignmentOptions.Center);
            tmp.enableWordWrapping = false;

            void ApplyVisual()
            {
                bool on = Services.SettingsService.IsColorScopeEnabled(group, channel);
                bg.color = on ? Theme.Crimson : Theme.ButtonBg;
                tmp.color = on ? Theme.TextPrimary : Theme.TextMuted;
            }
            ApplyVisual();

            UiFactory.Button(btn, () =>
            {
                bool next = !Services.SettingsService.IsColorScopeEnabled(group, channel);
                Services.SettingsService.SetColorScope(group, channel, next);
                Services.SettingsService.MarkDirty();
                ApplyVisual();
                RebuildLog();
                UpdateChannelLabel();
            });
        }
    }

    static void RefreshColorSelection()
    {
        for (int row = 0; row < _colorRowNames.Length; row++)
        {
            string current = CurrentColorForRow(row);
            for (int s = 0; s < _colorPalette.Length; s++)
            {
                int index = row * _colorPalette.Length + s;
                if (index >= _colorSwatchRows.Count) continue;
                string swatchHex = ColorUtility.ToHtmlStringRGB(_colorPalette[s]).ToUpper();
                _colorSwatchRows[index].SetActive(current.ToUpper() == swatchHex);
            }
        }
    }

    static void OnColorPicked(int row, int swatchIndex)
    {
        Color color = _colorPalette[swatchIndex];
        string hex = ColorUtility.ToHtmlStringRGB(color);
        if (row >= 0 && row < 10)
        {
            ChatChannel c = _colorRowChannels[row];
            if ((row & 1) == 0) Services.SettingsService.SetChannelLabelHex(c, hex);
            else Services.SettingsService.SetChannelHex(c, hex);
        }
        else if (row == 10)
            Services.SettingsService.SetSelfHex(hex, CurrentColorForRow(11));
        else if (row == 11)
            Services.SettingsService.SetSelfHex(CurrentColorForRow(10), hex);
        else if (row == 12)
            Services.SettingsService.SetAdminHex(hex, CurrentColorForRow(13));
        else if (row == 13)
            Services.SettingsService.SetAdminHex(CurrentColorForRow(12), hex);

        Services.SettingsService.MarkDirty();
        RefreshColorSelection();
        RebuildLog();
        UpdateChannelLabel();
    }

    static TextMeshProUGUI BuildToggleRow(Transform parent, int layer, string name, float y, System.Func<bool> getValue, System.Action<bool> onToggle)
    {
        GameObject lab = UiFactory.Create("ToggleLabel" + name, parent, layer);
        UiFactory.AnchorPoint(lab, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(8f, y), new Vector2(100f, 18f));
        UiFactory.Label(lab, name, Theme.FontSizeSmall, Theme.TextMuted);

        GameObject btn = UiFactory.Create("ToggleBtn" + name, parent, layer);
        UiFactory.AnchorPoint(btn, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(116f, y), new Vector2(52f, 18f));
        UiFactory.Background(btn, Theme.ButtonBg);
        GameObject btnLabelGo = UiFactory.Create("ToggleValue", btn.transform, layer);
        UiFactory.FillParent(btnLabelGo);
        TextMeshProUGUI btnLabel = UiFactory.Label(btnLabelGo, getValue() ? "On" : "Off", Theme.FontSizeSmall, Theme.TextPrimary, TextAlignmentOptions.Center);
        UiFactory.Button(btn, () =>
        {
            bool next = !getValue();
            onToggle(next);
            btnLabel.text = next ? "On" : "Off";
        });
        return btnLabel;
    }

    static void ToggleMirrorLayout()
    {
        try
        {
            Services.SettingsService.MirrorLayout = !Mirrored;
            _viewSettings = false;
            _viewHelp = false;
            _hkArmedIndex = -1;
            ResetSession();
            EnsureBuilt();
            MarkActivity();
        }
        catch (Exception ex)
        {
            Core.Log.LogError($"[ChatPlus] Mirror toggle failed: {ex}");
        }
    }

    static void ToggleSettingsView()
    {
        _viewSettings = !_viewSettings;

        if (_viewSettings)
        {
            _viewHelp = false;
            // Cancel any in-flight hotkey sequence so its remaining commands can never
            // resume and fire after Settings is closed.
            _pendingHkIndex = -1;
            _pendingCmdIndex = -1;
            DefocusInput();
            RefreshGeometryFields();
            if (_settingsScroll != null) _settingsScroll.verticalNormalizedPosition = 1f;
            if (_hkHint != null) _hkHint.text = "Hotkeys";
        }
        else
        {
            _viewHelp = false;
            _chatActive = true;
            _focusPending = true;
            MarkActivity();
        }

        UpdateViewActives();
        UpdateComposeVisibility();
        UpdateFade();
    }

    static void ShowHelpPage()
    {
        if (!_viewSettings) return;
        _viewHelp = true;
        if (_helpScroll != null) _helpScroll.verticalNormalizedPosition = 1f;
        UpdateViewActives();
        UpdateFade();
    }

    static void ShowSettingsPage()
    {
        if (!_viewSettings) return;
        _viewHelp = false;
        UpdateViewActives();
        UpdateFade();
    }

    static void UpdateViewActives()
    {
        if (_chatView != null) _chatView.SetActive(!_viewSettings);
        if (_settingsView != null) _settingsView.SetActive(_viewSettings && !_viewHelp);
        if (_helpView != null) _helpView.SetActive(_viewSettings && _viewHelp);
        if (_bottomBar != null) _bottomBar.SetActive(_viewSettings);
        if (_helpBtn != null) _helpBtn.SetActive(_viewSettings && !_viewHelp);
        if (_helpBackBtn != null) _helpBackBtn.SetActive(_viewSettings && _viewHelp);
        if (_copyBtn != null) _copyBtn.SetActive(_viewSettings && !_viewHelp);
    }

    static void UpdateFade()
    {
        if (_fade == null) return;

        float target = _chatActive || _viewSettings || (Time.realtimeSinceStartupAsDouble - _lastActivityUtc) < IdleVisibleSeconds ? 1f : 0f;
        float speed = target > _alpha || _chatActive || _viewSettings ? FadeInRate : FadeOutRate;
        _alpha = Mathf.MoveTowards(_alpha, target, speed * Time.deltaTime);

        _fade.alpha = _alpha;
        _fade.interactable = _chatActive || _viewSettings;
        _fade.blocksRaycasts = _chatActive || _viewSettings;
    }

    static void UpdateComposeVisibility()
    {
        if (_composeBar != null)
        {
            bool show = (_chatActive && !_viewSettings) || _focusPending;
            if (_composeBar.activeSelf != show)
                _composeBar.SetActive(show);
        }

        if (_hintBar != null)
        {
            bool showHint = !_viewSettings && _composeBar != null && !_composeBar.activeSelf;
            if (_hintBar.activeSelf != showHint)
                _hintBar.SetActive(showHint);
        }

        bool showLabel = _chatActive && !_viewSettings;
        if (_channelLabel != null && _channelLabel.gameObject.activeSelf != showLabel)
            _channelLabel.gameObject.SetActive(showLabel);
        if (_channelBg != null && _channelBg.activeSelf != showLabel)
            _channelBg.SetActive(showLabel);
        if (_gripRect != null && _gripRect.gameObject.activeSelf != showLabel)
            _gripRect.gameObject.SetActive(showLabel);
    }

    static void UpdateScrollbarInteraction()
    {
        if (_viewSettings) return;
        if (_scrollbar == null || _scrollbarRect == null) return;
        if (_alpha <= 0.01f) return;

        // Scrolling is only ours while the chat box is focused. Otherwise the mouse
        // buttons and wheel belong to the game.
        if (_chatActive)
        {
            bool over = IsPointerOverScrollbar();

            float wheel = Input.mouseScrollDelta.y;
            if (over && Mathf.Abs(wheel) > 0.01f)
            {
                int step = Mathf.Max(1, VisibleLineEstimate() / 3);
                SeekToCenter(GlobalCenterLine() + Mathf.RoundToInt(-wheel * step));
            }

            if (!_scrollbarDragging && Input.GetMouseButtonDown(0) && over)
            {
                _scrollbarDragging = true;
                ApplyScrollbarFromPointer();
            }
            else if (_scrollbarDragging)
            {
                ApplyScrollbarFromPointer();
                if (Input.GetMouseButtonUp(0)) _scrollbarDragging = false;
            }
        }
        else
        {
            _scrollbarDragging = false;
        }

        UpdateScrollbarDisplay();
    }

    static int GlobalCenterLine() => (_winStart + _winEnd) / 2;

    // Approximate number of chat lines the viewport shows, from the rendered average
    // line height. Used for the rail handle size and for seek distances.
    static int VisibleLineEstimate()
    {
        float contentH = _scrollContent != null ? _scrollContent.rect.height : 0f;
        float vpH = (_scroll != null && _scroll.viewport != null) ? _scroll.viewport.rect.height : 0f;
        int rendered = Math.Max(1, _renderedLines.Count);
        if (contentH <= 0f || vpH <= 0f) return rendered;
        float avgH = contentH / rendered;
        if (avgH <= 0f) return rendered;
        return Mathf.Max(1, Mathf.RoundToInt(vpH / avgH));
    }

    // The rail scrollbar shows the position inside the FULL history, not just the
    // rendered window. The ScrollRect is decoupled, so this bar is display plus seek.
    static void UpdateScrollbarDisplay()
    {
        if (_scrollbar == null) return;

        int count = ChatDataService.AllLines.Count;
        if (count <= 1)
        {
            _displayScrollSize = 1f;
            _scrollbar.size = 1f;
            _scrollbar.value = 1f;
            return;
        }

        float contentH = _scrollContent != null ? _scrollContent.rect.height : 0f;
        float vpH = (_scroll != null && _scroll.viewport != null) ? _scroll.viewport.rect.height : 0f;

        // Handle size and position use the same line basis, so they agree. The old
        // pixel size plus window-center mapping left the handle above the bottom when
        // the view was pinned to the newest line.
        int visible = Mathf.Clamp(VisibleLineEstimate(), 1, count);
        float targetSize = Mathf.Clamp01((float)visible / count);
        targetSize = Mathf.Max(targetSize, 0.05f);
        _displayScrollSize = Mathf.MoveTowards(_displayScrollSize, targetSize, 0.05f);
        _scrollbar.size = _displayScrollSize;

        int start = _windowMoved ? _pendingWinStart : _winStart;
        int end = _windowMoved ? _pendingWinEnd : _winEnd;
        int windowLines = Mathf.Max(1, end - start + 1);
        float range = Mathf.Max(0f, contentH - vpH);
        float frac = range > 0f ? Mathf.Clamp01(ViewportTopPixels() / range) : 1f;
        float topGlobal = start + frac * Mathf.Max(0, windowLines - visible);
        int scrollRange = Mathf.Max(1, count - visible);
        _scrollbar.value = 1f - Mathf.Clamp01(topGlobal / scrollRange);
    }

    // Jump the window so the given global line sits at the viewport center.
    static void SeekToCenter(int centerLine)
    {
        int newest = ChatDataService.AllLines.Count - 1;
        if (newest < 0) return;
        IReadOnlyList<ChatLine> lines = ChatDataService.AllLines;
        int visible = VisibleLineEstimate();
        int half = visible / 2;

        // Near the newest side, hand back to tail follow.
        if (centerLine >= newest - half)
        {
            _followTail = true;
            _windowMoved = false;
            _tailRequested = true;
            _logOffsetTarget = _logOffsetApplied;
            _seekPending = true;
            _logDirty = true;
            return;
        }

        int targetTop = Math.Clamp(centerLine - half, 0, newest);
        int start = targetTop;
        int end = OldestFitEnd(lines, start, MaxRenderChars);
        if (end < targetTop) end = targetTop;
        if (end > newest) end = newest;

        _followTail = false;
        _tailRequested = false;
        _logOffsetTarget = _logOffsetApplied;
        _seekPending = true;
        _anchorAllLines = targetTop;
        _anchorSubPixel = 0f;
        _windowMoved = true;
        _pendingWinStart = start;
        _pendingWinEnd = end;
        _logDirty = true;
    }

    static void ApplyScrollbarFromPointer()
    {
        if (_scrollbarRect == null) return;
        Camera camera = Core.UiCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null!
            : Core.UiCanvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_scrollbarRect, Input.mousePosition, camera, out Vector2 local))
            return;

        float h = _scrollbarRect.rect.height;
        if (h <= 0f) return;
        float t = Mathf.Clamp01((local.y - _scrollbarRect.rect.yMin) / h);
        int count = ChatDataService.AllLines.Count;
        if (count <= 0) return;
        int center = Mathf.RoundToInt((1f - t) * Mathf.Max(0, count - 1));
        SeekToCenter(center);
    }

    // Manual wheel + drag for the settings and help scrollbars. The chat log needs
    // full manual interaction (native ScrollRect doesn't scroll here), and settings
    // shared the same native-only setup, so give both pages the same handling.
    static void UpdateSettingsScrollInteraction()
    {
        if (!_viewSettings) return;
        if (_alpha <= 0.01f) return;
        Scrollbar? sbar = _viewHelp ? _helpScrollbar : _settingsScrollbar;
        RectTransform? barRect = _viewHelp ? _helpScrollbarRect : _settingsScrollbarRect;
        RectTransform? viewport = _viewHelp && _helpScroll != null ? _helpScroll.viewport
            : (!_viewHelp && _settingsScroll != null ? _settingsScroll.viewport : null);
        if (sbar == null || barRect == null || viewport == null) return;

        bool overBar = ScreenPointWithinRect(barRect);

        // The whole window captures the wheel while the chat UI is focused. The
        // settings page scrolls whenever the pointer is over the window, matching the
        // chat log behavior. Outside the window the wheel zooms the camera.
        float wheel = Input.mouseScrollDelta.y;
        if (CapturesWheel && Mathf.Abs(wheel) > 0.01f)
        {
            sbar.value = Mathf.Clamp01(sbar.value + wheel * WheelScrollStep);
        }

        if (!_settingsScrollbarDragging && Input.GetMouseButtonDown(0) && overBar)
        {
            _settingsScrollbarDragging = true;
            ApplySettingsScrollbarFromPointer(sbar, barRect);
        }
        else if (_settingsScrollbarDragging)
        {
            ApplySettingsScrollbarFromPointer(sbar, barRect);
            if (Input.GetMouseButtonUp(0)) _settingsScrollbarDragging = false;
        }
    }

    static void ApplySettingsScrollbarFromPointer(Scrollbar sbar, RectTransform barRect)
    {
        Camera camera = Core.UiCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null!
            : Core.UiCanvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(barRect, Input.mousePosition, camera, out Vector2 local))
            return;

        float h = barRect.rect.height;
        if (h <= 0f) return;
        float t = (local.y - barRect.rect.yMin) / h;
        sbar.value = Mathf.Clamp01(t);
    }

    static bool ScreenPointWithinRect(RectTransform rt)
    {
        if (rt == null) return false;
        Camera camera = Core.UiCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null!
            : Core.UiCanvas.worldCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, camera);
    }

    static void RefreshLogGeometry()
    {
        if (_log == null || _scrollContent == null) return;
        try
        {
            _log.ForceMeshUpdate(false);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollContent);
        }
        catch
        {
        }
    }

    // Show the scroll HANDLE only when the content overflows the viewport; keep the
    // faint track/bar always visible. Applied to the chat log and settings scrollbars.
    static void UpdateHandleVisibility()
    {
        try
        {
            if (_scroll != null)
            {
                // The bar reflects the full history, so show the handle whenever the
                // history holds more lines than the viewport can show.
                int visible = VisibleLineEstimate();
                bool overflow = ChatDataService.AllLines.Count > visible + 1;
                if (_scrollbarHandleGo != null && _scrollbarHandleGo.activeSelf != overflow)
                    _scrollbarHandleGo.SetActive(overflow);
            }

            if (_settingsHandleGo != null)
            {
                bool overflow = _settingsScroll != null && _settingsScroll.content != null &&
                    _settingsScroll.viewport != null &&
                    _settingsScroll.content.rect.height > _settingsScroll.viewport.rect.height + 1f;
                if (_settingsHandleGo.activeSelf != overflow)
                    _settingsHandleGo.SetActive(overflow);
            }

            if (_helpHandleGo != null)
            {
                bool overflow = _helpScroll != null && _helpScroll.content != null &&
                    _helpScroll.viewport != null &&
                    _helpScroll.content.rect.height > _helpScroll.viewport.rect.height + 1f;
                if (_helpHandleGo.activeSelf != overflow)
                    _helpHandleGo.SetActive(overflow);
            }
        }
        catch
        {
        }
    }

    static bool IsPointerOverScrollbar()
    {
        if (_scrollbarRect == null) return false;
        Camera camera = Core.UiCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null!
            : Core.UiCanvas.worldCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(_scrollbarRect, Input.mousePosition, camera);
    }

    public static void FocusInput()
    {
        if (_root == null) return;
        if (_viewSettings) ToggleSettingsView();
        SetOpen(true);
        if (!_chatActive)
        {
            _chatActive = true;
            _focusPending = true;
        }
        _wasTypingLastFrame = true;
        MarkActivity();
        UpdateComposeVisibility();
        UpdateFade();
    }

    // Frees the keyboard and the EventSystem without closing the window. Used when a
    // disconnect leaves the session alive but the local player is gone, so stale
    // typing state cannot keep the game input suppressed.
    public static void ForceReleaseInput()
    {
        _chatActive = false;
        _viewSettings = false;
        _viewHelp = false;
        _hkArmedIndex = -1;
        _focusPending = false;
        _dragging = false;
        _scrollbarDragging = false;
        _settingsScrollbarDragging = false;
        _pendingHkIndex = -1;
        try { if (_compose != null) _compose.DeactivateInputField(); } catch { }
        try { EventSystem.current?.SetSelectedGameObject(null); } catch { }
    }

    public static void ReleaseInput()
    {
        ForceReleaseInput();
        SetOpen(false);
    }

    static void SetOpen(bool open)
    {
        if (_root == null) return;

        bool transitioning = open && !_open;
        _open = open;
        _root.SetActive(open);

        if (transitioning)
        {
            _logDirty = true;
            _forceHoverRecompute = true;
        }
        else if (!open)
        {
            // Only tear down input/view state on a real close. A no-op re-open while
            // the window is already up (e.g. FocusInput / modded re-toggle) must never
            // flip _viewSettings behind the Settings UI and open the hotkey fire gate.
            _chatActive = false;
            _viewSettings = false;
            _viewHelp = false;
            _hkArmedIndex = -1;
            _focusPending = false;
            _scrollbarDragging = false;
            if (_compose != null)
            {
                _compose.DeactivateInputField();
                EventSystem.current?.SetSelectedGameObject(null);
            }
        }

        if (!open && _fade != null)
        {
            _alpha = 0f;
            _fade.alpha = 0f;
            _fade.interactable = false;
            _fade.blocksRaycasts = false;
        }

        UpdateFadedState();
    }

    public static bool IsDragging => _dragging && _open && _moddedActive;

    public static void ApplyLayout()
    {
        if (_rootRect == null) return;
        try
        {
            _rootRect.anchoredPosition = ClampWindowPos(new Vector2(Services.SettingsService.WindowX, Services.SettingsService.WindowY));
            _rootRect.sizeDelta = new Vector2(WinW, WinH);
            RefreshBakedSizes();
            RefreshGeometryFields();
            Core.Log.LogInfo($"[ChatPlus] Layout pos=({_rootRect.anchoredPosition.x:0},{_rootRect.anchoredPosition.y:0}) size=({WinW:0}x{WinH:0})");
        }
        catch (Exception ex)
        {
            Core.Log.LogDebug($"[ChatPlus] ApplyLayout skipped: {ex.Message}");
        }
    }

    static Vector2 ClampWindowPos(Vector2 pos)
    {
        try
        {
            RectTransform? canvasRt = Core.UiCanvas.transform as RectTransform;
            if (canvasRt == null) return pos;
            float cw = canvasRt.rect.width;
            float ch = canvasRt.rect.height;
            if (cw < WinW || ch < WinH) return pos;
            return new Vector2(
                Mathf.Clamp(pos.x, 64f - WinW, cw - 64f),
                Mathf.Clamp(pos.y, 64f - WinH, ch - 64f));
        }
        catch
        {
            return pos;
        }
    }

    static void RefreshBakedSizes()
    {
        try
        {
            float growMax = Math.Max(InputH, Math.Min(MaxComposeH, WinH - LabelH - 60f));
            _composeH = Math.Min(_composeH, growMax);
            if (_composeBarRect != null) _composeBarRect.sizeDelta = new Vector2(ContentWidth, _composeH);
            if (_logScrollRect != null) _logScrollRect.sizeDelta = new Vector2(ContentWidth, WinH - _composeH - LabelH);
            if (_hintBar != null)
            {
                RectTransform? hrt = _hintBar.GetComponent<RectTransform>();
                if (hrt != null) hrt.sizeDelta = new Vector2(ContentWidth, InputH);
            }
            if (_channelBg != null)
            {
                RectTransform? bgrt = _channelBg.GetComponent<RectTransform>();
                if (bgrt != null) bgrt.sizeDelta = new Vector2(WinW, LabelH);
            }
            if (_channelLabelRect != null) _channelLabelRect.sizeDelta = new Vector2(WinW - RailW - 24f, LabelH);
            if (_scrollbarRect != null) _scrollbarRect.sizeDelta = new Vector2(0f, WinH - LabelH - InputH);
            _logDirty = true;
        }
        catch
        {
        }
    }

    static bool IsPointerOverGrip()
    {
        if (_gripRect == null) return false;
        Camera camera = Core.UiCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null!
            : Core.UiCanvas.worldCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(_gripRect, Input.mousePosition, camera);
    }

    static void BeginDrag()
    {
        if (_rootRect == null) return;
        _dragging = true;
        _dragStartMouse = Input.mousePosition;
        _dragStartPos = _rootRect.anchoredPosition;
        if (_gripBg != null) _gripBg.color = new Color(1f, 1f, 1f, 0.20f);
    }

    static void DragMove()
    {
        if (_rootRect == null) return;
        try
        {
            float scale = Core.UiCanvas.scaleFactor;
            if (scale < 0.01f) scale = 1f;
            Vector2 diff = ((Vector2)Input.mousePosition - _dragStartMouse) / scale;
            _rootRect.anchoredPosition = ClampWindowPos(_dragStartPos + diff);
        }
        catch
        {
        }
    }

    static void EndDrag()
    {
        _dragging = false;
        if (_gripBg != null) _gripBg.color = new Color(1f, 1f, 1f, 0.08f);
        try
        {
            if (_rootRect == null) return;
            Services.SettingsService.WindowX = _rootRect.anchoredPosition.x;
            Services.SettingsService.WindowY = _rootRect.anchoredPosition.y;
            RefreshGeometryFields();
            Core.Log.LogInfo($"[ChatPlus] Layout pos=({_rootRect.anchoredPosition.x:0},{_rootRect.anchoredPosition.y:0}) size=({WinW:0}x{WinH:0})");
        }
        catch
        {
        }
    }

    static void OnLineCaptured(ChatLine line)
    {
        // Restored lines are seeded into the log when history loads. Do not record them
        // again, or the saved store reverses and drops the newest lines.
        if (!HistoryService.IsSeeding)
        {
            HistoryService.RecordLine(line);
            MarkActivity();
        }
        _logDirty = true;
    }

    static string Trunc(string text, int max)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        string flat = text.Replace('\n', ' ').Replace('\r', ' ');
        return flat.Length <= max ? flat : flat.Substring(0, max);
    }

    static int VisibleLength(string richText)
    {
        if (string.IsNullOrEmpty(richText)) return 0;
        int count = 0;
        int i = 0;
        while (i < richText.Length)
        {
            char c = richText[i];
            if (c == '<')
            {
                int close = richText.IndexOf('>', i + 1, Math.Min(64, richText.Length - i - 1));
                if (close >= 0 && LooksLikeTag(richText, i, close))
                {
                    i = close + 1;
                    continue;
                }
                count++;
                i++;
            }
            else
            {
                count++;
                i++;
            }
        }
        return count;
    }

    static bool LooksLikeTag(string s, int open, int close)
    {
        int i = open + 1;
        if (i <= close && s[i] == '/') i++;

        // Short color tag, for example <#FFF> or <#FFFFFF>. TextMeshPro only accepts
        // these exact inner lengths.
        if (i <= close && s[i] == '#')
        {
            int n = close - i;
            return n == 4 || n == 5 || n == 7 || n == 9;
        }

        int nameStart = i;
        // A tag name can hold a hyphen after the first letter, for example
        // line-height and font-weight.
        while (i <= close && (((s[i] >= 'a' && s[i] <= 'z') || (s[i] >= 'A' && s[i] <= 'Z')) || (s[i] == '-' && i > nameStart))) i++;
        if (i == nameStart) return false;
        if (i == close) return true;
        if (s[i] != '=') return false;
        i++;
        return i < close;
    }

    static void RecomputeVisibleOffsets()
    {
        _lineOffsets.Clear();
        int acc = 0;
        for (int i = 0; i < _lineVisLens.Count; i++)
        {
            _lineOffsets.Add(acc + i);
            acc += _lineVisLens[i];
        }
    }

    // Derive per-chat-line character offsets from TMP's OWN parsed text. Because
    // FormatLine converts message-internal newlines to <br>, every literal '\n' in
    // the parsed text IS a chat-line separator, so the index right after each '\n' is
    // the exact start of the next chat line. This is immune to tag-parsing mismatch
    // between our VisibleLength and TMP's parser (the source of hover drift).
    // Build per-chat-line character offsets by walking the RENDERED source text, not
    // TMP's parsed characters. TMP reports an extra '\n' character for every <br>, so
    // counting parsed newlines drifted the offset list past the rendered line count
    // (bottomIdx ran past the window end and blocked scrolling). Walking the source
    // counts only the real chat-line separators.
    static void RecomputeOffsetsFromSource()
    {
        try
        {
            _lineOffsets.Clear();
            _lineOffsets.Add(0);
            string s = _log != null ? _log.text : string.Empty;
            int parsed = 0;
            int nextLine = 1;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '<')
                {
                    int close = s.IndexOf('>', i + 1);
                    if (close > i && LooksLikeTag(s, i, close))
                    {
                        // TMP turns <br> into one parsed newline character.
                        if (close - i == 3 && s[i + 1] == 'b' && s[i + 2] == 'r')
                            parsed++;
                        i = close;
                        continue;
                    }
                }
                parsed++;
                if (c == '\n')
                {
                    if (nextLine < _renderedLines.Count)
                    {
                        _lineOffsets.Add(parsed);
                        nextLine++;
                    }
                }
            }
        }
        catch
        {
            RecomputeVisibleOffsets();
        }
    }

    static void RebuildLog()
    {
        if (_log == null || _scroll == null) return;

        // Never retry a broken build more often than once a second.
        if (Time.realtimeSinceStartupAsDouble < _rebuildBackoffUntil)
        {
            _logDirty = false;
            return;
        }

        var lines = ChatDataService.AllLines;
        int newest = lines.Count - 1;
        if (newest < 0)
        {
            _renderedLines.Clear();
            _lineOffsets.Clear();
            _lineVisLens.Clear();
            _lineRawStarts.Clear();
            _log.text = string.Empty;
            _windowMoved = false;
            _pendingWinStart = 0;
            _pendingWinEnd = 0;
            _anchorAllLines = -1;
            _logDirty = false;
            return;
        }

        double t0 = Time.realtimeSinceStartupAsDouble;
        bool tail = false;
        try
        {
            tail = _tailRequested;
            _tailRequested = false;

            // Anchor: the AllLines line at the viewport top before this rebuild. Tick
            // captures it when it moves the window. For a plain rebuild (message or
            // trim), capture it here against the current mesh before any clamp, so the
            // view stays put.
            bool windowMoved = _windowMoved;
            _windowMoved = false;
            int anchorAll = -1;
            if (windowMoved)
            {
                anchorAll = _anchorAllLines;
            }
            else if (!tail)
            {
                TryGetViewportLineBounds(out anchorAll, out _);
            }

            int oldWinStart = _winStart;
            if (tail)
            {
                _winEnd = newest;
                // Guard the fit result, because an over-budget single line can make
                // NewestFitStart return newest + 1 and leave the mesh empty.
                _winStart = Math.Min(NewestFitStart(lines, newest, MaxRenderChars), newest);
            }
            else if (windowMoved)
            {
                // Apply the slice Tick left pending. The stored window then always
                // matches the lines the mesh holds, even after a deferred build.
                _winEnd = Math.Clamp(_pendingWinEnd, 0, newest);
                _winStart = Math.Clamp(_pendingWinStart, 0, _winEnd);
            }
            else
            {
                _winEnd = Math.Clamp(_winEnd, 0, newest);
                _winStart = Math.Clamp(_winStart, 0, _winEnd);
            }
            windowMoved = windowMoved || _winStart != oldWinStart || tail;

            // Build the rich text while keeping _renderedLines/_lineVisLens/_lineOffsets
            // in EXACT 1:1 correspondence with what is actually rendered. The budget is
            // enforced on WHOLE lines only (never cutting mid-message).
            var sb = new System.Text.StringBuilder(4096);
            _renderedLines.Clear();
            _lineOffsets.Clear();
            _lineVisLens.Clear();
            _lineRawStarts.Clear();
            for (int i = _winStart; i <= _winEnd; i++)
            {
                ChatLine line = lines[i];
                if (_renderedLines.Count > 0) sb.Append('\n');
                string f = FormatLine(line);
                _lineRawStarts.Add(sb.Length);
                _lineVisLens.Add(VisibleLength(f));
                _renderedLines.Add(line);
                sb.Append(f);
            }

            RecomputeVisibleOffsets();
            _log.text = sb.ToString();
            _log.ForceMeshUpdate(false);
            RecomputeOffsetsFromSource();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollContent);

            _rebuildFailCount = 0;

            // Keep the previously top-visible line fixed when the window moved while
            // the user was not following the tail (shift or trim). Apply the anchor in
            // this frame, then leave the pending flag so the next Tick re-applies the
            // same value as a safety net.
            if (!tail && windowMoved && anchorAll >= _winStart && anchorAll <= _winEnd)
            {
                _anchorAllLines = anchorAll;
                _anchorTopPending = true;
                SetViewportTopToAllLines(anchorAll);
                if (_scroll != null) _lastScrollNorm = _scroll.verticalNormalizedPosition;
            }
            else if (!tail)
            {
                _anchorTopPending = false;
            }

            // Throttled diagnostic so we can watch render size / cost without spam.
            if (RebuildDiag && Time.realtimeSinceStartupAsDouble - _rebuildDiagLogAt > 10.0)
            {
                _rebuildDiagLogAt = Time.realtimeSinceStartupAsDouble;
                double ms = (Time.realtimeSinceStartupAsDouble - t0) * 1000.0;
                Core.Log.LogInfo($"[ChatPlus][Rebuild] win=[{_winStart}..{_winEnd}] newest={newest} lines={_renderedLines.Count} chars={_log.text.Length} tail={tail} meshMs={ms:0.0}");
            }

            // TEMP diagnostic: print the first and last few rendered lines so the
            // on-screen order can be compared against the saved file.
            if (OrderDiag && Time.realtimeSinceStartupAsDouble - _orderDiagAt > 1.0)
            {
                _orderDiagAt = Time.realtimeSinceStartupAsDouble;
                var diag = new System.Text.StringBuilder();
                diag.Append($"[ChatPlus][Order] win=[{_winStart}..{_winEnd}] total={lines.Count} tail={tail} first5:");
                int headEnd = Math.Min(_winEnd, _winStart + 4);
                for (int i = _winStart; i <= headEnd; i++)
                    diag.Append($" {lines[i].ReceivedUtc:HH:mm:ss}|{Trunc(lines[i].Text, 24)}");
                diag.Append(" | last5:");
                int tailStart = Math.Max(_winStart, _winEnd - 4);
                for (int i = tailStart; i <= _winEnd; i++)
                    diag.Append($" {lines[i].ReceivedUtc:HH:mm:ss}|{Trunc(lines[i].Text, 24)}");
                Core.Log.LogInfo(diag.ToString());
            }
        }
        catch (Exception ex)
        {
            _rebuildFailCount++;
            if (Time.realtimeSinceStartupAsDouble - _rebuildFailLogAt > 1.0)
            {
                _rebuildFailLogAt = Time.realtimeSinceStartupAsDouble;
                Core.Log.LogError($"[ChatPlus][Rebuild] FAILED x{_rebuildFailCount}: {ex}");
            }
            if (_rebuildFailCount >= 3)
                _rebuildBackoffUntil = Time.realtimeSinceStartupAsDouble + 1.0;
        }
        finally
        {
            _logDirty = false;
        }

        // Defer the scroll-to-bottom one frame: ScrollRect positions from bounds it
        // caches in its own LateUpdate, so setting the position in the same frame as
        // the layout rebuild would land at the OLD bottom and clip the newest line.
        // Only a tail rebuild follows the bottom. A window move keeps its anchor.
        _scrollToBottomPending = tail;

        // A rebuild changes the content height, which shifts verticalNormalizedPosition
        // without any user input. Ignore the next few frames so that resize is never
        // mistaken for the user scrolling.
        _ignoreScrollFrames = 3;
    }

    static int NewestFitStart(IReadOnlyList<ChatLine> lines, int endIdx, int charBudget)
    {
        int oldestCandidate = Math.Max(0, endIdx - MaxVisibleLines + 1);
        int acc = 0;
        for (int i = endIdx; i >= oldestCandidate; i--)
        {
            string f = FormatLine(lines[i]);
            int sep = (i > oldestCandidate) ? 1 : 0;
            if (acc + sep + f.Length > charBudget) return i + 1;
            acc += sep + f.Length;
        }
        return oldestCandidate;
    }

    // Mirror of NewestFitStart for a window anchored at the OLDEST side: returns the
    // newest line that still fits the budget starting at startIdx.
    static int OldestFitEnd(IReadOnlyList<ChatLine> lines, int startIdx, int charBudget)
    {
        int newestCandidate = Math.Min(lines.Count - 1, startIdx + MaxVisibleLines - 1);
        int acc = 0;
        for (int i = startIdx; i <= newestCandidate; i++)
        {
            string f = FormatLine(lines[i]);
            int sep = (i > startIdx) ? 1 : 0;
            if (acc + sep + f.Length > charBudget) return i - 1;
            acc += sep + f.Length;
        }
        return newestCandidate;
    }

    // Returns the AllLines indices at the top and bottom edges of the viewport,
    // derived from the current scroll position and TMP line metrics.
    static bool TryGetViewportLineBounds(out int topIdx, out int bottomIdx)
    {
        topIdx = -1;
        bottomIdx = -1;
        try
        {
            if (_renderedLines.Count == 0) return false;
            if (_scroll == null || _scroll.viewport == null || _log == null || _scrollContent == null) return true;

            float v = _scroll.verticalNormalizedPosition;
            float totalH = _scrollContent.rect.height;
            float vpH = _scroll.viewport.rect.height;
            if (vpH <= 0f) vpH = 1f;

            var textInfo = _log.textInfo;

            // Scale TMP line heights to the ScrollRect content height so the fractional
            // scroll offset maps onto the right lines.
            float totalLineH = 0f;
            for (int i = 0; i < textInfo.lineCount; i++)
            {
                var li = textInfo.lineInfo[i];
                if (li.characterCount > 0) totalLineH += li.ascender - li.descender;
            }
            if (totalLineH <= 0f) return false;
            float scale = totalH / totalLineH;

            float topOffset = Mathf.Clamp01(1f - v) * Mathf.Max(0f, totalH - vpH);
            float bottomOffset = Mathf.Clamp(topOffset + vpH, 0f, totalH);

            // Single forward pass. TMP's firstCharacterIndex and _lineOffsets are both
            // ascending, so advance one pointer instead of rescanning _lineOffsets for
            // every visual line.
            int r = 0;
            float acc = 0f;
            bool topFound = false;
            bool bottomFound = false;
            for (int i = 0; i < textInfo.lineCount; i++)
            {
                var li = textInfo.lineInfo[i];
                if (li.characterCount == 0) continue;
                int ch = li.firstCharacterIndex;
                while (r + 1 < _lineOffsets.Count && _lineOffsets[r + 1] <= ch) r++;
                float h = (li.ascender - li.descender) * scale;
                float lineEnd = acc + h;
                if (!topFound && lineEnd >= topOffset)
                {
                    topIdx = _winStart + r;
                    topFound = true;
                }
                if (!bottomFound && lineEnd >= bottomOffset)
                {
                    bottomIdx = _winStart + r;
                    bottomFound = true;
                }
                if (topFound && bottomFound) break;
                acc = lineEnd;
            }
            if (!topFound) topIdx = _winStart + (_renderedLines.Count - 1);
            if (!bottomFound) bottomIdx = _winStart + (_renderedLines.Count - 1);
            // Safety net: a stale or drifted mapping must never put the edges outside
            // the rendered window, or the shift math sees negative margins.
            int minIdx = _winStart;
            int maxIdx = _winStart + _renderedLines.Count - 1;
            topIdx = Math.Clamp(topIdx, minIdx, maxIdx);
            bottomIdx = Math.Clamp(bottomIdx, minIdx, maxIdx);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Scaled pixel offset from the content top to the first visual line of the given
    // chat line, for the mesh that is currently loaded.
    static bool TryGetChatLinePixelTop(int allLinesIdx, out float pixelTop)
    {
        pixelTop = 0f;
        try
        {
            if (_log == null || _scrollContent == null) return false;
            int idx = allLinesIdx - _winStart;
            if (idx < 0 || idx >= _renderedLines.Count || _lineOffsets.Count != _renderedLines.Count) return false;
            int start = _lineOffsets[idx];
            var textInfo = _log.textInfo;

            float totalLineH = 0f;
            for (int i = 0; i < textInfo.lineCount; i++)
            {
                var li = textInfo.lineInfo[i];
                if (li.characterCount > 0) totalLineH += li.ascender - li.descender;
            }
            if (totalLineH <= 0f) return false;

            float above = 0f;
            for (int i = 0; i < textInfo.lineCount; i++)
            {
                var li = textInfo.lineInfo[i];
                if (li.characterCount == 0) continue;
                if (li.firstCharacterIndex >= start) break;
                above += li.ascender - li.descender;
            }

            float scale = _scrollContent.rect.height / totalLineH;
            pixelTop = above * scale;
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Pixel offset of the viewport top from the content top, matching the mapping in
    // TryGetViewportLineBounds.
    static float ViewportTopPixels()
    {
        try
        {
            if (_scroll == null || _scroll.viewport == null || _scrollContent == null) return 0f;
            float totalH = _scrollContent.rect.height;
            float vpH = _scroll.viewport.rect.height;
            return Mathf.Clamp01(1f - _scroll.verticalNormalizedPosition) * Mathf.Max(0f, totalH - vpH);
        }
        catch
        {
            return 0f;
        }
    }

    static void SetViewportTopToAllLines(int allLinesIdx)
    {
        try
        {
            if (_scroll == null || _scroll.viewport == null || _log == null || _scrollContent == null) return;
            if (!TryGetChatLinePixelTop(allLinesIdx, out float above)) return;

            float totalH = _scrollContent.rect.height;
            float vpH = _scroll.viewport.rect.height;
            // Preserve the sub-line offset the viewport had on the anchor line, so the
            // rebuild does not snap to the top of the line and hop.
            above = Mathf.Clamp(above + _anchorSubPixel, 0f, totalH);

            if (totalH <= vpH)
            {
                _scroll.verticalNormalizedPosition = 1f;
                return;
            }
            float range = totalH - vpH;
            float pos = (range - above) / range;
            _scroll.verticalNormalizedPosition = Mathf.Clamp01(pos);
        }
        catch
        {
        }
    }

    // Tag names that TextMeshPro can close with a matching </name> tag in this game
    // build. Derived from the SLASH_ MarkupTag list in the game's
    // Unity.TextMeshPro assembly.
    static readonly System.Collections.Generic.HashSet<string> ClosableRichTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "color", "size", "b", "i", "u", "s", "mark", "font", "style", "material",
        "link", "gradient", "align", "indent", "margin", "voffset", "cspace",
        "mspace", "nobr", "noparse", "sub", "sup", "uppercase", "lowercase",
        "allcaps", "smallcaps", "liga", "frac", "space", "width", "pos", "scale",
        "rotate", "page", "a", "action", "line-height", "line-indent", "font-weight",
    };

    // Close every tag that a captured message leaves open, so the tag cannot leak
    // into the next log line. The whole log is one TextMeshPro string, so one open
    // <size> or <color> would otherwise change every line after it. Unmatched
    // closing tags are dropped, because they would pop ChatPlus's own color span.
    static string BalanceRichText(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('<') < 0) return text;

        System.Collections.Generic.List<string>? open = null;
        System.Text.StringBuilder? sb = null;
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == '<')
            {
                int close = text.IndexOf('>', i + 1);
                if (close > i && LooksLikeTag(text, i, close))
                {
                    string inner = text.Substring(i + 1, close - i - 1);
                    bool closing = inner.Length > 0 && inner[0] == '/';
                    bool shortColor = !closing && inner.Length > 0 && inner[0] == '#';
                    string name = closing
                        ? TagNameOf(inner.Substring(1))
                        : (shortColor ? "color" : TagNameOf(inner));

                    // <noparse> disables tag parsing until its close. Copy the whole
                    // region verbatim so this walk and TextMeshPro agree.
                    if (!closing && name.Equals("noparse", StringComparison.OrdinalIgnoreCase))
                    {
                        const string closeTag = "</noparse>";
                        int end = text.IndexOf(closeTag, close + 1, StringComparison.OrdinalIgnoreCase);
                        int stop = end < 0 ? text.Length : end + closeTag.Length;
                        (sb ??= new System.Text.StringBuilder(text.Length + 16)).Append(text, i, stop - i);
                        i = stop;
                        continue;
                    }

                    sb ??= new System.Text.StringBuilder(text.Length + 16);
                    if (closing)
                    {
                        if (open != null && open.Count > 0 && string.Equals(open[^1], name, StringComparison.OrdinalIgnoreCase))
                        {
                            open.RemoveAt(open.Count - 1);
                            sb.Append(text, i, close - i + 1);
                        }
                    }
                    else
                    {
                        sb.Append(text, i, close - i + 1);
                        if (ClosableRichTags.Contains(name))
                        {
                            open ??= new System.Collections.Generic.List<string>();
                            open.Add(name);
                        }
                    }

                    i = close + 1;
                    continue;
                }
            }

            (sb ??= new System.Text.StringBuilder(text.Length + 16)).Append(text[i]);
            i++;
        }

        if (open != null)
        {
            for (int k = open.Count - 1; k >= 0; k--)
            {
                sb!.Append("</").Append(open[k]).Append('>');
            }
        }

        return sb?.ToString() ?? text;
    }

    static string TagNameOf(string s)
    {
        int i = 0;
        while (i < s.Length && (((s[i] >= 'a' && s[i] <= 'z') || (s[i] >= 'A' && s[i] <= 'Z')) || (s[i] == '-' && i > 0))) i++;
        return i > 0 ? s.Substring(0, i) : s;
    }

    static string FormatLine(ChatLine line)
    {
        var sb = new System.Text.StringBuilder(96);
        if (Services.SettingsService.ShowTimestamp)
        {
            string ts = line.ReceivedUtc.ToLocalTime().ToString("HH:mm:ss");
            sb.Append($"<color=#808080>[{ts}]</color> ");
        }
        if (Services.SettingsService.ShowChannelIndicator)
        {
            string chTag = ColorUtility.ToHtmlStringRGB(Theme.ChannelLabelColor(line.Channel));
            sb.Append($"<color=#{chTag}>[{ChannelCode(line.Channel)}]</color> ");
        }
        string sender = line.Sender.Length == 0 ? (line.Channel == ChatChannel.System ? "System" : "?") : line.Sender;
        // External text can open a rich-text tag and never close it. The whole log is
        // one TextMeshPro string, so an open <size> or <color> would change every line
        // after it. Balance each captured string so its tags end within this line.
        sender = BalanceRichText(sender);
        // Convert message-internal newlines to <br> so the ONLY literal \n in the
        // rendered text are the chat-line separators. That lets the hover mapping be
        // derived from TMP's own parsed characterInfo instead of our own tag parsing.
        string body = BalanceRichText(line.Text).Replace("\n", "<br>");
        bool isSelf = line.Channel != ChatChannel.System && line.SenderId != default && Core.HasInitialized &&
            Core.LocalUser != Entity.Null && line.SenderId == Core.LocalUser.GetNetworkId();
        bool isAdmin = line.Channel != ChatChannel.System &&
            Services.AdminService.IsAdmin(line.SenderId, line.Sender);
        // The name and the body resolve colors independently. Each falls through
        // admin, then self, then the channel color. A group switched off for this
        // channel is skipped. Admin styling wins over self styling.
        string name = isAdmin ? sender + " [ADMIN]" : sender;
        string nameTag = ColorUtility.ToHtmlStringRGB(ResolveNameColor(line, isAdmin, isSelf));
        string msgTag = ColorUtility.ToHtmlStringRGB(ResolveMessageColor(line, isAdmin, isSelf));
        sb.Append($"<color=#{nameTag}>{name}:</color> <color=#{msgTag}>{body}</color>");
        return sb.ToString();
    }

    static Color ResolveNameColor(ChatLine line, bool isAdmin, bool isSelf)
    {
        if (isAdmin && Services.SettingsService.IsColorScopeEnabled(ChatColorGroup.AdminName, line.Channel))
            return Theme.AdminNameColor;
        if (isSelf && Services.SettingsService.IsColorScopeEnabled(ChatColorGroup.SelfName, line.Channel))
            return Theme.SelfNameColor;
        return Theme.ForChannel(line.Channel);
    }

    static Color ResolveMessageColor(ChatLine line, bool isAdmin, bool isSelf)
    {
        if (isAdmin && Services.SettingsService.IsColorScopeEnabled(ChatColorGroup.AdminMessage, line.Channel))
            return Theme.AdminMessageColor;
        if (isSelf && Services.SettingsService.IsColorScopeEnabled(ChatColorGroup.SelfMessage, line.Channel))
            return Theme.SelfMessageColor;
        return Theme.ForChannel(line.Channel);
    }

    static string ChannelCode(ChatChannel channel) => channel switch
    {
        ChatChannel.Global => "G",
        ChatChannel.Local => "L",
        ChatChannel.Clan => "C",
        ChatChannel.Whisper => "W",
        ChatChannel.System => "S",
        _ => "?",
    };

    static string FormatLinePlain(ChatLine line)
    {
        string sender = line.Sender.Length == 0 ? (line.Channel == ChatChannel.System ? "System" : "?") : line.Sender;
        var sb = new System.Text.StringBuilder(96);
        if (Services.SettingsService.ShowTimestamp)
        {
            sb.Append($"[{line.ReceivedUtc.ToLocalTime().ToString("HH:mm:ss")}] ");
        }
        if (Services.SettingsService.ShowChannelIndicator)
        {
            sb.Append($"[{ChannelCode(line.Channel)}] ");
        }
        bool isAdmin = line.Channel != ChatChannel.System &&
            Services.AdminService.IsAdmin(line.SenderId, line.Sender);
        sb.Append(isAdmin ? $"{sender} [ADMIN]: {line.Text}" : $"{sender}: {line.Text}");
        return sb.ToString();
    }

    static void CopyAllToClipboard()
    {
        try
        {
            var lines = _renderedLines.Count > 0 ? _renderedLines : ChatDataService.AllLines;
            if (lines.Count == 0)
            {
                FlashCopyFeedback("Empty");
                return;
            }

            var sb = new System.Text.StringBuilder(8192);
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(FormatLinePlain(lines[i]));
            }
            GUIUtility.systemCopyBuffer = sb.ToString();
            FlashCopyFeedback($"Copied {lines.Count}");
            Core.Log.LogInfo($"[ChatPlus] Copied {lines.Count} line(s) to clipboard.");
        }
        catch (Exception ex)
        {
            Core.Log.LogError($"[ChatPlus] Copy failed: {ex.Message}");
        }
    }

    static void FlashCopyFeedback(string msg)
    {
        if (_copyLabel != null) _copyLabel.text = msg;
        _copyFeedbackUntil = Time.realtimeSinceStartupAsDouble + 1.5;
    }

    static bool IsPointerOverLogViewport()
    {
        if (_scroll == null || _scroll.viewport == null) return false;
        Camera camera = Core.UiCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null!
            : Core.UiCanvas.worldCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(_scroll.viewport, Input.mousePosition, camera);
    }

    static int ResolveLineAtPoint(Vector2 screenPos)
    {
        try
        {
            if (_log == null || _renderedLines.Count == 0 || _lineOffsets.Count != _renderedLines.Count ||
                _lineVisLens.Count != _renderedLines.Count || _lineRawStarts.Count != _renderedLines.Count) return -1;
            Camera camera = Core.UiCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null!
                : Core.UiCanvas.worldCamera;
            int ch = TMP_TextUtilities.FindIntersectingCharacter(_log, screenPos, camera, false);
            if (ch >= 0)
            {
                int idx = 0;
                for (int i = 0; i < _lineOffsets.Count; i++)
                {
                    if (_lineOffsets[i] <= ch) idx = i;
                    else break;
                }
                if (idx >= 0 && idx < _renderedLines.Count) return idx;
            }
            return ResolveLineByProximity(screenPos, camera);
        }
        catch
        {
            return -1;
        }
    }

    static bool BoundsForLine(int idx, out float top, out float bottom)
    {
        top = 0f;
        bottom = 0f;
        try
        {
            if (_log == null || idx < 0 || idx >= _renderedLines.Count ||
                _lineOffsets.Count != _renderedLines.Count) return false;
            int start = _lineOffsets[idx];
            var textInfo = _log.textInfo;
            int end = (idx + 1 < _lineOffsets.Count) ? _lineOffsets[idx + 1] : textInfo.characterCount;
            bool any = false;
            for (int i = 0; i < textInfo.lineCount; i++)
            {
                var lineInfo = textInfo.lineInfo[i];
                if (lineInfo.characterCount == 0) continue;
                int first = lineInfo.firstCharacterIndex;
                if (first < start || first >= end) continue;
                if (!any)
                {
                    top = lineInfo.ascender;
                    bottom = lineInfo.descender;
                    any = true;
                }
                else
                {
                    if (lineInfo.ascender > top) top = lineInfo.ascender;
                    if (lineInfo.descender < bottom) bottom = lineInfo.descender;
                }
            }
            return any;
        }
        catch
        {
            return false;
        }
    }

    static int ResolveLineByProximity(Vector2 screenPos, Camera camera)
    {
        try
        {
            if (_log == null || _scrollContent == null || _renderedLines.Count == 0) return -1;
            RectTransform content = _scrollContent;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(content, screenPos, camera, out Vector2 local))
                return -1;
            float localY = local.y;
            int best = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < _renderedLines.Count; i++)
            {
                float top;
                float bottom;
                if (!BoundsForLine(i, out top, out bottom)) continue;
                float center = (top + bottom) * 0.5f;
                float half = (top - bottom) * 0.5f + 6f;
                float d = Math.Abs(localY - center) - half;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }
            if (best < 0 || bestDist > 0f) return -1;
            return best;
        }
        catch
        {
            return -1;
        }
    }

    static void CopyLineAtPoint(Vector2 screenPos)
    {
        try
        {
            int idx = ResolveLineAtPoint(screenPos);
            if (idx < 0) return;
            GUIUtility.systemCopyBuffer = FormatLinePlain(_renderedLines[idx]);
            if (_channelLabel != null) _channelLabel.text = "Copied line!";
            _channelLabelFeedbackUntil = Time.realtimeSinceStartupAsDouble + 1.5;
            Core.Log.LogInfo("[ChatPlus] Copied 1 line to clipboard.");
        }
        catch (Exception ex)
        {
            Core.Log.LogDebug($"[ChatPlus] Line copy skipped: {ex.Message}");
        }
    }

    static void UpdateHoverHighlight()
    {
        try
        {
            bool track = (_forceHoverRecompute || (_alpha > 0.05f && _chatActive)) &&
            !Services.ConsoleGateService.IsConsoleOpen && !_viewSettings && _highlightObj != null && _highlight != null &&
                _highlightRect != null && _log != null && _scroll != null && _scroll.viewport != null &&
                IsPointerOverLogViewport();
            _forceHoverRecompute = false;
            int idx = track ? ResolveLineAtPoint(Input.mousePosition) : -1;
            _hoverLineIdx = idx;

            if (idx < 0 || _scrollContent == null)
            {
                if (_highlightObj != null && _highlightObj.activeSelf) _highlightObj.SetActive(false);
                return;
            }

            float topY;
            float bottomY;
            if (!BoundsForLine(idx, out topY, out bottomY)) return;

            GameObject? hlObj = _highlightObj;
            RectTransform? hlRect = _highlightRect;
            Image? hlImg = _highlight;
            if (hlObj == null || hlRect == null || hlImg == null) return;

            float h = topY - bottomY;
            if (h <= 0.5f)
            {
                if (hlObj.activeSelf) hlObj.SetActive(false);
                return;
            }

            hlRect.anchoredPosition = new Vector2(0f, topY);
            hlRect.sizeDelta = new Vector2(0f, h);
            float alpha = Time.realtimeSinceStartupAsDouble < _highlightFlashUntil ? 0.18f : 0.09f;
            hlImg.color = new Color(1f, 1f, 1f, alpha);
            if (!hlObj.activeSelf) hlObj.SetActive(true);
        }
        catch
        {
            try { if (_highlightObj != null && _highlightObj.activeSelf) _highlightObj.SetActive(false); } catch { }
        }
    }

    static void FlashHighlight()
    {
        _highlightFlashUntil = Time.realtimeSinceStartupAsDouble + 0.15;
    }
}