using System;
using System.Collections.Generic;

namespace LastResort
{
    [Serializable]
    public sealed class StoryChoice
    {
        public string id;
        public string text;
        public string previewResponse;
    }

    [Serializable]
    public sealed class CastMember
    {
        public string name;
        public string introduction;
    }

    [Serializable]
    public sealed class StorySetup
    {
        public string title;
        public string premise;
        public string openingTitle;
        public string opening;
        public CastMember[] cast;
        public StoryChoice[] choices;

        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(opening)
                || choices == null || choices.Length != 5 || cast == null) return false;
            var ids = new HashSet<string>();
            foreach (StoryChoice choice in choices)
                if (choice == null || string.IsNullOrWhiteSpace(choice.id)
                    || !ids.Add(choice.id) || string.IsNullOrWhiteSpace(choice.text)
                    || string.IsNullOrWhiteSpace(choice.previewResponse)) return false;
            return true;
        }
    }

    // Offline preview history only. Live history is authoritative on the server;
    // its wire types are in LastResortService.cs.
    [Serializable]
    public sealed class DecisionRecord
    {
        public int number;
        public string choiceId;
        public string choiceText;
        public string outcome;
    }

    [Serializable]
    public sealed class StorySession
    {
        public const int DecisionLimit = 20;
        public List<DecisionRecord> decisions = new List<DecisionRecord>();
        public int Completed => decisions.Count;
        public int Day => Math.Min(5, Completed / 4 + 1);

        public bool TryRecord(StoryChoice choice)
        {
            // This interface preview accepts one opening choice only.
            if (choice == null || Completed != 0) return false;
            decisions.Add(new DecisionRecord
            {
                number = 1, choiceId = choice.id, choiceText = choice.text,
                outcome = choice.previewResponse
            });
            return true;
        }
    }
}
