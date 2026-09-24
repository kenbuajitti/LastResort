using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Word selection, ladder history, and elapsed timer. Keep class and serialized field names
// so the existing game scene needs no Inspector rewiring.
public class TspGameController : MonoBehaviour
{
    [SerializeField] TspPuzzleLoader puzzleLoader;
    [SerializeField] TspPuzzleRenderer puzzleRenderer;
    [SerializeField] TspRouteLine routeLine, optimalRouteLine;
    [SerializeField] Button startButton, undoButton, submitButton, mainMenuButton;
    [SerializeField] TMP_Text statusText, timerText;
    [SerializeField] TMP_Dropdown nodeCountDropdown;
    TMP_Dropdown lettersDropdown;
    [SerializeField] GameObject resultPanel, routeNavigationPanel;

    readonly List<WordPuzzle> puzzles = new();
    readonly List<WordPuzzle> matching = new();
    static readonly string[] levels = { "Beginner", "Intermediate", "Advanced", "Expert" };
    readonly List<RectTransform> tiles = new();
    readonly List<string> ladder = new();
    readonly HashSet<string> candidates = new();
    RectTransform ladderViewport;
    TMP_Text ladderText;
    ScrollRect ladderScroll;
    Button restart;
    RectTransform board, content, viewport;
    TMP_Text heading, currentLabel, candidateLabel, counter;
    Button previous, next, doneButton;
    Toggle filterButton;
    Image puzzleFilterTrack, puzzleFilterKnob;
    Sprite puzzleSwitchSprite;
    Texture2D puzzleSwitchTexture;
    bool onlyNotDone = true;
    string packId = "wordiq";
    // Session-only, like RouteIQ: survives menu changes, never writes to disk.
    static readonly HashSet<string> donePuzzles = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetDoneSession() { donePuzzles.Clear(); }

    string DoneKey(WordPuzzle p)
    {
        // Include endpoints so regenerated packs that reuse IDs do not collide.
        return packId + "|" + p.wordLength + "|" + p.id + "|" + p.startWord + "|" + p.targetWord;
    }
    void ToggleDone()
    {
        if (timerRunning || matching.Count == 0) return;
        string key = DoneKey(matching[index]);
        if (!donePuzzles.Add(key)) donePuzzles.Remove(key);
        if (onlyNotDone)
        {
            RebuildMatching();
            ShowPuzzle();
        }
        else UpdateDoneButton();
    }
    void ToggleFilter(bool allPuzzles)
    {
        if (timerRunning)
        {
            filterButton.SetIsOnWithoutNotify(!onlyNotDone);
            UpdatePuzzleSwitchAppearance(!onlyNotDone);
            return;
        }
        WordPuzzle current = matching.Count > 0 ? matching[index] : null;
        onlyNotDone = !allPuzzles;
        RebuildMatching();
        int retained = current == null ? -1 : matching.IndexOf(current);
        if (retained >= 0)
        {
            index = retained;
            RefreshWords();
        }
        else ShowPuzzle();
    }
    void RebuildMatching()
    {
        matching.Clear();
        int option = nodeCountDropdown.value;
        if (option >= 0 && option < levels.Length)
            matching.AddRange(puzzles.FindAll(p => p.difficulty == levels[option] && p.wordLength == lettersDropdown.value + 3
                && (!onlyNotDone || !donePuzzles.Contains(DoneKey(p)))));
        // Removing the current puzzle leaves the next one at the same index.
        if (index >= matching.Count) index = 0;
    }
    void UpdateDoneButton()
    {
        if (doneButton == null) return;
        bool hasPuzzle = matching.Count > 0;
        bool done = hasPuzzle && donePuzzles.Contains(DoneKey(matching[index]));
        doneButton.GetComponentInChildren<TMP_Text>(true).text = done ? "[X] DONE" : "[ ] DONE";
        doneButton.interactable = hasPuzzle && !timerRunning;
    }
    ScrollRect scroll;
    Vector2 lastBoardSize;
    int index;
    bool started, timerRunning;
    double elapsedSeconds, timerStartedAt;

