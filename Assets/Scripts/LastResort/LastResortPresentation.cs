using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace LastResort
{
    public sealed partial class LastResortController
    {
        RectTransform menuRoot, menuStack, menuCover, helpRoot, helpCard, introRoot, introCover;
        Button resumeButton;
        IQMusic music;
        IQSpeakerGraphic speaker;
        Texture2D coverTexture;
        TMP_Text menuDescription;
        readonly System.Collections.Generic.List<Button> menuButtons = new System.Collections.Generic.List<Button>();
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void StoryIQOpenGames();
#endif

        void BuildSeriesMenu()
        {
            coverTexture = Resources.Load<Texture2D>("StoryIQ/Ensemble");
            music = IQMusic.GetPlayer();
            menuRoot = Panel("StoryIQ Menu", safeArea, new Color(.025f, .04f, .10f));
            Stretch(menuRoot);
            menuCover = CreateCover(menuRoot);
            menuStack = Panel("Menu buttons", menuRoot, new Color(.015f, .025f, .07f, .78f));
            menuDescription = Label(menuStack,
                "A holiday to die for.\nFive days. Twenty decisions. Your story.", 25, Color.white, false);
            menuDescription.alignment = TextAlignmentOptions.Center;
            menuDescription.enableAutoSizing = true;
            menuDescription.fontSizeMin = 18;
            menuDescription.fontSizeMax = 25;
            resumeButton = MenuButton("CONTINUE YOUR STAY", () => {
                if (pendingRequest != null) RetryLive(); else ShowLiveStory();
            });
            MenuButton("PLAY", BeginLive);
            MenuButton("HOW TO PLAY", ShowHelp);
            MenuButton("MEET THE CHARACTERS", ShowCast);
            MenuButton("OFFLINE PREVIEW", BeginStay);
            MenuButton("ALL IQ GAMES", OpenAllGames);

            // This control remains available on both menu and reading screens.
            RectTransform soundRect = Panel("Sound off - click to enable", safeArea, Color.clear);
            soundRect.anchorMin = soundRect.anchorMax = soundRect.pivot = new Vector2(1, 0);
            soundRect.anchoredPosition = new Vector2(-10, 10);
            soundRect.sizeDelta = new Vector2(52, 48);
            var soundButton = soundRect.gameObject.AddComponent<Button>();
            soundButton.targetGraphic = soundRect.GetComponent<Image>();
            soundButton.transition = Selectable.Transition.None;
            RectTransform icon = Rect("Speaker", soundRect);
            Stretch(icon);
            speaker = icon.gameObject.AddComponent<IQSpeakerGraphic>();
            speaker.color = Color.white;
            speaker.raycastTarget = false;
            soundButton.onClick.AddListener(music.ToggleSound);

            helpRoot = Panel("How to Play overlay", safeArea, new Color(0, 0, 0, .66f));
            Stretch(helpRoot);
            helpCard = Panel("Instructions", helpRoot, new Color(.035f, .055f, .11f, .98f));
            var heading = Label(helpCard, "HOW TO PLAY", 30, Color.white, false);
            heading.alignment = TextAlignmentOptions.Center;
            SetBounds(heading.rectTransform, new Vector2(0, 1), Vector2.one,
                new Vector2(20, -66), new Vector2(-20, -20));
            var body = Label(helpCard,
                "Click the text or NEXT to read one line at a time. BACK rereads earlier lines. At the end of each scene, choose an action. There is no timer.\n\n" +
                "The illustration changes with the mood of the text. Your holiday spans five days and twenty decisions. Your choices shape the next scene and the final epilogue.\n\n" +
                "PLAY starts a live story. OFFLINE PREVIEW offers one written sample decision. MEET THE CHARACTERS introduces the guests.\n\n" +
                "Use MENU, then CONTINUE YOUR STAY to resume. Keep the game tab open to retain your stay. If a connection fails, RETRY CONNECTION resumes the same request.",
                24, Color.white, false);
            body.enableAutoSizing = true;
            body.fontSizeMin = 16;
            body.fontSizeMax = 24;
            SetBounds(body.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(28, 92), new Vector2(-28, -82));
            Button close = FlatButton(helpCard, "CLOSE", () => helpRoot.gameObject.SetActive(false));
            var closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(.5f, 0);
            closeRect.pivot = new Vector2(.5f, 0);
            closeRect.anchoredPosition = new Vector2(0, 24);
            closeRect.sizeDelta = new Vector2(220, 44);
            helpRoot.gameObject.SetActive(false);
            LayoutSeriesMenu();
        }

        RectTransform CreateCover(Transform parent)
        {
            RectTransform rect = Rect("Cover", parent);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = coverTexture;
            image.raycastTarget = false;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            return rect;
        }

        Button FlatButton(Transform parent, string caption, UnityAction action)
        {
            RectTransform rect = Panel(caption, parent, Color.white);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            var colors = button.colors;
            colors.highlightedColor = new Color(.87f, .91f, 1);
            colors.pressedColor = new Color(.70f, .78f, .9f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.onClick.AddListener(action);
            var text = Label(rect, caption, 22, Color.black, false);
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(10, 3);
            text.rectTransform.offsetMax = new Vector2(-10, -3);
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = 16;
            text.fontSizeMax = 22;
            return button;
        }

        Button MenuButton(string caption, UnityAction action)
        {
            Button button = FlatButton(menuStack, caption, action);
            menuButtons.Add(button);
            return button;
        }

        void LayoutSeriesMenu()
        {
            if (menuRoot == null) return;
            float w = safeArea.rect.width, h = safeArea.rect.height;
            bool portrait = h > w;
            FitCover(menuCover, portrait ? w : w * .54f, portrait ? h * .42f : h * .86f, false);
            menuCover.anchoredPosition = portrait ? new Vector2(0, h * .27f) : new Vector2(-w * .22f, 0);
            if (introCover != null) FitCover(introCover, w, h, false);
            int count = 0;
            foreach (var button in menuButtons) if (button.gameObject.activeSelf) count++;
            float bh = Mathf.Clamp((h * .66f - 102) / Mathf.Max(1, count), 36, 46);
            float height = 94 + count * (bh + 8);
            float width = Mathf.Min(430, portrait ? w - 36 : w * .44f);
            menuStack.anchorMin = menuStack.anchorMax = new Vector2(.5f, 0);
            menuStack.pivot = new Vector2(.5f, 0);
            menuStack.anchoredPosition = new Vector2(portrait ? 0 : w * .27f, portrait ? 30 : Mathf.Max(18, (h - height) * .5f));
            menuStack.sizeDelta = new Vector2(width, height);
            SetBounds(menuDescription.rectTransform, new Vector2(0, 1), Vector2.one,
                new Vector2(16, -82), new Vector2(-16, -12));
            int row = 0;
            foreach (var button in menuButtons)
            {
                if (!button.gameObject.activeSelf) continue;
                var rect = button.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
                rect.pivot = new Vector2(.5f, 1);
                rect.sizeDelta = new Vector2(width - 40, bh);
                rect.anchoredPosition = new Vector2(0, -86 - row++ * (bh + 8));
            }
            helpCard.anchorMin = helpCard.anchorMax = helpCard.pivot = new Vector2(.5f, .5f);
            helpCard.anchoredPosition = Vector2.zero;
            helpCard.sizeDelta = new Vector2(Mathf.Min(790, w - 40), Mathf.Min(570, h - 48));
        }

        void FitCover(RectTransform rect, float w, float h, bool top)
        {
            var texture = rect.GetComponent<RawImage>().texture;
            if (texture == null) return;
            float aspect = (float)texture.width / texture.height;
            float width = Mathf.Min(w, h * aspect);
            rect.sizeDelta = new Vector2(width, width / aspect);
            rect.anchoredPosition = new Vector2(0, top ? (h - width / aspect) * .5f : 0);
        }

        IEnumerator ShowIntroduction()
        {
            introRoot = Panel("StoryIQ five-second introduction", safeArea, new Color(.025f, .04f, .10f));
            Stretch(introRoot);
            introCover = CreateCover(introRoot);
            var ensemble = Resources.Load<Texture2D>("StoryIQ/Ensemble");
            if (ensemble != null) introCover.GetComponent<RawImage>().texture = ensemble;
            LayoutSeriesMenu();
            float elapsed = 0;
            while (elapsed < 5f)
            {
                introCover.localScale = Vector3.one * Mathf.Lerp(.94f, 1f, elapsed / 5f);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Destroy(introRoot.gameObject);
            introRoot = introCover = null;
        }

        void LateUpdate()
        {
            if (speaker == null || music == null) return;
            bool audible = music.IsAudible;
            if (speaker.IsOn != audible) { speaker.IsOn = audible; speaker.SetVerticesDirty(); }
            speaker.transform.parent.name = audible ? "Sound on - click to mute" : "Sound off - click to enable";
        }

        void OpenAllGames()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            StoryIQOpenGames();
#else
            Application.OpenURL("https://iqgamesonline.com/?iqreturn=1&game=StoryIQ");
#endif
        }
    }
}
