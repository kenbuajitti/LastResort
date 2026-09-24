using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Attached to the canvas in both supplied scenes. Runs before puzzle placement.
[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(Canvas), typeof(CanvasScaler))]
public class TspResponsiveLayout : MonoBehaviour
{
    readonly Dictionary<string, RectTransform> items = new();
    CanvasScaler scaler;
    Canvas canvas;
    float appliedScale;
    Rect lastSafe, lastViewport;
    Vector2 lastCanvasSize;
    int lastWidth, lastHeight;
    bool game;
    float left, top;

    void Awake()
    {
        scaler = GetComponent<CanvasScaler>();
        canvas = GetComponent<Canvas>();
        foreach (RectTransform rt in GetComponentsInChildren<RectTransform>(true))
        {
            if (!items.ContainsKey(rt.name)) items.Add(rt.name, rt);
        }
        game = items.ContainsKey("PuzzleArea");
        // The menu contains an unused duplicate inside the instruction panel.
        if (!game)
        {
            items["HowToPlayButton"] = transform.Find("HowToPlayButton") as RectTransform;
            var duplicate = transform.Find("HowToPlayPanel/HowToPlayButton");
            if (duplicate != null) duplicate.gameObject.SetActive(false);
        }
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            // Labels do not receive pointer events, so the button's own graphic must.
            // Some scene buttons previously relied on their labels as click targets.
            if (button.targetGraphic != null)
                button.targetGraphic.raycastTarget = true;

            foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
            {
                var rt = label.rectTransform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(6, 3); rt.offsetMax = new Vector2(-6, -3);
                label.enableAutoSizing = true; label.fontSizeMin = 18; label.fontSizeMax = 24;
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;
            }
        }
        Apply();
    }
    public void RegisterPuzzleBrowser(Toggle filter, Button done, Button previous, Button next, TMP_Text counter)
    {
        foreach (var component in new Component[] { filter, done, previous, next, counter })
            items[component.name] = component.GetComponent<RectTransform>();
        Apply();
    }

    void LateUpdate()
    {
        if (Screen.width != lastWidth || Screen.height != lastHeight || Screen.safeArea != lastSafe
            || canvas.pixelRect != lastViewport
            || ((RectTransform)transform).rect.size != lastCanvasSize
            || !Mathf.Approximately(canvas.scaleFactor, appliedScale)) Apply();
    }
    void Box(string name, float x, float y, float w, float h, bool root = true)
    {
        if (!items.TryGetValue(name, out var rt) || rt == null) return;
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x + w / 2 + (root ? left : 0), -y - h / 2 - (root ? top : 0));
    }
    void TextStyle(string name, float max, float min = 22)
    {
        if (!items.TryGetValue(name, out var rt)) return;
        var text = rt.GetComponent<TMP_Text>();
        if (text == null) return;
        text.enableAutoSizing = true; text.fontSizeMin = min; text.fontSizeMax = max;
        text.margin = new Vector4(6, 3, 6, 3);
        text.raycastTarget = false;
    }
    void Apply()
    {
        lastWidth = Screen.width; lastHeight = Screen.height; lastSafe = Screen.safeArea;
        if (lastWidth <= 0 || lastHeight <= 0) return;
        // A camera viewport can be smaller than Screen. Use the actual canvas
        // viewport and intersect it with the device safe area before laying out.
        Canvas.ForceUpdateCanvases();
        Rect viewport = canvas.pixelRect;
        if (viewport.width <= 0 || viewport.height <= 0) return;
        Rect safe = lastSafe.width > 0 && lastSafe.height > 0 ? lastSafe : viewport;
        float xMin = Mathf.Max(viewport.xMin, safe.xMin);
        float yMin = Mathf.Max(viewport.yMin, safe.yMin);
        float xMax = Mathf.Min(viewport.xMax, safe.xMax);
        float yMax = Mathf.Min(viewport.yMax, safe.yMax);
        safe = xMax > xMin && yMax > yMin
            ? Rect.MinMaxRect(xMin, yMin, xMax, yMax) : viewport;
        bool landscape = safe.width / safe.height >= 1.25f;
        float scale = landscape
            ? Mathf.Min(safe.width / 800f, safe.height / 600f)
            : Mathf.Min(safe.width / 600f, safe.height / 930f);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = scale;
        canvas.scaleFactor = scale;
        appliedScale = scale;
        Canvas.ForceUpdateCanvases();
        // Read actual local dimensions; do not assume Screen / scale matches
        // this canvas. Recheck in LateUpdate after CanvasScaler has updated.
        Vector2 size = ((RectTransform)transform).rect.size;
        if (size.x <= 0 || size.y <= 0) return;
        float unitsX = size.x / viewport.width;
        float unitsY = size.y / viewport.height;
        left = (safe.xMin - viewport.xMin) * unitsX;
        top = (viewport.yMax - safe.yMax) * unitsY;
        float w = safe.width * unitsX, h = safe.height * unitsY;
        lastViewport = canvas.pixelRect;
        lastCanvasSize = size;
        if (game) GameLayout(w, h); else MenuLayout(w, h);
        Canvas.ForceUpdateCanvases();
    }
    public void RegisterWordBrowser(Button previous, Button next, TMP_Text counter, Button done = null, Toggle filter = null, TMP_Dropdown letters = null)
    {
        foreach (var component in new Component[] { previous, next, counter })
            items[component.name] = component.GetComponent<RectTransform>();
        if (done != null) items[done.name] = done.GetComponent<RectTransform>();
        if (filter != null) items[filter.name] = filter.GetComponent<RectTransform>();
        if (letters != null)
        {
            items[letters.name] = letters.GetComponent<RectTransform>();
            items["LettersHeading"] = letters.transform.Find("LettersHeading") as RectTransform;
        }
        Apply();
    }
    void GameLayout(float w, float h)
    {
        bool wide = w / h >= 1.25f;
        bool hasDone = items.ContainsKey("WordPuzzleDoneButton");
        float filterRow = items.ContainsKey("LettersDropdown") ? 48f : 0f;
        float boardWidth = wide ? Mathf.Max(340, w * .58f) : w - 32;
        float boardHeight = wide ? h - 32 : h - (hasDone ? 368 : 340) - filterRow;
        float bx = 16, by = wide ? 16 : 108 + filterRow;
        Box("PuzzleArea", bx, by, boardWidth, boardHeight);
        float x = wide ? boardWidth + 36 : 16;
        float width = wide ? w - x - 16 : w - 32;
        Box("TitleText", x, 10, width, 42);
        Box("NodeCountDropdown", x + 110, 60 + filterRow, width - 110, 36);
        Box("NodesHeading", -110, 0, 102, 36, false);
        Box("LettersDropdown", x + 110, 60, width - 110, 36);
        Box("LettersHeading", -110, 0, 102, 36, false);
        float controlsY = wide ? 130 + filterRow : by + boardHeight + 16;
        float third = (width - 16) / 3;
        Box("BrowsePreviousButton", x, controlsY, third, 44);
        Box("PuzzleCounterText", x + third + 8, controlsY, third, 44);
        Box("BrowseNextButton", x + 2 * (third + 8), controlsY, third, 44);
        if (hasDone)
        {
            bool hasFilter = items.ContainsKey("WordPuzzleFilterButton");
            float doneWidth = hasFilter ? (width - 8) * .40f : Mathf.Min(180, width);
            Box("WordPuzzleDoneButton", hasFilter ? x + width - doneWidth : x + (width - doneWidth) / 2,
                controlsY + 50, doneWidth, 38);
            if (hasFilter)
                Box("WordPuzzleFilterButton", x, controlsY + 50,
                    width - doneWidth - 8, 38);
        }
        Box("StatusText", x, controlsY + (hasDone ? 94 : 56), width,
            hasDone ? (wide ? 70 : 66) : (wide ? 100 : 72));
        Box("MainMenuButton", x, controlsY + (wide ? 176 : hasDone ? 166 : 140), width, 48);
        TextStyle("TitleText", 32, 20);
        // Both filter labels use one fixed size; independent autosizing made
        // the longer LETTERS label smaller than LEVEL.
        foreach (string labelName in new[] { "NodesHeading", "LettersHeading" })
        {
            TextStyle(labelName, 20, 20);
            if (!items.TryGetValue(labelName, out var headingRect)) continue;
            var heading = headingRect.GetComponent<TMP_Text>();
            if (heading == null) continue;
            heading.enableAutoSizing = false;
            heading.fontSize = 20;
            heading.enableWordWrapping = false;
        }
        TextStyle("PuzzleCounterText", 24, 16);
        TextStyle("StatusText", 23, 16);
        var dropdown = items["NodeCountDropdown"].GetComponent<TMP_Dropdown>();
        dropdown.captionText.enableAutoSizing = true;
        dropdown.captionText.fontSizeMin = 16;
        dropdown.captionText.fontSizeMax = 24;
        dropdown.captionText.enableWordWrapping = false;
        dropdown.itemText.enableAutoSizing = true;
        dropdown.itemText.fontSizeMin = 14;
        dropdown.itemText.fontSizeMax = 24;
        dropdown.itemText.enableWordWrapping = false;
        if (items.TryGetValue("LettersDropdown", out var lettersRect))
        {
            var letters = lettersRect.GetComponent<TMP_Dropdown>();
            letters.captionText.enableAutoSizing = true;
            letters.captionText.fontSizeMin = 16;
            letters.captionText.fontSizeMax = 24;
            letters.captionText.enableWordWrapping = false;
            letters.template.sizeDelta = new Vector2(letters.template.sizeDelta.x, 100);
            items["LettersHeading"].GetComponent<TMP_Text>().enableWordWrapping = false;
        }
        items["Template"].sizeDelta = new Vector2(items["Template"].sizeDelta.x, Mathf.Min(300, h - 116));
    }
    void MenuLayout(float w, float h)
    {
        float width = Mathf.Min(w - 40, 760), x = (w - width) / 2;
        Box("GameTitleText", x, h * .15f, width, 80);
        Box("SubtitleText", x, h * .15f + 92, width, 80);
        Box("PlayGameButton", w / 2 - 130, h * .58f, 260, 56);
        Box("HowToPlayButton", w / 2 - 130, h * .58f + 72, 260, 56);
        Box("HowToPlayPanel", x, 20, width, h - 40);
        Box("HowtoPlayTitleText", 16, 16, width - 32, 48, false);
        Box("HowToPlayText", 24, 76, width - 48, h - 216, false);
        Box("CloseHowToPlayButton", width / 2 - 110, h - 112, 220, 52, false);
        TextStyle("GameTitleText", 56, 32); TextStyle("SubtitleText", 30);
        TextStyle("HowtoPlayTitleText", 32); TextStyle("HowToPlayText", 26, 20);
        if (items.TryGetValue("HowToPlayText", out var helpRect))
        {
            var help = helpRect.GetComponent<TMP_Text>();
            if (help != null)
            {
                help.alignment = TextAlignmentOptions.TopLeft;
                help.fontStyle = FontStyles.Normal;
                help.lineSpacing = 0;
                help.textWrappingMode = TextWrappingModes.Normal;
            }
        }
    }
}
