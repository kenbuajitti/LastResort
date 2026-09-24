using System;
using System.Collections.Generic;

[Serializable] public class WordPack
{
    public int schemaVersion;
    public string packId;
    public List<WordPuzzle> puzzles;
}
[Serializable] public class WordPuzzle
{
    public string id, startWord, targetWord, difficulty;
    public int wordLength, solutionMoves;
    public List<string> solutionPath;
    public List<WordNode> nodes;
    public WordRules rules;
}
[Serializable] public class WordRules
{
    public bool allowRepeatedWords, uniqueSolution, allowRearrangement, allowAnagramOnlyMove;
    public int maxTurns, maxLetterReplacementsPerTurn;
    public string candidateScope;
}
[Serializable] public class WordNode { public string word; public List<string> nextWords; }

// Pure C#: loader rejects inconsistent difficulty, hidden edges and alternative routes.
public static class WordIQPuzzleValidation
{
    static readonly string[] Levels = { "Beginner", "Intermediate", "Advanced", "Expert" };
    static bool IsWord(string word, int length)
    {
        if (word == null || word.Length != length) return false;
        foreach (char c in word) if (c < 'A' || c > 'Z') return false;
        return true;
    }
    static bool Legal(string a, string b)
    {
        if (a == b) return false;
        var count = new int[26];
        foreach (char c in a) count[c - 'A']++;
        foreach (char c in b) count[c - 'A']--;
        int replacements = 0;
        foreach (int n in count) if (n > 0) replacements += n;
        return replacements <= 1;
    }
    public static bool Valid(WordPuzzle p)
    {
        if (p == null || string.IsNullOrEmpty(p.id) || p.wordLength < 2 || p.wordLength > 15 ||
            p.solutionMoves < 3 || p.solutionMoves > 6 || p.difficulty != Levels[p.solutionMoves - 3] ||
            p.nodes == null || p.nodes.Count > 64 || p.rules == null ||
            p.rules.allowRepeatedWords || !p.rules.uniqueSolution || !p.rules.allowRearrangement ||
            !p.rules.allowAnagramOnlyMove || p.rules.maxLetterReplacementsPerTurn != 1 ||
            p.rules.candidateScope != "puzzleWordsOnly" || p.rules.maxTurns != p.solutionMoves ||
            !IsWord(p.startWord, p.wordLength) || !IsWord(p.targetWord, p.wordLength) ||
            p.startWord == p.targetWord) return false;
        var graph = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        int edges = 0;
        foreach (var n in p.nodes)
        {
            if (n == null || !IsWord(n.word, p.wordLength) || graph.ContainsKey(n.word) || n.nextWords == null) return false;
            var neighbors = new HashSet<string>(n.nextWords, StringComparer.Ordinal);
            if (neighbors.Count != n.nextWords.Count) return false;
            graph.Add(n.word, neighbors);
            edges += neighbors.Count;
        }
        if (!graph.ContainsKey(p.startWord) || !graph.ContainsKey(p.targetWord)) return false;
        foreach (var entry in graph)
        {
            foreach (string neighbor in entry.Value)
                if (neighbor == null || !graph.ContainsKey(neighbor)) return false;
            foreach (string word in graph.Keys)
                if (entry.Value.Contains(word) != Legal(entry.Key, word)) return false;
        }
        var seen = new HashSet<string>();
        var pending = new Stack<string>();
        pending.Push(p.startWord);
        while (pending.Count > 0)
        {
            string word = pending.Pop();
            if (seen.Add(word)) foreach (string neighbor in graph[word]) pending.Push(neighbor);
        }
        // A connected undirected tree has exactly one simple path between any pair.
        if (seen.Count != graph.Count || edges != 2 * (graph.Count - 1)) return false;
        if (p.solutionPath == null || p.solutionPath.Count != p.solutionMoves + 1 ||
            p.solutionPath[0] != p.startWord || p.solutionPath[p.solutionMoves] != p.targetWord) return false;
        seen.Clear();
        for (int i = 0; i < p.solutionPath.Count; i++)
        {
            string word = p.solutionPath[i];
            if (word == null || !graph.ContainsKey(word) || !seen.Add(word)) return false;
            if (i > 0 && !graph[p.solutionPath[i - 1]].Contains(word)) return false;
        }
        return true;
    }
}
