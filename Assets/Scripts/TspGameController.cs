using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Number selection, ladder history, and elapsed timer. Keep class and serialized field names
// so the existing game scene needs no Inspector rewiring.
public class TspGameController : MonoBehaviour
{
    [SerializeField] TspPuzzleLoader puzzleLoader;
    [SerializeField] TspPuzzleRenderer puzzleRenderer;
    [SerializeField] TspRouteLine routeLine, optimalRouteLine;
    [SerializeField] Button startButton, undoButton, submitButton, mainMenuButton;
    [SerializeField] TMP_Text statusText, timerText;
    [SerializeField] TMP_Dropdown nodeCountDropdown;
    [SerializeField] GameObject resultPanel, routeNavigationPanel;

    readonly List<NumberIQPuzzle> puzzles = new();
    readonly List<NumberIQPuzzle> matching = new();
    readonly List<int> difficultyRanks = new();
    readonly List<RectTransform> tiles = new();
    readonly List<int> ladder = new();
    readonly HashSet<int> candidates = new();
    RectTransform ladderViewport;
    TMP_Text ladderText;
    ScrollRect ladderScroll;
    Button restart, revealPathButton, closePathButton;
    RectTransform revealPanel, revealViewport;
    TMP_Text revealHeading, revealText;
    ScrollRect revealScroll;
    Graphic revealBackground;
    // Optional override; otherwise reuse the existing scene's board/game background.
    [SerializeField] Graphic revealBackgroundSource;
    bool PathVisible => revealPanel != null && revealPanel.gameObject.activeSelf;
    RectTransform board, content, viewport;
    TMP_Text heading, currentLabel, candidateLabel, counter, operationsLabel, solutionText;
    Button previous, next, doneButton;
    Toggle filterButton;
    Image puzzleFilterTrack, puzzleFilterKnob;
    Sprite puzzleSwitchSprite;
    Texture2D puzzleSwitchTexture;
    bool onlyNotDone = true;
    string packId = "numberiq";
    // Session-only, like RouteIQ: survives menu changes, never writes to disk.
    static readonly HashSet<string> donePuzzles = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetDoneSession() { donePuzzles.Clear(); }