    double ElapsedSeconds => elapsedSeconds + (timerRunning
        ? Time.realtimeSinceStartupAsDouble - timerStartedAt : 0d);

    void Update() { if (timerRunning) UpdateTimerDisplay(); }

    void UpdateTimerDisplay()
    {
        var elapsed = TimeSpan.FromSeconds(ElapsedSeconds);
        timerText.text = $"TIME: {(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}.{elapsed.Milliseconds / 100}";
    }

    void StartPuzzle()
    {
        if (started || matching.Count == 0) return;
        started = true;
        ResumeTimer();
        RefreshWords();
    }

    void ResumeTimer()
    {
        if (timerRunning) return;
        timerStartedAt = Time.realtimeSinceStartupAsDouble;
        timerRunning = true;
    }

    void StopTimer()
    {
        elapsedSeconds = ElapsedSeconds;
        timerRunning = false;
        UpdateTimerDisplay();
    }

    void Awake()
    {
        // Both legacy components initialize in Start; disable before that runs.
        puzzleLoader.enabled = false;
        puzzleRenderer.enabled = false;
        routeLine.gameObject.SetActive(false);
        optimalRouteLine.gameObject.SetActive(false);
        resultPanel.SetActive(false);
        routeNavigationPanel.SetActive(false);
        var canvas = startButton.GetComponentInParent<Canvas>();
        board = canvas.transform.Find("PuzzleArea") as RectTransform;
        mainMenuButton.transform.SetParent(startButton.transform.parent, false);
        mainMenuButton.gameObject.SetActive(true);
        mainMenuButton.onClick = new Button.ButtonClickedEvent();
        mainMenuButton.onClick.AddListener(ReturnToMenu);
        previous = MakeButton("BrowsePreviousButton", "PREV", () => Browse(-1));
        next = MakeButton("BrowseNextButton", "NEXT", () => Browse(1));
        counter = MakeText("PuzzleCounterText", startButton.transform.parent, 24);
        doneButton = MakeButton("WordPuzzleDoneButton", "[ ] DONE", ToggleDone);
        doneButton.interactable = false;
        filterButton = CreatePuzzleFilterToggle();
        filterButton.onValueChanged.AddListener(ToggleFilter);
        startButton.gameObject.SetActive(false);
        undoButton.gameObject.SetActive(false);
        submitButton.gameObject.SetActive(false);
        timerText.transform.SetParent(board, false);
        timerText.gameObject.SetActive(true);
        timerText.color = new Color(.08f, .13f, .22f);
        timerText.alignment = TextAlignmentOptions.Center;
        timerText.enableAutoSizing = true;
        timerText.fontSizeMin = 14;
        timerText.fontSizeMax = 26;
        timerText.raycastTarget = false;
        UpdateTimerDisplay();
        // Clone the scene dropdown so its font, template and pointer behavior match.
        lettersDropdown = Instantiate(nodeCountDropdown, nodeCountDropdown.transform.parent);
        lettersDropdown.name = "LettersDropdown";
        var lettersHeading = lettersDropdown.transform.Find("NodesHeading");
        lettersHeading.name = "LettersHeading";
        lettersHeading.GetComponent<TMP_Text>().text = "LETTERS";
        lettersDropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
        lettersDropdown.ClearOptions();
        lettersDropdown.AddOptions(new List<string> { "3", "4" });
        lettersDropdown.SetValueWithoutNotify(0);
        lettersDropdown.RefreshShownValue();
        lettersDropdown.onValueChanged.AddListener(SelectDifficulty);
        nodeCountDropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
        nodeCountDropdown.onValueChanged.AddListener(SelectDifficulty);
        var label = nodeCountDropdown.transform.Find("NodesHeading").GetComponent<TMP_Text>();
        label.text = "LEVEL";
        canvas.transform.Find("TitleText").GetComponent<TMP_Text>().text = "WORD IQ";
        statusText.text = "Exactly one path reaches the target. Letters may be rearranged.";
        board.GetComponent<Image>().color = new Color(.94f, .96f, .98f);
        heading = MakeText("WordPuzzleHeading", board, 34);
        currentLabel = MakeText("CurrentWord", board, 28);
        currentLabel.color = new Color(.76f, .12f, .19f);
        candidateLabel = MakeText("CandidatesHeading", board, 20);
        candidateLabel.text = "NEXT WORDS";
        viewport = MakeRect("WordViewport", board);
        viewport.gameObject.AddComponent<RectMask2D>();
        var hitArea = viewport.gameObject.AddComponent<Image>();
        hitArea.color = new Color(0, 0, 0, 0);
        scroll = viewport.gameObject.AddComponent<ScrollRect>();
        content = MakeRect("WordCandidates", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(.5f, 1);
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        ladderViewport = MakeRect("LadderViewport", board);
        ladderViewport.gameObject.AddComponent<RectMask2D>();
        ladderViewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
        ladderScroll = ladderViewport.gameObject.AddComponent<ScrollRect>();
        ladderText = MakeText("WordLadder", ladderViewport, 26);
        ladderText.enableAutoSizing = false;
        ladderText.textWrappingMode = TextWrappingModes.NoWrap;
        ladderText.alignment = TextAlignmentOptions.MidlineLeft;
        ladderScroll.viewport = ladderViewport;
        ladderScroll.content = ladderText.rectTransform;
        ladderScroll.horizontal = true;
        ladderScroll.vertical = false;
        ladderScroll.movementType = ScrollRect.MovementType.Clamped;
        undoButton.transform.SetParent(board, false);
        undoButton.onClick = new Button.ButtonClickedEvent();
        undoButton.onClick.AddListener(UndoWord);
        undoButton.gameObject.SetActive(true);
        undoButton.interactable = false;
        restart = MakeButton("RestartWordButton", "RESTART", ShowPuzzle);
        restart.transform.SetParent(board, false);
        restart.interactable = false;
        startButton.transform.SetParent(board, false);
        startButton.onClick = new Button.ButtonClickedEvent();
        startButton.onClick.AddListener(StartPuzzle);
        startButton.gameObject.SetActive(true);
        startButton.interactable = false;
        canvas.GetComponent<TspResponsiveLayout>().RegisterWordBrowser(previous, next, counter, doneButton, filterButton, lettersDropdown);
    }

    void Start()
    {
        try
        {
            var asset = Resources.Load<TextAsset>("wordiq-puzzles");
            if (asset == null) throw new Exception("Missing Assets/Resources/wordiq-puzzles.json");
            var pack = JsonUtility.FromJson<WordPack>(asset.text);
            if (pack == null || pack.schemaVersion != 2 || pack.puzzles == null)
                throw new Exception("Expected WordIQ schemaVersion 2 with a puzzles array.");
            packId = string.IsNullOrEmpty(pack.packId) ? "wordiq" : pack.packId;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in pack.puzzles)
            {
                if (!WordIQPuzzleValidation.Valid(p) || !ids.Add(p.id)) { Debug.LogWarning("Skipping invalid WordIQ puzzle: " + p?.id); continue; }
                puzzles.Add(p);
            }
            if (puzzles.Count == 0) throw new Exception("No valid schema 2 puzzles found.");
            nodeCountDropdown.ClearOptions();
            nodeCountDropdown.AddOptions(new List<string> { "Beginner - 3", "Intermediate - 4", "Advanced - 5", "Expert - 6" });
            nodeCountDropdown.SetValueWithoutNotify(0);
            nodeCountDropdown.RefreshShownValue();
            lettersDropdown.interactable = nodeCountDropdown.interactable = puzzles.Count > 0;
            SelectDifficulty(0);
        }
        catch (Exception e)
        {
            Debug.LogError("WordIQ: " + e.Message);
            heading.text = "No puzzle available";
            statusText.text = "The word puzzle file could not be loaded.";
            lettersDropdown.interactable = nodeCountDropdown.interactable = previous.interactable = next.interactable = false;
        }
    }

