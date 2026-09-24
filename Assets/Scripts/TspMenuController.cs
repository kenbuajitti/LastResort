using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TspMenuController : MonoBehaviour
{
    [SerializeField] private GameObject howToPlayPanel;

    void Awake()
    {
        Transform menuCanvas = null;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                if (canvas.name == "MenuCanvas") menuCanvas = canvas.transform;
        if (menuCanvas != null)
        {
            Transform panel = menuCanvas.Find("HowToPlayPanel");
            if (panel != null) howToPlayPanel = panel.gameObject;
            Bind(menuCanvas.Find("PlayGameButton"), PlayGame);
            Bind(menuCanvas.Find("HowToPlayButton"), ShowHowToPlay);
            Bind(menuCanvas.Find("HowToPlayPanel/CloseHowToPlayButton"), CloseHowToPlay);
            if (howToPlayPanel != null) howToPlayPanel.SetActive(false);
        }

        // Decorative UI uses the menu font and never intercepts button clicks.
        if (howToPlayPanel == null) return;
        Transform menu = howToPlayPanel.transform.parent;
        Transform background = menu.Find("MenuBackground");
        Transform title = menu.Find("GameTitleText");
        if (background == null || title == null) return;
        TMP_Text titleText = title.GetComponent<TMP_Text>();
        if (titleText == null) return;

        const string letters = "WORDGAME";
        for (int i = 0; i < letters.Length; i++)
        {
            var tile = new GameObject("LetterTile" + i, typeof(RectTransform), typeof(Image));
            tile.transform.SetParent(background, false);
            var rect = tile.GetComponent<RectTransform>();
            Vector2 anchor = new Vector2(i < 4 ? .09f : .91f, .16f + (i % 4) * .22f);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = new Vector2(66, 66);
            rect.localRotation = Quaternion.Euler(0, 0, i % 2 == 0 ? -12 : 12);
            Image tileImage = tile.GetComponent<Image>();
            tileImage.color = new Color(.3f, .65f, .72f, .07f);
            tileImage.raycastTarget = false;

            var label = new GameObject("Letter", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(tile.transform, false);
            var text = label.GetComponent<TextMeshProUGUI>();
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.font = titleText.font;
            text.text = letters[i].ToString();
            text.fontSize = 40;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(.5f, .85f, .9f, .16f);
            text.raycastTarget = false;
        }
    }

    static void Bind(Transform target, UnityEngine.Events.UnityAction action)
    {
        if (target == null) return;
        Button button = target.GetComponent<Button>();
        if (button == null) return;
        // Replace stale Inspector calls as well as runtime listeners.
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(action);
        button.interactable = true;
        if (button.targetGraphic != null) button.targetGraphic.raycastTarget = true;
    }

    public void PlayGame()
    {
        SceneManager.LoadScene("TspGameScene");
    }

    public void ShowHowToPlay()
    {
        if (howToPlayPanel != null)
            howToPlayPanel.SetActive(true);
    }

    public void CloseHowToPlay()
    {
        if (howToPlayPanel != null)
            howToPlayPanel.SetActive(false);
    }
}