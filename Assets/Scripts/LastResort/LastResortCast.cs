using TMPro;
using UnityEngine;

namespace LastResort
{
    public sealed partial class LastResortController
    {
        bool castMode;
        int castIndex;

        void ShowCastCharacter(int index)
        {
            if (setup.cast == null || setup.cast.Length == 0) { ShowMenu(); return; }
            castIndex = Mathf.Clamp(index, 0, setup.cast.Length - 1);
            ClearPage("MEET THE CHARACTERS  |  " + (castIndex + 1) + " / " + setup.cast.Length);
            castMode = true;
            showingStory = false;
            home.gameObject.SetActive(true);
            CastMember person = setup.cast[castIndex];
            Label(content, "THE PEOPLE AT THE BELLWEATHER", 23, teal);
            Label(content, person.name, 30, ink);
            Label(content, person.introduction, 25, ink);
            novelRequested = true;
            FinishPage();
        }

        Texture2D CastPortrait()
        {
            string name = setup.cast[castIndex].name;
            string[] keys = { "You", "Vivian", "Felix", "June", "Penny", "Arthur", "Rosa", "Graham", "Tamsin" };
            foreach (string key in keys)
                if (name.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return Resources.Load<Texture2D>("StoryIQ/Characters/" + key);
            return Resources.Load<Texture2D>("StoryIQ/Characters/You");
        }

        void DrawCastCharacter()
        {
            novelChoices.gameObject.SetActive(false);
            novelLine.text = novelLines[novelIndex];
            novelCounter.text = (novelIndex + 1) + " / " + novelLines.Count;
            novelBack.interactable = novelIndex > 0 || castIndex > 0;
            novelNext.gameObject.SetActive(true);
            bool lastLine = novelIndex == novelLines.Count - 1;
            novelNext.GetComponentInChildren<TMP_Text>().text = !lastLine ? "NEXT" :
                castIndex + 1 < setup.cast.Length ? "NEXT GUEST" : "MENU";
            // Fit the whole portrait below the heading, without cropping faces on narrow screens.
            float w = Mathf.Max(1, novelArtFrame.rect.width);
            float h = Mathf.Max(1, novelArtFrame.rect.height - 50);
            float aspect = novelArt.texture == null ? 1 : (float)novelArt.texture.width / novelArt.texture.height;
            float width = Mathf.Min(w, h * aspect);
            RectTransform rect = novelArt.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(width, width / aspect);
            rect.anchoredPosition = new Vector2(0, -25);
            novelArt.uvRect = new Rect(0, 0, 1, 1);
        }

        void AdvanceCastCharacter()
        {
            if (novelIndex + 1 < novelLines.Count) { novelIndex++; DrawNovel(); }
            else if (castIndex + 1 < setup.cast.Length) ShowCastCharacter(castIndex + 1);
            else ShowMenu();
        }

        void BackCastCharacter()
        {
            if (novelIndex > 0) { novelIndex--; DrawNovel(); }
            else if (castIndex > 0)
            {
                ShowCastCharacter(castIndex - 1);
                novelIndex = novelLines.Count - 1;
                DrawNovel();
            }
        }
    }
}
