using System;
using System.Collections.Generic;

[Serializable]
public class NumberIQPack
{
    public int schemaVersion;
    public string packId, title;
    public List<NumberIQPuzzle> puzzles;
}

[Serializable]
public class NumberIQPuzzle
{
    public string id, title, difficulty;
    public int difficultyRank, startNumber, targetNumber, optimalTurns;
    public NumberIQRules rules;
    public List<NumberIQOperation> operations;
    public List<int> optimalPath;
    public List<string> optimalOperationIds;
}

[Serializable]
public class NumberIQRules
{
    public int minNumber, maxNumber, maxTurns;
    public bool integersOnly, allowRepeatedNumbers, allowNoOpMoves;
}

[Serializable]
public class NumberIQOperation
{
    public string id, type, label;
    public int operand;
}

public class NumberIQCandidate
{
    public int number;
    public readonly List<string> operationIds = new List<string>();
    public readonly List<string> labels = new List<string>();
}

// Pure game rules: no Unity dependencies, shared by loading and gameplay.
public static class NumberIQLogic
{
    public static string OperationLabel(NumberIQOperation op)
    {
        switch (op.type)
        {
            case "add": return "+" + op.operand;
            case "subtract": return "-" + op.operand;
            case "multiply": return "x" + op.operand;
            case "divide": return "/" + op.operand;
            case "reverseDigits": return "Reverse";
            default: return "?";
        }
    }

    public static bool TryApply(int current, NumberIQOperation op, NumberIQRules rules, out int result)
    {
        result = 0;
        if (op == null || rules == null || current < rules.minNumber || current > rules.maxNumber)
            return false;
        long value;
        if (op.type != "reverseDigits" && op.operand <= 0) return false;
        switch (op.type)
        {
            case "add": value = (long)current + op.operand; break;
            case "subtract": value = (long)current - op.operand; break;
            case "multiply": value = (long)current * op.operand; break;
            case "divide":
                if (current % op.operand != 0) return false;
                value = current / op.operand;
                break;
            case "reverseDigits":
                value = 0;
                for (int digits = current; digits > 0; digits /= 10)
                    value = value * 10 + digits % 10;
                break;
            default: return false;
        }
        if (value < rules.minNumber || value > rules.maxNumber ||
            (!rules.allowNoOpMoves && value == current)) return false;
        result = (int)value;
        return true;
    }

    public static List<NumberIQCandidate> Candidates(NumberIQPuzzle puzzle, IList<int> ladder)
    {
        var result = new List<NumberIQCandidate>();
        if (puzzle == null || ladder == null || ladder.Count == 0) return result;
        int current = ladder[ladder.Count - 1];
        if (current == puzzle.targetNumber ||
            (puzzle.rules.maxTurns > 0 && ladder.Count - 1 >= puzzle.rules.maxTurns)) return result;
        foreach (var op in puzzle.operations)
        {
            int number;
            if (!TryApply(current, op, puzzle.rules, out number) ||
                (!puzzle.rules.allowRepeatedNumbers && ladder.Contains(number))) continue;
            var candidate = result.Find(c => c.number == number);
            if (candidate == null)
            {
                candidate = new NumberIQCandidate { number = number };
                result.Add(candidate);
            }
            candidate.operationIds.Add(op.id);
            candidate.labels.Add(OperationLabel(op));
        }
        return result;
    }

    public static int ShortestTurns(NumberIQPuzzle puzzle)
    {
        var distance = new Dictionary<int, int> { { puzzle.startNumber, 0 } };
        var queue = new Queue<int>();
        queue.Enqueue(puzzle.startNumber);
        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            if (current == puzzle.targetNumber) return distance[current];
            foreach (var op in puzzle.operations)
            {
                int next;
                if (!TryApply(current, op, puzzle.rules, out next) || distance.ContainsKey(next)) continue;
                distance[next] = distance[current] + 1;
                queue.Enqueue(next);
            }
        }
        return -1;
    }

    public static bool Validate(NumberIQPuzzle p, out string error)
    {
        error = "Invalid identity, difficulty, rules or operations.";
        if (p == null || string.IsNullOrEmpty(p.id) ||
            (p.difficulty != "beginner" && p.difficulty != "intermediate" && p.difficulty != "advanced") ||
            p.difficultyRank != (p.difficulty == "beginner" ? 1 : p.difficulty == "intermediate" ? 2 : 3) ||
            p.rules == null || p.rules.minNumber < 1 || p.rules.maxNumber > 99 ||
            p.rules.maxNumber < p.rules.minNumber || !p.rules.integersOnly ||
            p.rules.allowRepeatedNumbers || p.rules.allowNoOpMoves || p.rules.maxTurns < 0 ||
            p.operations == null || p.operations.Count != 3) return false;
        if (p.startNumber < p.rules.minNumber || p.startNumber > p.rules.maxNumber ||
            p.targetNumber < p.rules.minNumber || p.targetNumber > p.rules.maxNumber ||
            p.startNumber == p.targetNumber) return false;
        var ids = new HashSet<string>();
        foreach (var op in p.operations)
        {
            if (op == null || string.IsNullOrEmpty(op.id) || !ids.Add(op.id)) return false;
            if (op.type == "reverseDigits") { if (op.operand != 0) return false; }
            else if ((op.type != "add" && op.type != "subtract" && op.type != "multiply" && op.type != "divide") ||
                op.operand <= 0) return false;
        }
        error = "Invalid optimal path.";
        if (p.optimalPath == null || p.optimalOperationIds == null || p.optimalTurns < 1 ||
            p.optimalPath.Count != p.optimalTurns + 1 || p.optimalOperationIds.Count != p.optimalTurns ||
            p.optimalPath[0] != p.startNumber || p.optimalPath[p.optimalTurns] != p.targetNumber ||
            new HashSet<int>(p.optimalPath).Count != p.optimalPath.Count) return false;
        for (int i = 0; i < p.optimalTurns; i++)
        {
            var op = p.operations.Find(o => o.id == p.optimalOperationIds[i]);
            int next;
            if (!TryApply(p.optimalPath[i], op, p.rules, out next) || next != p.optimalPath[i + 1]) return false;
        }
        error = "Stored solution is not shortest, or exceeds the turn limit.";
        if (ShortestTurns(p) != p.optimalTurns || (p.rules.maxTurns > 0 && p.optimalTurns > p.rules.maxTurns)) return false;
        error = null;
        return true;
    }
}
