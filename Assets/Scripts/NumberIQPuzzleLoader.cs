using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

// Called by the existing scene controller; no extra Inspector component is needed.
public static class NumberIQPuzzleLoader
{
    public static IEnumerator Load(Action<NumberIQPack> loaded, Action<string> failed)
    {
        string path = Application.streamingAssetsPath.TrimEnd('/') + "/numberiq-puzzles.json";
        string json = null;
        string error = null;
        // WebGL and Android StreamingAssets paths are URLs, not filesystem paths.
        if (path.Contains("://") || path.StartsWith("jar:"))
        {
            using (var request = UnityWebRequest.Get(path))
            {
                request.timeout = 30;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                    error = "Could not load numberiq-puzzles.json: " + request.error;
                else json = request.downloadHandler.text;
            }
        }
        else
        {
            try { json = File.ReadAllText(path); }
            catch (Exception e) { error = "Could not read numberiq-puzzles.json: " + e.Message; }
        }
        if (error != null) { failed(error); yield break; }
        NumberIQPack pack = null;
        try
        {
            pack = JsonUtility.FromJson<NumberIQPack>(json);
            if (pack == null || pack.schemaVersion != 1 || string.IsNullOrEmpty(pack.packId) ||
                pack.puzzles == null || pack.puzzles.Count == 0)
                throw new FormatException("Expected NumberIQ schemaVersion 1, packId and a nonempty puzzles array.");
            var ids = new HashSet<string>();
            foreach (var puzzle in pack.puzzles)
            {
                string reason;
                if (!NumberIQLogic.Validate(puzzle, out reason))
                    throw new FormatException("Puzzle " + (puzzle == null ? "(null)" : puzzle.id) + ": " + reason);
                if (!ids.Add(puzzle.id)) throw new FormatException("Duplicate puzzle id: " + puzzle.id);
            }
        }
        catch (Exception e) { error = "Invalid NumberIQ puzzle pack: " + e.Message; }
        if (error != null) failed(error);
        else loaded(pack);
    }
}