    string DoneKey(NumberIQPuzzle p)
    {
        // Include endpoints so regenerated packs that reuse IDs do not collide.
        return packId + "|" + p.difficultyRank + "|" + p.id + "|" + p.startNumber + "|" + p.targetNumber;
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
        NumberIQPuzzle current = matching.Count > 0 ? matching[index] : null;
        onlyNotDone = !allPuzzles;
        RebuildMatching();
        int retained = current == null ? -1 : matching.IndexOf(current);
        if (retained >= 0)
        {
            index = retained;
            RefreshNumbers();
        }
        else ShowPuzzle();
    }
    void RebuildMatching()
    {
        matching.Clear();
        int option = nodeCountDropdown.value;
        if (option >= 0 && option < difficultyRanks.Count)
            matching.AddRange(puzzles.FindAll(p => p.difficultyRank == difficultyRanks[option]
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
    bool started, timerRunning, loading = true;
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
        if (PathVisible || started || matching.Count == 0) return;
        started = true;
        ResumeTimer();
        RefreshNumbers();
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
        nodeCountDropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
        nodeCountDropdown.onValueChanged.AddListener(SelectDifficulty);
        var label = nodeCountDropdown.transform.Find("NodesHeading").GetComponent<TMP_Text>();
        label.text = "DIFFICULTY";
        canvas.transform.Find("TitleText").GetComponent<TMP_Text>().text = "NUMBER IQ";
        statusText.text = "Loading number puzzles...";
        nodeCountDropdown.interactable = false;
        previous.interactable = next.interactable = filterButton.interactable = false;
        board.GetComponent<Image>().color = new Color(.94f, .96f, .98f);
        heading = MakeText("NumberIQPuzzleHeading", board, 34);
        heading.text = "Loading puzzles...";
        operationsLabel = MakeText("OperationsLabel", board, 22);
        currentLabel = MakeText("CurrentNumber", board, 28);
        currentLabel.color = new Color(.76f, .12f, .19f);
        candidateLabel = MakeText("CandidatesHeading", board, 20);
        candidateLabel.text = "NEXT NUMBERS";
        viewport = MakeRect("NumberViewport", board);
        viewport.gameObject.AddComponent<RectMask2D>();
        var hitArea = viewport.gameObject.AddComponent<Image>();
        hitArea.color = new Color(0, 0, 0, 0);
        scroll = viewport.gameObject.AddComponent<ScrollRect>();
        content = MakeRect("NumberCandidates", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(.5f, 1);
        scroll.viewport = viewport;
        scroll.content = content;
        solutionText = MakeText("NumberSolution", content, 23);
        solutionText.alignment = TextAlignmentOptions.TopLeft;
        solutionText.enableAutoSizing = false;
        solutionText.textWrappingMode = TextWrappingModes.Normal;
        solutionText.gameObject.SetActive(false);
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        ladderViewport = MakeRect("LadderViewport", board);
        ladderViewport.gameObject.AddComponent<RectMask2D>();
        ladderViewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
        ladderScroll = ladderViewport.gameObject.AddComponent<ScrollRect>();
        ladderText = MakeText("NumberLadder", ladderViewport, 26);
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
        undoButton.onClick.AddListener(UndoNumber);
        undoButton.gameObject.SetActive(true);
        undoButton.interactable = false;
        restart = MakeButton("RetryNumberButton", "RETRY", ShowPuzzle);
        restart.transform.SetParent(board, false);
        restart.interactable = false;
        startButton.transform.SetParent(board, false);
        startButton.onClick = new Button.ButtonClickedEvent();
        startButton.onClick.AddListener(StartPuzzle);
        startButton.gameObject.SetActive(true);
        startButton.interactable = false;
        revealPathButton = MakeButton("RevealPathButton", "Reveal Path", RevealPath);
        revealPathButton.transform.SetParent(board, false);
        revealPathButton.interactable = false;
        CreateRevealPanel();
        canvas.GetComponent<TspResponsiveLayout>().RegisterWordBrowser(previous, next, counter, doneButton, filterButton);
    }

    IEnumerator Start()
    {
        yield return NumberIQPuzzleLoader.Load(OnPackLoaded, OnLoadFailed);
    }

    void OnPackLoaded(NumberIQPack pack)
    {
        loading = false;
        packId = pack.packId;
        puzzles.AddRange(pack.puzzles);
        foreach (var puzzle in puzzles)
            if (!difficultyRanks.Contains(puzzle.difficultyRank)) difficultyRanks.Add(puzzle.difficultyRank);
        difficultyRanks.Sort();
        nodeCountDropdown.ClearOptions();
        nodeCountDropdown.AddOptions(difficultyRanks.ConvertAll(DifficultyLabel));
        nodeCountDropdown.SetValueWithoutNotify(0);
        nodeCountDropdown.RefreshShownValue();
        SelectDifficulty(0);
    }

    static string DifficultyLabel(int rank) => rank == 1 ? "Beginner" : rank == 2 ? "Intermediate" : "Advanced";

    void OnLoadFailed(string error)
    {
        loading = false;
        Debug.LogError("NumberIQ: " + error);
        heading.text = "Unable to load puzzles";
        statusText.text = "Check StreamingAssets/numberiq-puzzles.json. See Console for details.";
        candidateLabel.text = "Return to menu and try again";
        counter.text = "0 of 0";
        nodeCountDropdown.interactable = previous.interactable = next.interactable = filterButton.interactable = false;
        LayoutBoard();
    }

    void SelectDifficulty(int option)
    {
        if (loading || timerRunning) return;
        index = 0;
        RebuildMatching();
        ShowPuzzle();
    }
    void Browse(int direction)
    {
        if (timerRunning || matching.Count == 0) return;
        index = (index + direction + matching.Count) % matching.Count;
        ShowPuzzle();
    }
    void ShowPuzzle()
    {
        ClosePath();
        timerRunning = false;
        started = false;
        elapsedSeconds = 0d;
        UpdateTimerDisplay();
        ladder.Clear();
        if (matching.Count > 0) ladder.Add(matching[index].startNumber);
        RefreshNumbers();
    }
    void SelectNumber(int number)
    {
        if (PathVisible || !started || !timerRunning || matching.Count == 0 || !candidates.Contains(number)) return;
        // Re-check rules rather than trusting a stale tile callback.
        if (!NumberIQLogic.Candidates(matching[index], ladder).Exists(c => c.number == number)) return;
        ladder.Add(number);
        if (number == matching[index].targetNumber) StopTimer();
        RefreshNumbers();
    }

    void UndoNumber()
    {
        // A finished result reveals the solution; start a fresh attempt with Retry.
        if (PathVisible || !timerRunning || ladder.Count <= 1) return;
        ladder.RemoveAt(ladder.Count - 1);
        RefreshNumbers();
    }

    void RefreshNumbers()
    {
        UpdateDoneButton();
        revealPathButton.interactable = !loading && matching.Count > 0;
        filterButton.SetIsOnWithoutNotify(!onlyNotDone);
        UpdatePuzzleSwitchAppearance(!onlyNotDone);
        filterButton.interactable = !timerRunning && !loading;
        foreach (var tile in tiles) { tile.gameObject.SetActive(false); Destroy(tile.gameObject); }
        tiles.Clear();
        candidates.Clear();
        solutionText.gameObject.SetActive(false);
        undoButton.interactable = timerRunning && ladder.Count > 1;
        restart.interactable = started;
        startButton.interactable = !started && matching.Count > 0;
        ladderText.text = string.Join("  →  ", ladder);
        counter.text = matching.Count == 0 ? "0 of 0" : $"{index + 1} of {matching.Count}";
        previous.interactable = next.interactable = !timerRunning && matching.Count > 1;
        nodeCountDropdown.interactable = !timerRunning && difficultyRanks.Count > 0;
        currentLabel.text = "";
        if (matching.Count == 0)
        {
            heading.text = onlyNotDone ? "No unfinished puzzles" : "No puzzles available";
            statusText.text = onlyNotDone
                ? "Turn on All Puzzles to revisit or unmark completed puzzles."
                : "No puzzles are available for this difficulty.";
            candidateLabel.text = operationsLabel.text = "";
            LayoutBoard();
            return;
        }
        var p = matching[index];
        heading.text = p.startNumber + " → " + p.targetNumber;
        operationsLabel.text = "OPERATIONS: " + string.Join("   |   ", p.operations.ConvertAll(NumberIQLogic.OperationLabel));
        int current = ladder[ladder.Count - 1];
        bool complete = started && current == p.targetNumber;
        int turns = ladder.Count - 1;
        currentLabel.text = (complete ? "TARGET: " : "CURRENT: ") + current + "   |   " + turns
            + (p.rules.maxTurns > 0 ? "/" + p.rules.maxTurns : "") + " moves";
        if (started && !complete) foreach (var candidate in NumberIQLogic.Candidates(p, ladder))
        {
            int number = candidate.number;
            candidates.Add(number);
            var tile = MakeRect("Candidate_" + number, content);
            var bg = tile.gameObject.AddComponent<Image>();
            bg.color = new Color(.08f, .39f, .44f);
            bg.raycastTarget = true;
            var button = tile.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(() => SelectNumber(number));
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            var text = MakeText("Number", tile, 28);
            text.text = number + "\n<size=65%>" + string.Join(" / ", candidate.labels) + "</size>";
            text.color = Color.white;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(8, 4);
            text.rectTransform.offsetMax = new Vector2(-8, -4);
            tiles.Add(tile);
        }
        bool limitReached = p.rules.maxTurns > 0 && turns >= p.rules.maxTurns;
        candidateLabel.text = !started ? "SELECT START TO BEGIN" : complete ? "PUZZLE COMPLETE"
            : limitReached ? "TURN LIMIT — UNDO OR RETRY" : tiles.Count > 0 ? "NEXT NUMBERS" : "NO NEXT NUMBERS — UNDO OR RETRY";
        statusText.text = !started ? "Reach the target in the fewest moves. START begins the timer."
            : complete ? "Mark DONE, choose another puzzle, or RETRY for a fresh attempt."
            : tiles.Count == 0 ? "Select UNDO to go back, or RETRY to start again."
            : "Choose a number. Whole numbers only; no repeats. Undo keeps the timer running.";
        if (complete)
        {
            int extra = turns - p.optimalTurns;
            solutionText.text = "<b>" + (extra == 0 ? "OPTIMAL!" : "TARGET REACHED") + "</b>\n"
                + "Your moves: " + turns + "   |   Optimal: " + p.optimalTurns + "\n"
                + "Extra moves: " + extra + "   |   Time: " + ElapsedSeconds.ToString("0.0") + "s\n\n"
                + "<b>One optimal ladder</b>\n" + string.Join(" → ", p.optimalPath);
            solutionText.gameObject.SetActive(true);
        }
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
        Place(heading.rectTransform, 12, 8, w - 24, 42);
        Place(timerText.rectTransform, 12, 50, w - 24, 30);
        Place(operationsLabel.rectTransform, 12, 83, w - 24, 34);
        Place(ladderViewport, 16, 122, w - 32, 42);
        Place(ladderText.rectTransform, 0, 0, Mathf.Max(w - 32, ladderText.preferredWidth + 16), 42);
        Place(currentLabel.rectTransform, 12, 170, w - 24, 32);
        Place(candidateLabel.rectTransform, 12, 208, w - 24, 30);
        Place(viewport, 16, 246, w - 32, Mathf.Max(48, h - 368));
        Place((RectTransform)revealPathButton.transform, 16, h - 112, w - 32, 44);
        LayoutRevealPanel(w, h);
        float actionWidth = (w - 56) / 3;
        Place((RectTransform)startButton.transform, 16, h - 60, actionWidth, 44);
        Place((RectTransform)undoButton.transform, 28 + actionWidth, h - 60, actionWidth, 44);
        Place((RectTransform)restart.transform, 40 + 2 * actionWidth, h - 60, actionWidth, 44);
        float available = Mathf.Max(1, w - 32);
        int columns = Mathf.Clamp(Mathf.FloorToInt((available + 12) / 148), 1, 3);
        float tileWidth = (available - (columns - 1) * 12) / columns;
        int rows = Mathf.CeilToInt(tiles.Count / (float)columns);
        float contentHeight = Mathf.Max(viewport.rect.height, rows * 94 - 12);
        if (solutionText.gameObject.activeSelf)
        {
            float resultHeight = solutionText.GetPreferredValues(solutionText.text, available - 8, 0).y + 16;
            Place(solutionText.rectTransform, 4, 0, available - 8, resultHeight);
            contentHeight = Mathf.Max(contentHeight, resultHeight);
        }
        content.sizeDelta = new Vector2(0, contentHeight);
        for (int i = 0; i < tiles.Count; i++)
            Place(tiles[i], (i % columns) * (tileWidth + 12), (i / columns) * 94, tileWidth, 82);
    }
    void CreateRevealPanel()
    {
        revealPanel = MakeRect("NumberIQRevealPanel", board);
        var backdrop = revealPanel.gameObject.AddComponent<Image>();
        backdrop.color = new Color(.94f, .96f, .98f, 1f);
        backdrop.raycastTarget = true;
        revealHeading = MakeText("RevealHeading", revealPanel, 30);
        revealViewport = MakeRect("RevealViewport", revealPanel);
        revealViewport.gameObject.AddComponent<RectMask2D>();
        revealViewport.gameObject.AddComponent<Image>().color = Color.clear;
        revealScroll = revealViewport.gameObject.AddComponent<ScrollRect>();
        revealText = MakeText("RevealNumbers", revealViewport, 28);
        revealText.enableAutoSizing = false;
        revealText.textWrappingMode = TextWrappingModes.Normal;
        revealText.alignment = TextAlignmentOptions.Top;
        revealScroll.viewport = revealViewport;
        revealScroll.content = revealText.rectTransform;
        revealScroll.horizontal = false;
        revealScroll.vertical = true;
        revealScroll.movementType = ScrollRect.MovementType.Clamped;
        closePathButton = MakeButton("ClosePathButton", "Close", ClosePath);
        closePathButton.transform.SetParent(revealPanel, false);
        closePathButton.interactable = true;
        var navigation = closePathButton.navigation;
        navigation.mode = Navigation.Mode.None;
        closePathButton.navigation = navigation;
        revealPanel.gameObject.SetActive(false);
    }

    static bool HasArtwork(Graphic graphic)
    {
        return (graphic is Image image && image.overrideSprite != null)
            || (graphic is RawImage raw && raw.texture != null);
    }

    Graphic FindGameBackground()
    {
        if (revealBackgroundSource != null) return revealBackgroundSource;
        // Prefer artwork assigned directly to the game board.
        var boardGraphic = board.GetComponent<Graphic>();
        if (HasArtwork(boardGraphic)) return boardGraphic;
        Graphic best = null;
        float bestArea = -1;
        foreach (var graphic in board.GetComponentInParent<Canvas>().GetComponentsInChildren<Graphic>())
        {
            if (!graphic.enabled || graphic.transform.IsChildOf(revealPanel)
                || !HasArtwork(graphic) || graphic.GetComponentInParent<Selectable>() != null) continue;
            string name = graphic.name.ToLowerInvariant();
            if (!name.Contains("background") && name != "bg") continue;
            var rect = graphic.rectTransform.rect;
            float area = rect.width * rect.height;
            if (area > bestArea) { best = graphic; bestArea = area; }
        }
        return best != null ? best : boardGraphic;
    }

    void CopyGameBackground()
    {
        // Refresh on each reveal so later changes to the game's artwork are reflected.
        if (revealBackground != null)
        {
            revealBackground.gameObject.SetActive(false);
            Destroy(revealBackground.gameObject);
            revealBackground = null;
        }
        var source = FindGameBackground();
        if (source == null) return;
        var rt = MakeRect("RevealGameBackground", revealPanel);
        rt.SetAsFirstSibling();
        if (source is RawImage raw)
        {
            var copy = rt.gameObject.AddComponent<RawImage>();
            copy.texture = raw.texture;
            copy.uvRect = raw.uvRect;
            revealBackground = copy;
        }
        else if (source is Image image)
        {
            var copy = rt.gameObject.AddComponent<Image>();
            copy.sprite = image.overrideSprite;
            copy.type = image.type;
            copy.preserveAspect = image.preserveAspect;
            copy.fillCenter = image.fillCenter;
            copy.fillMethod = image.fillMethod;
            copy.fillAmount = image.fillAmount;
            copy.fillClockwise = image.fillClockwise;
            copy.fillOrigin = image.fillOrigin;
            copy.pixelsPerUnitMultiplier = image.pixelsPerUnitMultiplier;
            revealBackground = copy;
        }
        else { Destroy(rt.gameObject); return; }
        revealBackground.color = source.color;
        revealBackground.raycastTarget = false;
    }

    void RevealPath()
    {
        if (loading || matching.Count == 0) return;
        var p = matching[index];
        revealHeading.text = "SOLUTION LADDER — " + p.optimalTurns + " MOVES";
        var lines = new List<string> { p.optimalPath[0].ToString() };
        for (int i = 0; i < p.optimalOperationIds.Count; i++)
        {
            var op = p.operations.Find(o => o.id == p.optimalOperationIds[i]);
            lines.Add("<size=75%>↓  " + NumberIQLogic.OperationLabel(op) + "</size>");
            lines.Add(p.optimalPath[i + 1].ToString());
        }
        revealText.text = string.Join("\n", lines);
        CopyGameBackground();
        revealPanel.gameObject.SetActive(true);
        revealPanel.SetAsLastSibling();
        LayoutBoard();
        Canvas.ForceUpdateCanvases();
        revealScroll.StopMovement();
        revealScroll.verticalNormalizedPosition = 1;
        closePathButton.Select();
        // Match WordIQ: preserve progress, elapsed timer and Done status.
    }

    void ClosePath()
    {
        if (revealPanel == null) return;
        revealPanel.gameObject.SetActive(false);
        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
    }

    void LayoutRevealPanel(float w, float h)
    {
        if (revealPanel == null) return;
        Place(revealPanel, 0, 0, w, h);
        if (revealBackground != null) Place(revealBackground.rectTransform, 0, 0, w, h);
        Place(revealHeading.rectTransform, 16, 16, w - 32, 48);
        float viewHeight = Mathf.Max(48, h - 144);
        Place(revealViewport, 16, 72, w - 32, viewHeight);
        float textHeight = revealText.GetPreferredValues(revealText.text, w - 32, 0).y + 16;
        Place(revealText.rectTransform, 0, 0, w - 32, Mathf.Max(viewHeight, textHeight));
        Place((RectTransform)closePathButton.transform, 16, h - 60, w - 32, 44);
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
