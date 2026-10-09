using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LastResort
{
    // Presentation only: server request IDs, choices and twenty-decision contract remain authoritative.
    public sealed partial class LastResortController
    {
        RectTransform novelRoot, novelArtFrame, novelDialogue, novelChoices;
        RawImage novelArt;
        TMP_Text novelTitle, novelLine, novelCounter;
        Button novelBack, novelNext;
        readonly List<string> novelLines = new List<string>();
        readonly List<int> novelOffsets = new List<int>();
        readonly List<int> novelMoods = new List<int>();
        readonly List<Button> novelChoiceButtons = new List<Button>();
        readonly Dictionary<string, int> novelBookmarks = new Dictionary<string, int>();
        string novelText = "", novelKey = "";
        int novelIndex;
        bool novelPicking;

        void BuildNovel()
        {
            novelRoot = Panel("Visual novel", frame, new Color(.025f, .045f, .09f));
            SetBounds(novelRoot, Vector2.zero, Vector2.one, new Vector2(14, 104), new Vector2(-14, -96));
            novelArtFrame = Rect("Artwork frame", novelRoot);
            SetBounds(novelArtFrame, new Vector2(0, .30f), Vector2.one, Vector2.zero, Vector2.zero);
            novelArtFrame.gameObject.AddComponent<RectMask2D>();
            RectTransform picture = Rect("Mood illustration", novelArtFrame);
            Stretch(picture);
            novelArt = picture.gameObject.AddComponent<RawImage>();
            novelArt.texture = Resources.Load<Texture2D>("StoryIQ/Moods");
            novelArt.raycastTarget = false;
            novelTitle = Label(novelArtFrame, "", 25, Color.white, false);
            var titleBack = Panel("Scene heading", novelArtFrame, new Color(.02f, .03f, .07f, .8f));
            SetBounds(titleBack, new Vector2(0, 1), Vector2.one, new Vector2(0, -50), Vector2.zero);
            novelTitle.transform.SetParent(titleBack, false);
            Stretch(novelTitle.rectTransform);
            novelTitle.rectTransform.offsetMin = new Vector2(16, 5);
            novelTitle.rectTransform.offsetMax = new Vector2(-16, -5);
            novelTitle.enableAutoSizing = true;
            novelTitle.fontSizeMin = 16;
            novelTitle.fontSizeMax = 25;
            novelTitle.alignment = TextAlignmentOptions.MidlineLeft;

            novelDialogue = Panel("Click to advance", novelRoot, new Color(.035f, .06f, .12f));
            SetBounds(novelDialogue, Vector2.zero, new Vector2(1, .30f), Vector2.zero, Vector2.zero);
            var advance = novelDialogue.gameObject.AddComponent<Button>();
            advance.targetGraphic = novelDialogue.GetComponent<Image>();
            advance.transition = Selectable.Transition.None;
            advance.onClick.AddListener(AdvanceNovel);
            novelLine = Label(novelDialogue, "", 25, cream, false);
            SetBounds(novelLine.rectTransform, Vector2.zero, Vector2.one, new Vector2(18, 52), new Vector2(-18, -8));
            novelLine.alignment = TextAlignmentOptions.MidlineLeft;
            novelLine.overflowMode = TextOverflowModes.Overflow;
            novelBack = FlatButton(novelDialogue, "BACK", BackNovel);
            SetBounds(novelBack.GetComponent<RectTransform>(), Vector2.zero, new Vector2(.24f, 0), new Vector2(12, 8), new Vector2(-4, 44));
            novelNext = FlatButton(novelDialogue, "NEXT", AdvanceNovel);
            SetBounds(novelNext.GetComponent<RectTransform>(), new Vector2(.76f, 0), new Vector2(1, 0), new Vector2(4, 8), new Vector2(-12, 44));
            novelCounter = Label(novelDialogue, "", 16, cream, false);
            SetBounds(novelCounter.rectTransform, new Vector2(.25f, 0), new Vector2(.75f, 0), new Vector2(0, 8), new Vector2(0, 44));
            novelCounter.alignment = TextAlignmentOptions.Center;
            novelChoices = Panel("Your response", novelRoot, new Color(.025f, .045f, .085f, .97f));
            SetBounds(novelChoices, new Vector2(0, .30f), Vector2.one, new Vector2(10, 8), new Vector2(-10, -8));
            novelChoices.gameObject.SetActive(false);
        }

        void PresentNovelPage()
        {
            if (novelRoot == null) BuildNovel();
            foreach (var old in novelChoiceButtons) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
            novelChoiceButtons.Clear();
            var labels = new List<string>();
            var actions = new List<Button>();
            foreach (Transform child in content)
            {
                if (!child.gameObject.activeSelf) continue;
                var button = child.GetComponent<Button>();
                if (button != null) { actions.Add(button); continue; }
                var label = child.GetComponent<TMP_Text>();
                if (label != null && label.text != "What do you do?") labels.Add(label.text);
            }
            novelTitle.text = labels.Count > 1 ? labels[1] : "StoryIQ";
            var body = new StringBuilder();
            for (int i = 2; i < labels.Count; i++) body.AppendLine(labels[i]).AppendLine();
            novelText = body.ToString().Trim();
            novelKey = progress.text + "\n" + novelText;
            foreach (var original in actions)
            {
                Button source = original;
                Button choice = FlatButton(novelChoices, source.GetComponentInChildren<TMP_Text>().text,
                    () => { novelBookmarks.Remove(novelKey); source.onClick.Invoke(); });
                novelChoiceButtons.Add(choice);
            }
            scroll.gameObject.SetActive(false);
            readingHint.gameObject.SetActive(false);
            novelRoot.gameObject.SetActive(true);
            novelPicking = false;
            novelIndex = 0;
            novelArt.texture = castMode ? CastPortrait() : Resources.Load<Texture2D>("StoryIQ/Moods");
            int offset;
            if (castMode) offset = 0;
            else novelBookmarks.TryGetValue(novelKey, out offset);
            ReflowNovel(offset);
        }

        void RefreshNovel()
        {
            int offset = novelIndex < novelOffsets.Count ? novelOffsets[novelIndex] : 0;
            ReflowNovel(offset);
        }

        void ReflowNovel(int offset)
        {
            Canvas.ForceUpdateCanvases();
            // Reserve enough physical height for a single line and the controls on small windows.
            float dialogueHeight = Mathf.Max(104, novelRoot.rect.height * .30f);
            dialogueHeight = Mathf.Min(dialogueHeight, novelRoot.rect.height * .55f);
            float fraction = dialogueHeight / Mathf.Max(1, novelRoot.rect.height);
            novelDialogue.anchorMax = new Vector2(1, fraction);
            novelArtFrame.anchorMin = new Vector2(0, fraction);
            novelChoices.anchorMin = new Vector2(0, fraction);
            Canvas.ForceUpdateCanvases();
            float width = Mathf.Max(40, novelLine.rectTransform.rect.width);
            novelLine.fontSize = safeArea.rect.width < 650 ? 22 : 25;
            novelLines.Clear(); novelOffsets.Clear(); novelMoods.Clear();
            int currentMood = 0;
            // Mood is evaluated for complete sentences; wrapped lines retain the same illustration.
            foreach (Match sentence in Regex.Matches(novelText, @"[^.!?\n]+(?:[.!?]+[""'”’]*|$)|[^\n]+", RegexOptions.Multiline))
            {
                string value = sentence.Value.Trim();
                if (value.Length == 0) continue;
                currentMood = DetectNovelMood(value, currentMood);
                int local = 0;
                while (local < value.Length)
                {
                    while (local < value.Length && char.IsWhiteSpace(value[local])) local++;
                    if (local >= value.Length) break;
                    int remaining = value.Length - local, low = 1, high = remaining, best = 1;
                    while (low <= high)
                    {
                        int mid = (low + high) / 2;
                        float measured = novelLine.GetPreferredValues(value.Substring(local, mid), float.PositiveInfinity, float.PositiveInfinity).x;
                        if (measured <= width - 4) { best = mid; low = mid + 1; } else high = mid - 1;
                    }
                    int length = best;
                    if (best < remaining)
                    {
                        int space = value.LastIndexOf(' ', local + best - 1, best);
                        if (space > local) length = space - local;
                    }
                    novelOffsets.Add(sentence.Index + local);
                    novelLines.Add(value.Substring(local, length).Trim());
                    novelMoods.Add(currentMood);
                    local += length;
                }
            }
            if (novelLines.Count == 0) { novelLines.Add("Continue when ready."); novelOffsets.Add(0); novelMoods.Add(0); }
            novelIndex = 0;
            for (int i = 0; i < novelOffsets.Count; i++) if (novelOffsets[i] <= offset) novelIndex = i;
            int count = novelChoiceButtons.Count;
            for (int i = 0; i < count; i++)
            {
                SetBounds(novelChoiceButtons[i].GetComponent<RectTransform>(), new Vector2(0, 1f - (i + 1f) / count),
                    new Vector2(1, 1f - (float)i / count), new Vector2(6, 4), new Vector2(-6, -4));
                var label = novelChoiceButtons[i].GetComponentInChildren<TMP_Text>();
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.fontSizeMin = 12; label.fontSizeMax = 22;
            }
            DrawNovel();
        }

        static int DetectNovelMood(string text, int previous)
        {
            string s = text.ToLowerInvariant();
            if (Regex.IsMatch(s, @"\b(threat|danger|afraid|fear|shout\w*|scream\w*|storm|blood|weapon|panic|furious|angry|argu\w*)\b")) return 2;
            if (Regex.IsMatch(s, @"\b(secret\w*|warning|note|whisper\w*|shadow\w*|mystery|suspicio\w*|watching|handwriting|unreachable|envelope)\b")) return 1;
            if (Regex.IsMatch(s, @"\b(relief|relieved|forgiv\w*|reconcil\w*|hopeful|safe|peace|reassur\w*)\b")) return 3;
            if (Regex.IsMatch(s, @"\b(welcome|sunshine|laugh\w*|warm|dazzling|smile\w*|holiday|arrival)\b")) return 0;
            return previous;
        }

        void DrawNovel()
        {
            if (castMode) { DrawCastCharacter(); return; }
            Stretch(novelArt.rectTransform);
            novelChoices.gameObject.SetActive(novelPicking);
            novelLine.text = novelPicking ? "Choose your response above." : novelLines[novelIndex];
            novelCounter.text = novelPicking ? "YOUR CHOICE" : (novelIndex + 1) + " / " + novelLines.Count;
            novelBack.interactable = novelPicking || novelIndex > 0;
            novelNext.gameObject.SetActive(!novelPicking);
            novelNext.GetComponentInChildren<TMP_Text>().text = novelIndex == novelLines.Count - 1 ? "CHOICES" : "NEXT";
            novelBookmarks[novelKey] = novelOffsets[novelIndex];
            int mood = novelMoods[novelIndex];
            // RawImage UV uses bottom-left origin; quadrants avoid creating duplicate texture files.
            float x = mood == 1 || mood == 3 ? .5f : 0;
            float y = mood < 2 ? .5f : 0;
            float textureAspect = novelArt.texture != null ? (float)novelArt.texture.width / novelArt.texture.height : 16f / 9f;
            float frameAspect = novelArtFrame.rect.width / Mathf.Max(1, novelArtFrame.rect.height);
            float u = .5f, v = .5f;
            if (frameAspect > textureAspect) v *= textureAspect / frameAspect;
            else u *= frameAspect / textureAspect;
            novelArt.uvRect = new Rect(x + (.5f - u) * .5f, y + (.5f - v) * .5f, u, v);
        }

        void AdvanceNovel()
        {
            if (castMode) { AdvanceCastCharacter(); return; }
            if (novelPicking || liveBusy) return;
            if (novelIndex + 1 < novelLines.Count) novelIndex++;
            else novelPicking = true;
            DrawNovel();
        }

        void BackNovel()
        {
            if (castMode) { BackCastCharacter(); return; }
            if (novelPicking) novelPicking = false;
            else novelIndex = Mathf.Max(0, novelIndex - 1);
            DrawNovel();
        }
    }
}
