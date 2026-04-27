using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Runtime UI — "Cosmic Glass" design system.
/// All visuals built in code — no prefabs or art assets needed.
///
/// Design tokens come from the Claude Design handoff (oklch palette → sRGB).
///
/// Layout:
///   Top-left  — [LV 01] pill  +  [SCORE · BEST] pill
///   Top-right — [☰] menu icon-button
///   Bot-left  — NEXT UP queue (3 upcoming planets with name + score value)
///   Overlays  — Settings (full), Game Over (full), Win (full)
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Canvas")]
    public Canvas canvas;

    // ── Design tokens (oklch → sRGB approximate) ─────────────────
    // Glass surfaces
    static readonly Color GlassBg     = new Color(1f, 1f, 1f, 0.045f);
    static readonly Color GlassBgStr  = new Color(1f, 1f, 1f, 0.075f);
    static readonly Color GlassBdr    = new Color(1f, 1f, 1f, 0.10f);
    static readonly Color GlassBdrStr = new Color(1f, 1f, 1f, 0.20f);

    // Accents
    static readonly Color AccCyan   = new Color(0.02f, 0.84f, 1.00f, 1f); // oklch(0.82 0.14 220)
    static readonly Color AccViolet = new Color(0.60f, 0.28f, 0.92f, 1f); // oklch(0.72 0.18 300)
    static readonly Color AccAmber  = new Color(0.96f, 0.75f, 0.12f, 1f); // oklch(0.85 0.16 75)
    static readonly Color AccRed    = new Color(0.90f, 0.23f, 0.18f, 1f); // oklch(0.70 0.22 25)

    // Ink
    static readonly Color Ink0 = new Color(0.965f, 0.953f, 1.00f, 1.00f); // #F6F3FF
    static readonly Color Ink1 = new Color(0.965f, 0.953f, 1.00f, 0.85f);
    static readonly Color Ink2 = new Color(0.965f, 0.953f, 1.00f, 0.55f);
    static readonly Color Ink3 = new Color(0.965f, 0.953f, 1.00f, 0.32f);
    static readonly Color Ink4 = new Color(0.965f, 0.953f, 1.00f, 0.14f);

    // ── Live refs ──────────────────────────────────────────────────
    TMP_Text _scoreTxt, _bestTxt, _levelTxt;
    TMP_Text _goScoreTxt, _goBestTxt;
    TMP_Text _winScoreTxt;
    TMP_Text _musicToggleTxt, _soundToggleTxt;
    Image    _musicToggleImg, _soundToggleImg;

    readonly List<Image>    _qDots   = new List<Image>();
    readonly List<TMP_Text> _qNames  = new List<TMP_Text>();
    readonly List<TMP_Text> _qScores = new List<TMP_Text>();

    GameObject _goPanel, _winPanel, _settingsOverlay;

    readonly Dictionary<int, Sprite> _roundedCache = new Dictionary<int, Sprite>();

    // ─────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();

        var cs = canvas.GetComponent<CanvasScaler>();
        if (cs == null) cs = canvas.gameObject.AddComponent<CanvasScaler>();
        cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1080f, 1920f);
        cs.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight  = 0.5f;

        // GraphicRaycaster is required for Button clicks on a runtime-built canvas
        if (canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        Build();
    }

    // ─────────────────────────────────────────────────────────────
    #region Public API

    public void UpdateScore(int score, int best)
    {
        if (_scoreTxt) _scoreTxt.text = score.ToString("N0");
        if (_bestTxt)  _bestTxt.text  = best.ToString("N0");
    }

    public void UpdateLevel(int level)
    {
        if (_levelTxt) _levelTxt.text = level.ToString("D2");
    }

    public void UpdateQueue(List<PlanetData> upcoming)
    {
        for (int i = 0; i < _qDots.Count; i++)
        {
            bool has = i < upcoming.Count && upcoming[i] != null;
            _qDots[i].color = has ? upcoming[i].primaryColor : Ink4;
            if (_qNames.Count  > i) _qNames[i].text  = has ? upcoming[i].planetName.ToUpper() : "";
            if (_qScores.Count > i) _qScores[i].text = has ? $"+{upcoming[i].scoreOnMerge}" : "";
        }
    }

    public void HideOverlays()
    {
        if (_goPanel)         _goPanel.SetActive(false);
        if (_winPanel)        _winPanel.SetActive(false);
        if (_settingsOverlay) _settingsOverlay.SetActive(false);
    }

    public void ShowGameOver()
    {
        if (_goPanel) _goPanel.SetActive(true);
        int score = ScoreManager.Instance?.CurrentScore ?? 0;
        int best  = ScoreManager.Instance?.HighScore    ?? 0;
        if (_goScoreTxt) _goScoreTxt.text = score.ToString("N0");
        if (_goBestTxt)  _goBestTxt.text  = Mathf.Max(score, best).ToString("N0");
    }

    public void ShowWin(int score)
    {
        if (_winPanel)    _winPanel.SetActive(true);
        if (_winScoreTxt) _winScoreTxt.text = score.ToString("N0");
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Layout Build

    void Build()
    {
        var root = canvas.GetComponent<RectTransform>();
        BuildTopRow(root);
        BuildNextQueue(root);
        BuildMenuButton(root);
        BuildSettingsOverlay(root);
        BuildGameOverPanel(root);
        BuildWinPanel(root);

        UpdateScore(0, ScoreManager.Instance?.HighScore ?? 0);
        UpdateLevel(1);
    }

    // ── Top row: [LV] + [SCORE | BEST] ───────────────────────────
    void BuildTopRow(RectTransform root)
    {
        const float H   = 72f;
        const float TOP = -20f;

        // Level pill
        var lvl = GlassPill(root, "LvPill",
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(20f, TOP - H), new Vector2(108f, TOP));
        AddBorder(lvl, GlassBdr);

        Label(lvl, "Lb", "LV", 9, TextAlignmentOptions.Center, Ink2,
            new Vector2(0f, 0.56f), new Vector2(1f, 1f));
        _levelTxt = Label(lvl, "LT", "01", 22, TextAlignmentOptions.Center, Ink0,
            new Vector2(0f, 0f), new Vector2(1f, 0.58f));

        // Score + Best pill
        var sc = GlassPill(root, "ScPill",
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(120f, TOP - H), new Vector2(466f, TOP));
        AddBorder(sc, GlassBdr);

        // Amber dot icon
        var dot = MakeRect(sc, "Dot",
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(14f, -13f), new Vector2(40f, 13f));
        var di = dot.gameObject.AddComponent<Image>();
        di.sprite = CircleSprite();
        di.color  = AccAmber;
        di.raycastTarget = false;

        // Score column
        Label(sc, "SLb", "SCORE", 9, TextAlignmentOptions.Left, Ink2,
            new Vector2(0f, 0.54f), new Vector2(0.55f, 1f),
            new Vector2(50f, 0f), new Vector2(-4f, 0f));
        _scoreTxt = Label(sc, "SV", "0", 22, TextAlignmentOptions.Left, Ink0,
            new Vector2(0f, 0.02f), new Vector2(0.55f, 0.56f),
            new Vector2(50f, 0f), new Vector2(-4f, 0f));

        // Vertical divider
        var dv = MakeRect(sc, "Dv",
            new Vector2(0.56f, 0.12f), new Vector2(0.562f, 0.88f),
            Vector2.zero, Vector2.zero);
        FillRect(dv, Ink4);

        // Best column
        Label(sc, "BLb", "BEST", 9, TextAlignmentOptions.Left, Ink2,
            new Vector2(0.58f, 0.54f), new Vector2(1f, 1f),
            Vector2.zero, new Vector2(-14f, 0f));
        _bestTxt = Label(sc, "BV", "0", 16, TextAlignmentOptions.Left, Ink2,
            new Vector2(0.58f, 0.02f), new Vector2(1f, 0.56f),
            Vector2.zero, new Vector2(-14f, 0f));
    }

    // ── Bottom-left: NEXT UP queue ────────────────────────────────
    void BuildNextQueue(RectTransform root)
    {
        const float BOT = 70f;
        const float W   = 230f;
        const float H   = 210f;

        var panel = GlassPill(root, "NxtQ",
            new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(20f, BOT), new Vector2(20f + W, BOT + H));
        AddBorder(panel, GlassBdr);

        // "NEXT UP" header
        Label(panel, "NH", "NEXT UP", 9, TextAlignmentOptions.Left, AccCyan,
            new Vector2(0f, 0.87f), new Vector2(1f, 1f),
            new Vector2(16f, 0f), new Vector2(-16f, 0f));

        // Cyan underline
        var ul = MakeRect(panel, "UL",
            new Vector2(0.05f, 0.847f), new Vector2(0.95f, 0.851f),
            Vector2.zero, Vector2.zero);
        FillRect(ul, new Color(AccCyan.r, AccCyan.g, AccCyan.b, 0.28f));

        // 3 planet queue slots
        float[] y0s = { 0.59f, 0.31f, 0.04f };
        float[] y1s = { 0.84f, 0.56f, 0.29f };

        for (int i = 0; i < 3; i++)
        {
            var slot = MakeRect(panel, $"Q{i}",
                new Vector2(0f, y0s[i]), new Vector2(1f, y1s[i]),
                new Vector2(14f, 2f), new Vector2(-14f, -2f));

            // Planet colour dot
            var dotR = MakeRect(slot, "D",
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(0f, 5f), new Vector2(26f, -5f));
            var img = dotR.gameObject.AddComponent<Image>();
            img.sprite = CircleSprite();
            img.color  = Ink4;
            img.raycastTarget = false;
            _qDots.Add(img);

            // Planet name
            var nl = Label(slot, "N", "", 13, TextAlignmentOptions.Left, Ink0,
                new Vector2(0f, 0.47f), new Vector2(0.85f, 1f),
                new Vector2(34f, 0f), Vector2.zero);
            _qNames.Add(nl);

            // Score value
            var sl = Label(slot, "S", "", 10, TextAlignmentOptions.Left, Ink3,
                new Vector2(0f, 0f), new Vector2(0.85f, 0.50f),
                new Vector2(34f, 0f), Vector2.zero);
            _qScores.Add(sl);

            // Fade later items
            if (i > 0)
            {
                var cg = slot.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = i == 1 ? 0.70f : 0.42f;
            }
        }
    }

    // ── Top-right: ☰ Menu button ──────────────────────────────────
    void BuildMenuButton(RectTransform root)
    {
        const float SZ  = 72f;
        const float TOP = -20f;

        var btn = MakeRect(root, "MenuBtn",
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-SZ - 20f, TOP - SZ), new Vector2(-20f, TOP));

        var img = btn.gameObject.AddComponent<Image>();
        img.sprite = RoundedSprite(18);
        img.type   = Image.Type.Sliced;
        img.color  = GlassBgStr;
        img.raycastTarget = true;

        AddBorder(btn, GlassBdr);
        Label(btn, "Ic", "☰", 28, TextAlignmentOptions.Center, Ink0);

        var b = btn.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(() =>
            _settingsOverlay?.SetActive(!_settingsOverlay.activeSelf));
    }

    // ── Settings overlay (full-screen glass card) ─────────────────
    void BuildSettingsOverlay(RectTransform root)
    {
        _settingsOverlay = FullOverlay(root, "Settings",
            new Color(0.01f, 0.004f, 0.047f, 0.88f));
        var ort = _settingsOverlay.GetComponent<RectTransform>();

        // Glass card
        var card = GlassPill(ort, "Card",
            new Vector2(0.06f, 0.30f), new Vector2(0.94f, 0.72f),
            Vector2.zero, Vector2.zero);
        AddBorder(card, GlassBdrStr);

        // Title
        Label(card, "Ti", "Settings", 24, TextAlignmentOptions.Left, Ink0,
            new Vector2(0f, 0.82f), new Vector2(0.76f, 1f),
            new Vector2(20f, 0f), Vector2.zero);

        // Close button
        var xR = MakeRect(card, "X",
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-54f, -54f), new Vector2(-12f, -12f));
        var xi = xR.gameObject.AddComponent<Image>();
        xi.sprite = CircleSprite();
        xi.color  = GlassBgStr;
        xi.raycastTarget = true;
        Label(xR, "L", "✕", 14, TextAlignmentOptions.Center, Ink1);
        var xb = xR.gameObject.AddComponent<Button>();
        xb.targetGraphic = xi;
        xb.onClick.AddListener(() => _settingsOverlay.SetActive(false));

        // Divider under title
        FillRect(MakeRect(card, "TD",
            new Vector2(0f, 0.79f), new Vector2(1f, 0.793f),
            new Vector2(16f, 0f), new Vector2(-16f, 0f)), Ink4);

        // AUDIO label
        Label(card, "AL", "AUDIO", 9, TextAlignmentOptions.Left, Ink2,
            new Vector2(0f, 0.71f), new Vector2(1f, 0.79f),
            new Vector2(20f, 0f), Vector2.zero);

        // Music row + toggle
        BuildSettingsRow(card, "MRow", 0.54f, 0.70f, "♪", "Music", "Background score");
        var mBR = MakeRect(card, "MT",
            new Vector2(0.66f, 0.56f), new Vector2(1f, 0.68f),
            Vector2.zero, new Vector2(-16f, 0f));
        bool mOn = AudioManager.Instance == null || AudioManager.Instance.MusicEnabled;
        _musicToggleImg = mBR.gameObject.AddComponent<Image>();
        _musicToggleImg.sprite = RoundedSprite(12);
        _musicToggleImg.type   = Image.Type.Sliced;
        _musicToggleImg.color  = mOn ? new Color(AccCyan.r, AccCyan.g, AccCyan.b, 0.55f) : GlassBg;
        _musicToggleImg.raycastTarget = true;
        _musicToggleTxt = Label(mBR, "T", mOn ? "ON" : "OFF", 11,
            TextAlignmentOptions.Center, mOn ? Color.black : Ink2);
        var mb = mBR.gameObject.AddComponent<Button>();
        mb.targetGraphic = _musicToggleImg;
        mb.onClick.AddListener(ToggleMusic);

        // Sound row + toggle
        BuildSettingsRow(card, "SRow", 0.37f, 0.53f, "♫", "Sound Effects", "Merges & launches");
        var sBR = MakeRect(card, "ST",
            new Vector2(0.66f, 0.39f), new Vector2(1f, 0.51f),
            Vector2.zero, new Vector2(-16f, 0f));
        bool sOn = AudioManager.Instance == null || AudioManager.Instance.SoundEnabled;
        _soundToggleImg = sBR.gameObject.AddComponent<Image>();
        _soundToggleImg.sprite = RoundedSprite(12);
        _soundToggleImg.type   = Image.Type.Sliced;
        _soundToggleImg.color  = sOn ? new Color(AccCyan.r, AccCyan.g, AccCyan.b, 0.55f) : GlassBg;
        _soundToggleImg.raycastTarget = true;
        _soundToggleTxt = Label(sBR, "T", sOn ? "ON" : "OFF", 11,
            TextAlignmentOptions.Center, sOn ? Color.black : Ink2);
        var sb = sBR.gameObject.AddComponent<Button>();
        sb.targetGraphic = _soundToggleImg;
        sb.onClick.AddListener(ToggleSound);

        // Bottom buttons
        AccentButton(card, "Rest", "↺  RESTART",
            new Vector2(0.04f, 0.05f), new Vector2(0.48f, 0.22f), AccCyan,
            () => { HideOverlays(); GameManager.Instance?.RestartGame(); });
        AccentButton(card, "Home", "⌂  HOME",
            new Vector2(0.52f, 0.05f), new Vector2(0.96f, 0.22f), Ink3,
            () => _settingsOverlay.SetActive(false));

        _settingsOverlay.SetActive(false);
    }

    // ── Game Over overlay ─────────────────────────────────────────
    void BuildGameOverPanel(RectTransform root)
    {
        _goPanel = FullOverlay(root, "GameOver",
            new Color(0.01f, 0.004f, 0.047f, 0.88f));
        var ort = _goPanel.GetComponent<RectTransform>();

        var card = GlassPill(ort, "Card",
            new Vector2(0.06f, 0.16f), new Vector2(0.94f, 0.84f),
            Vector2.zero, Vector2.zero);
        AddBorder(card, GlassBdrStr);

        // Red accent glow band at top
        FillRect(MakeRect(card, "G",
            new Vector2(0f, 0.88f), new Vector2(1f, 1f),
            Vector2.zero, Vector2.zero),
            new Color(AccRed.r, AccRed.g, AccRed.b, 0.18f));

        // "BOUNDARY BREACHED"
        Label(card, "Sub", "BOUNDARY BREACHED", 10,
            TextAlignmentOptions.Center, AccRed,
            new Vector2(0f, 0.84f), new Vector2(1f, 0.93f));

        // "Game Over"
        Label(card, "Ti", "Game Over", 46,
            TextAlignmentOptions.Center, new Color(1f, 0.60f, 0.55f, 1f),
            new Vector2(0f, 0.73f), new Vector2(1f, 0.86f));

        // Subtitle
        Label(card, "Su", "A planet drifted beyond the gravity zone.", 11,
            TextAlignmentOptions.Center, Ink2,
            new Vector2(0.04f, 0.65f), new Vector2(0.96f, 0.74f));

        // Score / Best stat card
        var sc = GlassPill(card, "SC",
            new Vector2(0.05f, 0.43f), new Vector2(0.95f, 0.62f),
            Vector2.zero, Vector2.zero);
        AddBorder(sc, GlassBdr);

        Label(sc, "SLb", "FINAL SCORE", 9, TextAlignmentOptions.Left, Ink2,
            new Vector2(0f, 0.53f), new Vector2(0.54f, 1f),
            new Vector2(16f, 0f), Vector2.zero);
        _goScoreTxt = Label(sc, "SV", "0", 30, TextAlignmentOptions.Left, AccAmber,
            new Vector2(0f, 0.02f), new Vector2(0.54f, 0.56f),
            new Vector2(16f, 0f), Vector2.zero);

        FillRect(MakeRect(sc, "Dv",
            new Vector2(0.56f, 0.10f), new Vector2(0.562f, 0.90f),
            Vector2.zero, Vector2.zero), Ink4);

        Label(sc, "BLb", "BEST", 9, TextAlignmentOptions.Left, Ink2,
            new Vector2(0.58f, 0.53f), new Vector2(1f, 1f),
            Vector2.zero, new Vector2(-16f, 0f));
        _goBestTxt = Label(sc, "BV", "0", 20, TextAlignmentOptions.Left, Ink1,
            new Vector2(0.58f, 0.02f), new Vector2(1f, 0.56f),
            Vector2.zero, new Vector2(-16f, 0f));

        // Buttons
        AccentButton(card, "Home", "⌂  HOME",
            new Vector2(0.04f, 0.05f), new Vector2(0.46f, 0.20f), Ink3,
            () => { HideOverlays(); GameManager.Instance?.RestartGame(); });
        AccentButton(card, "Retry", "↺  RETRY",
            new Vector2(0.54f, 0.05f), new Vector2(0.96f, 0.20f), AccCyan,
            () => { HideOverlays(); GameManager.Instance?.RestartGame(); });

        _goPanel.SetActive(false);
    }

    // ── Win overlay ───────────────────────────────────────────────
    void BuildWinPanel(RectTransform root)
    {
        _winPanel = FullOverlay(root, "Win",
            new Color(0.01f, 0.004f, 0.047f, 0.88f));
        var wrt = _winPanel.GetComponent<RectTransform>();

        var card = GlassPill(wrt, "Card",
            new Vector2(0.06f, 0.12f), new Vector2(0.94f, 0.88f),
            Vector2.zero, Vector2.zero);
        AddBorder(card, GlassBdrStr);

        // Amber glow band at top
        FillRect(MakeRect(card, "G",
            new Vector2(0f, 0.88f), new Vector2(1f, 1f),
            Vector2.zero, Vector2.zero),
            new Color(AccAmber.r, AccAmber.g, AccAmber.b, 0.14f));

        // "★ COSMOS COMPLETE ★"
        Label(card, "Sub", "★   COSMOS COMPLETE   ★", 10,
            TextAlignmentOptions.Center, AccAmber,
            new Vector2(0f, 0.85f), new Vector2(1f, 0.94f));

        // "VICTORY"
        Label(card, "Ti", "VICTORY", 54,
            TextAlignmentOptions.Center, new Color(1f, 0.93f, 0.55f, 1f),
            new Vector2(0f, 0.74f), new Vector2(1f, 0.87f));

        // Subtitle
        Label(card, "Su", "You forged two Saturns — the cosmos sings.", 11,
            TextAlignmentOptions.Center, Ink2,
            new Vector2(0.04f, 0.67f), new Vector2(0.96f, 0.75f));

        // Stats row
        var stats = GlassPill(card, "St",
            new Vector2(0.05f, 0.48f), new Vector2(0.95f, 0.65f),
            Vector2.zero, Vector2.zero);
        AddBorder(stats, GlassBdr);

        // Score
        Label(stats, "SLb", "SCORE", 9, TextAlignmentOptions.Center, Ink2,
            new Vector2(0f, 0.52f), new Vector2(0.34f, 1f));
        _winScoreTxt = Label(stats, "SV", "0", 22, TextAlignmentOptions.Center, AccAmber,
            new Vector2(0f, 0.02f), new Vector2(0.34f, 0.55f));

        FillRect(MakeRect(stats, "D1",
            new Vector2(0.36f, 0.10f), new Vector2(0.362f, 0.90f),
            Vector2.zero, Vector2.zero), Ink4);

        // Bonus
        Label(stats, "BLb", "BONUS", 9, TextAlignmentOptions.Center, Ink2,
            new Vector2(0.38f, 0.52f), new Vector2(0.65f, 1f));
        Label(stats, "BV", "+1,000", 16, TextAlignmentOptions.Center, AccCyan,
            new Vector2(0.38f, 0.02f), new Vector2(0.65f, 0.55f));

        FillRect(MakeRect(stats, "D2",
            new Vector2(0.67f, 0.10f), new Vector2(0.672f, 0.90f),
            Vector2.zero, Vector2.zero), Ink4);

        // Rank
        Label(stats, "RLb", "RANK", 9, TextAlignmentOptions.Center, Ink2,
            new Vector2(0.69f, 0.52f), new Vector2(1f, 1f));
        Label(stats, "RV", "COSMIC", 13, TextAlignmentOptions.Center, AccViolet,
            new Vector2(0.69f, 0.02f), new Vector2(1f, 0.55f));

        // Buttons
        AccentButton(card, "Share", "✦  SHARE",
            new Vector2(0.04f, 0.05f), new Vector2(0.46f, 0.19f), Ink3, () => { });
        AccentButton(card, "Again", "↺  PLAY AGAIN",
            new Vector2(0.54f, 0.05f), new Vector2(0.96f, 0.19f), AccCyan,
            () => { HideOverlays(); GameManager.Instance?.RestartGame(); });

        _winPanel.SetActive(false);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Toggle Handlers

    void ToggleMusic()
    {
        if (AudioManager.Instance == null) return;
        AudioManager.Instance.SetMusic(!AudioManager.Instance.MusicEnabled);
        bool on = AudioManager.Instance.MusicEnabled;
        if (_musicToggleTxt) { _musicToggleTxt.text = on ? "ON" : "OFF"; _musicToggleTxt.color = on ? Color.black : Ink2; }
        if (_musicToggleImg) _musicToggleImg.color = on ? new Color(AccCyan.r, AccCyan.g, AccCyan.b, 0.55f) : GlassBg;
    }

    void ToggleSound()
    {
        if (AudioManager.Instance == null) return;
        AudioManager.Instance.SetSound(!AudioManager.Instance.SoundEnabled);
        bool on = AudioManager.Instance.SoundEnabled;
        if (_soundToggleTxt) { _soundToggleTxt.text = on ? "ON" : "OFF"; _soundToggleTxt.color = on ? Color.black : Ink2; }
        if (_soundToggleImg) _soundToggleImg.color = on ? new Color(AccCyan.r, AccCyan.g, AccCyan.b, 0.55f) : GlassBg;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Widget Helpers

    /// <summary>Glass rounded-rect panel.</summary>
    RectTransform GlassPill(RectTransform parent, string name,
        Vector2 ancMin, Vector2 ancMax, Vector2 offMin, Vector2 offMax)
    {
        var rt  = MakeRect(parent, name, ancMin, ancMax, offMin, offMax);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite        = RoundedSprite(22);
        img.type          = Image.Type.Sliced;
        img.color         = GlassBg;
        img.raycastTarget = false;
        return rt;
    }

    /// <summary>Adds a 1px glass border Image as a child.</summary>
    void AddBorder(RectTransform rt, Color col)
    {
        var b   = MakeRect(rt, "Bdr", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var img = b.gameObject.AddComponent<Image>();
        img.sprite        = RoundedSprite(22);
        img.type          = Image.Type.Sliced;
        img.color         = col;
        img.raycastTarget = false;
    }

    /// <summary>Icon + label + hint row in the settings panel (no toggle button — added separately).</summary>
    void BuildSettingsRow(RectTransform card, string name,
        float y0, float y1, string icon, string label, string hint)
    {
        var row = MakeRect(card, name,
            new Vector2(0f, y0), new Vector2(0.64f, y1),
            new Vector2(16f, 2f), new Vector2(-4f, -2f));

        var ib  = MakeRect(row, "IB",
            new Vector2(0f, 0.1f), new Vector2(0f, 0.9f),
            Vector2.zero, new Vector2(32f, 0f));
        var ibi = ib.gameObject.AddComponent<Image>();
        ibi.sprite        = RoundedSprite(8);
        ibi.type          = Image.Type.Sliced;
        ibi.color         = GlassBg;
        ibi.raycastTarget = false;
        Label(ib, "I", icon, 15, TextAlignmentOptions.Center, AccCyan);

        Label(row, "L", label, 13, TextAlignmentOptions.Left, Ink0,
            new Vector2(0f, 0.47f), new Vector2(1f, 1f),
            new Vector2(40f, 0f), Vector2.zero);
        Label(row, "H", hint, 10, TextAlignmentOptions.Left, Ink3,
            new Vector2(0f, 0f), new Vector2(1f, 0.49f),
            new Vector2(40f, 0f), Vector2.zero);
    }

    /// <summary>Rounded button with accent colour tint + border glow.</summary>
    void AccentButton(RectTransform parent, string name, string text,
        Vector2 ancMin, Vector2 ancMax, Color accent,
        UnityEngine.Events.UnityAction onClick)
    {
        var rt  = MakeRect(parent, name, ancMin, ancMax, Vector2.zero, Vector2.zero);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite        = RoundedSprite(18);
        img.type          = Image.Type.Sliced;
        img.color         = new Color(accent.r * 0.18f, accent.g * 0.18f, accent.b * 0.18f, 0.92f);
        img.raycastTarget = true;

        // Accent-tinted border overlay
        var brd = MakeRect(rt, "Brd", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var bi  = brd.gameObject.AddComponent<Image>();
        bi.sprite        = RoundedSprite(18);
        bi.type          = Image.Type.Sliced;
        bi.color         = new Color(accent.r, accent.g, accent.b, 0.38f);
        bi.raycastTarget = false;

        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var cs = btn.colors;
        cs.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        cs.pressedColor     = new Color(0.78f, 0.78f, 0.78f, 1f);
        btn.colors = cs;
        btn.onClick.AddListener(onClick);

        Label(rt, "L", $"<b>{text}</b>", 13, TextAlignmentOptions.Center, Ink0);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Base Helpers

    RectTransform MakeRect(RectTransform parent, string name,
        Vector2 ancMin, Vector2 ancMax, Vector2 offMin, Vector2 offMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = ancMin;
        rt.anchorMax = ancMax;
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
        return rt;
    }

    Image FillRect(RectTransform rt, Color c)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.color         = c;
        img.raycastTarget = false;
        return img;
    }

    TMP_Text Label(RectTransform parent, string name, string text,
        float size, TextAlignmentOptions align,
        Color? col    = null,
        Vector2? ancMin = null, Vector2? ancMax = null,
        Vector2? offMin = null, Vector2? offMax = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = ancMin ?? Vector2.zero;
        rt.anchorMax = ancMax ?? Vector2.one;
        rt.offsetMin = offMin ?? Vector2.zero;
        rt.offsetMax = offMax ?? Vector2.zero;

        var t              = go.AddComponent<TextMeshProUGUI>();
        t.text             = text;
        t.fontSize         = size;
        t.color            = col ?? Ink0;
        t.alignment        = align;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode     = TextOverflowModes.Overflow;
        t.raycastTarget    = false;
        return t;
    }

    GameObject FullOverlay(RectTransform root, string name, Color bg)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(root, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        go.AddComponent<Image>().color = bg;
        return go;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Procedural Sprites

    Sprite RoundedSprite(int cornerPx)
    {
        if (_roundedCache.TryGetValue(cornerPx, out var cached)) return cached;

        int size = cornerPx * 3;
        var tex  = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float cx = (x < cornerPx) ? cornerPx : (x >= size - cornerPx) ? size - cornerPx : x;
            float cy = (y < cornerPx) ? cornerPx : (y >= size - cornerPx) ? size - cornerPx : y;
            float dx = x - cx, dy = y - cy;
            float d  = Mathf.Sqrt(dx * dx + dy * dy);
            float a  = Mathf.Clamp01(cornerPx - d + 0.5f);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();

        float b = cornerPx;
        var sp  = Sprite.Create(tex, new Rect(0, 0, size, size),
                      new Vector2(0.5f, 0.5f), size, 0,
                      SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        _roundedCache[cornerPx] = sp;
        return sp;
    }

    static Sprite _circleSprite;
    static Sprite CircleSprite()
    {
        if (_circleSprite) return _circleSprite;
        int res = 64; float c = res * 0.5f, r = c - 1f;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float a = Mathf.Clamp01(r - Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) + 1f);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        _circleSprite = Sprite.Create(tex, new Rect(0, 0, res, res), Vector2.one * 0.5f, res);
        return _circleSprite;
    }

    #endregion
}