    void SelectDifficulty(int option)
    {
        index = 0;
        RebuildMatching();
        ShowPuzzle();
    }
    void Browse(int direction)
    {
        if (matching.Count == 0) return;
        index = (index + direction + matching.Count) % matching.Count;
        ShowPuzzle();
    }
    void ShowPuzzle()
    {
        timerRunning = false;
        started = false;
        elapsedSeconds = 0d;
        UpdateTimerDisplay();
        ladder.Clear();
        if (matching.Count > 0) ladder.Add(matching[index].startWord);
        RefreshWords();
    }
    // Turn count is the number of edges: the initial word is turn zero.
    static int TurnLimit(WordPuzzle p) => p.rules != null && p.rules.maxTurns > 0
        ? Math.Min(6, p.rules.maxTurns) : 6;

    bool WithinTurnLimit(WordPuzzle p, string word)
    {
        int turns = ladder.Count - 1;
        return turns < TurnLimit(p) && (turns < TurnLimit(p) - 1 || word == p.targetWord);
    }

    void SelectWord(string word)
    {
        if (!started || !timerRunning || matching.Count == 0 || ladder.Count == 0 ||
            ladder[ladder.Count - 1] == matching[index].targetWord || !candidates.Contains(word)
            || !WithinTurnLimit(matching[index], word)) return;
        ladder.Add(word);
        if (word == matching[index].targetWord) StopTimer();
        RefreshWords();
    }
    void UndoWord()
    {
        if (!started || ladder.Count <= 1) return;
        ResumeTimer();
        ladder.RemoveAt(ladder.Count - 1);
        RefreshWords();
    }
    void RefreshWords()
    {
        UpdateDoneButton();
        filterButton.SetIsOnWithoutNotify(!onlyNotDone);
        UpdatePuzzleSwitchAppearance(!onlyNotDone);
        filterButton.interactable = !timerRunning;
        foreach (var tile in tiles) { tile.gameObject.SetActive(false); Destroy(tile.gameObject); }
        tiles.Clear();
        candidates.Clear();
        undoButton.interactable = ladder.Count > 1;
        restart.interactable = started;
        startButton.interactable = !started && matching.Count > 0;
        ladderText.text = string.Join("  →  ", ladder);
        counter.text = matching.Count == 0 ? "0 of 0" : $"{index + 1} of {matching.Count}";
        previous.interactable = next.interactable = matching.Count > 1;
        currentLabel.text = "";
        if (matching.Count == 0)
        {
            heading.text = onlyNotDone ? "No unfinished puzzles" : "No puzzles available";
            statusText.text = onlyNotDone
                ? "Turn on All Puzzles to revisit or unmark completed puzzles."
                : "No puzzles are available for this Letters/Level combination.";
            candidateLabel.text = "";
            LayoutBoard();
            return;
        }
        var p = matching[index];
        heading.text = p.startWord + " → " + p.targetWord + "  <size=65%>(" + p.wordLength + " letters)</size>";
        string current = ladder[ladder.Count - 1];
        bool complete = started && current == p.targetWord;
        if (complete && timerRunning) StopTimer();
        int turns = ladder.Count - 1;
        currentLabel.text = (complete ? "TARGET REACHED: " : "CURRENT: ") + current
            + "   |   " + turns + "/" + TurnLimit(p) + " turns";
        var node = p.nodes.Find(n => n.word == current);
        if (started && !complete && node.nextWords != null) foreach (var word in node.nextWords)
        {
            if (!WithinTurnLimit(p, word) || word == current || ((p.rules == null || !p.rules.allowRepeatedWords) && ladder.Contains(word))
                || !candidates.Add(word)) continue;
            var tile = MakeRect("Candidate_" + word, content);
            var bg = tile.gameObject.AddComponent<Image>();
            bg.color = new Color(.08f, .39f, .44f);
            bg.raycastTarget = true;
            var button = tile.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(() => SelectWord(word));
            // A replaced tile must not remain selected for keyboard submit.
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            var text = MakeText("Word", tile, 28);
            text.text = word;
            text.color = Color.white;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(8, 4);
            text.rectTransform.offsetMax = new Vector2(-8, -4);
            tiles.Add(tile);
        }
        bool finalTurn = turns >= TurnLimit(p) - 1;
        candidateLabel.text = !started ? "SELECT START TO BEGIN" : complete ? "LADDER COMPLETE" : finalTurn ? (tiles.Count > 0 ? "FINAL TURN — SELECT THE TARGET" : "TURN LIMIT — UNDO OR RESTART") : tiles.Count > 0 ? "NEXT WORDS" : "NO NEXT WORDS — UNDO TO TRY ANOTHER PATH";
        statusText.text = !started ? $"{p.difficulty}: {p.solutionMoves} moves. Exactly one path to the target. Select START."
            : complete ? "Target reached! You found the only path. Restart to try for a faster time."
            : finalTurn ? (tiles.Count > 0 ? "Only the target can be selected on the final turn." : "The target is not available for the final turn. Select UNDO or RESTART.")
            : tiles.Count == 0 ? "No unused candidates remain. Select UNDO to go back."
            : "Exactly one path reaches the target. Wrong branches need UNDO. Drag the ladder to review.";
        LayoutBoard();
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1;
        Canvas.ForceUpdateCanvases();
        ladderScroll.StopMovement();
        ladderScroll.horizontalNormalizedPosition = 1;
    }
    void LateUpdate() { if (board.rect.size != lastBoardSize) LayoutBoard(); }
    void LayoutBoard()
    {
        lastBoardSize = board.rect.size;
        float w = board.rect.width, h = board.rect.height;
        Place(heading.rectTransform, 12, 12, w - 24, 48);
        Place(timerText.rectTransform, 12, 62, w - 24, 32);
        Place(ladderViewport, 16, 100, w - 32, 44);
        Place(ladderText.rectTransform, 0, 0, Mathf.Max(w - 32, ladderText.preferredWidth + 16), 44);
        Place(currentLabel.rectTransform, 12, 150, w - 24, 36);
        Place(candidateLabel.rectTransform, 12, 194, w - 24, 32);
        Place(viewport, 16, 236, w - 32, Mathf.Max(48, h - 308));
        float actionWidth = (w - 56) / 3;
        Place((RectTransform)startButton.transform, 16, h - 60, actionWidth, 44);
        Place((RectTransform)undoButton.transform, 28 + actionWidth, h - 60, actionWidth, 44);
        Place((RectTransform)restart.transform, 40 + 2 * actionWidth, h - 60, actionWidth, 44);
        float available = Mathf.Max(1, w - 32);
        int letters = matching.Count == 0 ? 3 : matching[index].wordLength;
        float desired = Mathf.Max(112, letters * 22 + 24);
        int columns = Mathf.Max(1, Mathf.FloorToInt((available + 12) / (desired + 12)));
        float tileWidth = (available - (columns - 1) * 12) / columns;
        int rows = Mathf.CeilToInt(tiles.Count / (float)columns);
        content.sizeDelta = new Vector2(0, Mathf.Max(viewport.rect.height, rows * 68 - 12));
        for (int i = 0; i < tiles.Count; i++)
            Place(tiles[i], (i % columns) * (tileWidth + 12), (i / columns) * 68, tileWidth, 56);
    }
    static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }
    static RectTransform MakeRect(string name, Transform parent)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }
    TMP_Text MakeText(string name, Transform parent, float size)
    {
        var text = MakeRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = statusText.font;
        text.color = new Color(.08f, .13f, .22f);
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = 14;
        text.fontSizeMax = size;
        text.raycastTarget = false;
        return text;
    }
    Button MakeButton(string name, string label, UnityEngine.Events.UnityAction action)
    {
        var button = Instantiate(startButton, startButton.transform.parent);
        button.name = name;
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(action);
        button.GetComponentInChildren<TMP_Text>(true).text = label;
        button.gameObject.SetActive(true);
        return button;
    }
    private Toggle CreatePuzzleFilterToggle()
    {
        var root = new GameObject("WordPuzzleFilterButton", typeof(RectTransform), typeof(Image), typeof(Toggle));
        root.transform.SetParent(startButton.transform.parent, false);
        var background = root.GetComponent<Image>();
        background.color = new Color(.95f, .96f, .98f);
        var toggle = root.GetComponent<Toggle>();
        toggle.targetGraphic = background;

        // Rounded artwork is generated locally; no imported sprites or scene wiring needed.
        const int size = 32;
        puzzleSwitchTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        puzzleSwitchTexture.name = "PuzzleSwitchCircle";
        puzzleSwitchTexture.filterMode = FilterMode.Bilinear;
        puzzleSwitchTexture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = new Vector2(x - 15.5f, y - 15.5f).magnitude;
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(16f - distance));
            }
        puzzleSwitchTexture.SetPixels(pixels);
        puzzleSwitchTexture.Apply(false, true);
        puzzleSwitchSprite = Sprite.Create(puzzleSwitchTexture,
            new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(14f, 14f, 14f, 14f));

        var trackObject = new GameObject("SwitchTrack", typeof(RectTransform), typeof(Image));
        puzzleFilterTrack = trackObject.GetComponent<Image>();
        puzzleFilterTrack.rectTransform.SetParent(root.transform, false);
        puzzleFilterTrack.rectTransform.anchorMin = puzzleFilterTrack.rectTransform.anchorMax = new Vector2(0f, .5f);
        puzzleFilterTrack.rectTransform.anchoredPosition = new Vector2(35f, 0f);
        puzzleFilterTrack.rectTransform.sizeDelta = new Vector2(56f, 28f);
        puzzleFilterTrack.sprite = puzzleSwitchSprite;
        puzzleFilterTrack.type = Image.Type.Sliced;
        puzzleFilterTrack.raycastTarget = false;

        var knobObject = new GameObject("SwitchKnob", typeof(RectTransform), typeof(Image));
        puzzleFilterKnob = knobObject.GetComponent<Image>();
        puzzleFilterKnob.rectTransform.SetParent(trackObject.transform, false);
        puzzleFilterKnob.rectTransform.sizeDelta = new Vector2(28f, 28f);
        puzzleFilterKnob.sprite = puzzleSwitchSprite;
        puzzleFilterKnob.raycastTarget = false;
        // The knob stays visible in both states, unlike a checkbox's graphic.
        toggle.graphic = null;
        toggle.SetIsOnWithoutNotify(!onlyNotDone);
        UpdatePuzzleSwitchAppearance(toggle.isOn);

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.rectTransform.SetParent(root.transform, false);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(69f, 2f);
        label.rectTransform.offsetMax = new Vector2(-5f, -2f);
        label.font = startButton.GetComponentInChildren<TMP_Text>(true).font;
        label.text = "All Puzzles";
        label.color = new Color(.12f, .14f, .18f);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableAutoSizing = true;
        label.fontSizeMin = 12f;
        label.fontSizeMax = 20f;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return toggle;
    }

    private void UpdatePuzzleSwitchAppearance(bool allPuzzles)
    {
        puzzleFilterTrack.color = new Color(.85f, .85f, .85f);
        puzzleFilterKnob.color = allPuzzles
            ? new Color(.22f, .79f, .35f)
            : new Color(.60f, .60f, .60f);
        puzzleFilterKnob.rectTransform.anchoredPosition =
            new Vector2(allPuzzles ? 14f : -14f, 0f);
    }

    void OnDestroy()
    {
        if (filterButton != null) filterButton.onValueChanged.RemoveListener(ToggleFilter);
        if (puzzleSwitchSprite != null) Destroy(puzzleSwitchSprite);
        if (puzzleSwitchTexture != null) Destroy(puzzleSwitchTexture);
    }
    static void ReturnToMenu() { SceneManager.LoadScene("TspMenuScene"); }
}
