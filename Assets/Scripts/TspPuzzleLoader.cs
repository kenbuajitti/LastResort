using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TspPuzzleLoader : MonoBehaviour
{

    [SerializeField] private TspPuzzleRenderer puzzleRenderer;
    [SerializeField] private TMP_Dropdown nodeCountDropdown;
    public TspPuzzleData CurrentPuzzle { get; private set; }

    public event Action PuzzleChanged;
    public event Action SelectionChanged;
    public bool IsReady { get; private set; }
    public bool SelectionLocked { get; set; }
    public bool NotDoneOnly => notDoneOnly;
    public int CandidateCount => matchingPuzzles.Count;
    public int CandidatePosition => CurrentPuzzle == null ? 0 : currentPuzzleIndex + 1;
    public bool CurrentPuzzleDone => CurrentPuzzle != null && IsDone(CurrentPuzzle);
    public int SelectedNodeCount { get; private set; }

    // Static memory survives scene changes, but never writes progress to disk.
    private static readonly HashSet<string> donePuzzles = new();
    private static bool notDoneOnly;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        donePuzzles.Clear();
        notDoneOnly = false;
    }

    private static string PuzzleKey(TspPuzzleData puzzle) => $"{puzzle.nodes.Count}:{puzzle.id}";
    private static bool IsDone(TspPuzzleData puzzle) => donePuzzles.Contains(PuzzleKey(puzzle));

    public int CurrentNodeCount
    {
        get
        {
            if (CurrentPuzzle == null ||
                CurrentPuzzle.nodes == null)
            {
                return 0;
            }

            return CurrentPuzzle.nodes.Count;
        }
    }

    private TspPuzzleDatabase database;

    private readonly List<TspPuzzleData> matchingPuzzles =
        new();

    private readonly List<int> availableNodeCounts =
        new();

    private int currentPuzzleIndex;

    private void Start()
    {
        LoadPuzzleDatabase();
        IsReady = true;
    }

    private void LoadPuzzleDatabase()
    {
        TextAsset puzzleFile =
            Resources.Load<TextAsset>("puzzles");

        if (puzzleFile == null)
        {
            Debug.LogError(
                "Could not load Assets/Resources/puzzles.json"
            );
            return;
        }

        database =
            JsonUtility.FromJson<TspPuzzleDatabase>(
                puzzleFile.text
            );

        if (database == null ||
            database.puzzles == null ||
            database.puzzles.Count == 0)
        {
            Debug.LogError(
                "The puzzle database is empty or invalid."
            );
            return;
        }

        ConfigureNodeCountDropdown();
    }

    private void ConfigureNodeCountDropdown()
    {
        availableNodeCounts.Clear();
        availableNodeCounts.AddRange(GetAvailableNodeCounts());

        if (availableNodeCounts.Count == 0)
        {
            Debug.LogError("No valid puzzles were found in puzzles.json.");
            return;
        }

        if (nodeCountDropdown == null)
        {
            Debug.LogError(
                "Node Count Dropdown is not assigned on TspPuzzleLoader."
            );

            SelectNodeCount(availableNodeCounts[0]);
            return;
        }

        List<string> options = new();

        foreach (int nodeCount in availableNodeCounts)
            options.Add(nodeCount.ToString());

        nodeCountDropdown.ClearOptions();
        nodeCountDropdown.AddOptions(options);
        nodeCountDropdown.SetValueWithoutNotify(0);
        nodeCountDropdown.RefreshShownValue();

        SelectNodeCount(availableNodeCounts[0]);
    }

    public List<int> GetAvailableNodeCounts()
    {
        List<int> nodeCounts = new();

        if (database == null ||
            database.puzzles == null)
        {
            return nodeCounts;
        }

        foreach (TspPuzzleData puzzle in database.puzzles)
        {
            if (puzzle == null || puzzle.nodes == null)
                continue;

            int nodeCount = puzzle.nodes.Count;

            if (!nodeCounts.Contains(nodeCount))
                nodeCounts.Add(nodeCount);
        }

        nodeCounts.Sort();

        return nodeCounts;
    }

    public bool SelectNodeCount(int nodeCount)
    {
        if (SelectionLocked || database == null || database.puzzles == null)
            return false;

        SelectedNodeCount = nodeCount;
        RebuildCandidates();
        currentPuzzleIndex = 0;
        SetCurrentPuzzle();
        return true;
    }

    private void RebuildCandidates()
    {
        matchingPuzzles.Clear();
        if (database == null || database.puzzles == null) return;
        foreach (TspPuzzleData puzzle in database.puzzles)
        {
            if (puzzle != null && puzzle.nodes != null &&
                puzzle.nodes.Count == SelectedNodeCount && (!notDoneOnly || !IsDone(puzzle)))
                matchingPuzzles.Add(puzzle);
        }
    }

    public void SetNotDoneOnly(bool value)
    {
        if (SelectionLocked || notDoneOnly == value) return;
        TspPuzzleData previous = CurrentPuzzle;
        notDoneOnly = value;
        RebuildCandidates();
        // Keep the visible puzzle if it belongs to the new candidate list.
        currentPuzzleIndex = Math.Max(0, matchingPuzzles.IndexOf(previous));
        SetCurrentPuzzle(previous != null && matchingPuzzles.Contains(previous));
    }

    public void SetCurrentPuzzleDone(bool value)
    {
        if (SelectionLocked || CurrentPuzzle == null) return;
        string key = PuzzleKey(CurrentPuzzle);
        if (value) donePuzzles.Add(key); else donePuzzles.Remove(key);

        if (notDoneOnly)
        {
            // Removing the current entry leaves its successor at the same index.
            // Wrap to the first entry when the removed puzzle was the last one.
            RebuildCandidates();
            if (currentPuzzleIndex >= matchingPuzzles.Count) currentPuzzleIndex = 0;
            SetCurrentPuzzle();
        }
        else SelectionChanged?.Invoke();
    }

    public void LoadNextPuzzle()
    {
        if (SelectionLocked || matchingPuzzles.Count == 0) return;
        currentPuzzleIndex = (currentPuzzleIndex + 1) % matchingPuzzles.Count;
        SetCurrentPuzzle();
    }

    public void LoadPreviousPuzzle()
    {
        if (SelectionLocked || matchingPuzzles.Count == 0) return;
        currentPuzzleIndex = (currentPuzzleIndex + matchingPuzzles.Count - 1) % matchingPuzzles.Count;
        SetCurrentPuzzle();
    }

    private void SetCurrentPuzzle(bool preserveAttempt = false)
    {
        CurrentPuzzle = matchingPuzzles.Count == 0 ? null : matchingPuzzles[currentPuzzleIndex];
        if (!preserveAttempt) PuzzleChanged?.Invoke();
        SelectionChanged?.Invoke();
    }

    public void OnNodeCountDropdownChanged(int optionIndex)
    {
        if (optionIndex < 0 || optionIndex >= availableNodeCounts.Count || SelectionLocked)
            return;
        SelectNodeCount(availableNodeCounts[optionIndex]);
    }
}
