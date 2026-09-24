using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class WordIQMenuButtonRepair
{
    [MenuItem("Tools/WordIQ/Repair Menu Buttons")]
    public static void Repair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Exit Play Mode before repairing the menu.");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        Transform menu = null;
        TspMenuController controller = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                if (canvas.name == "MenuCanvas") menu = canvas.transform;
            if (controller == null) controller = root.GetComponentInChildren<TspMenuController>(true);
        }
        if (menu == null)
        {
            Debug.LogError("Open TspMenuScene first, then run Tools > WordIQ > Repair Menu Buttons.");
            return;
        }
        Button play = FindButton(menu, "PlayGameButton");
        Button help = FindButton(menu, "HowToPlayButton");
        Button close = FindButton(menu, "HowToPlayPanel/CloseHowToPlayButton");
        Transform panel = menu.Find("HowToPlayPanel");
        if (play == null || help == null || close == null || panel == null)
        {
            Debug.LogError("Expected menu buttons or HowToPlayPanel are missing. No changes made.");
            return;
        }
        Undo.SetCurrentGroupName("Repair WordIQ Menu Buttons");
        if (controller == null)
            controller = Undo.AddComponent<TspMenuController>(menu.gameObject);
        Undo.RecordObject(controller, "Connect help panel");
        var serialized = new SerializedObject(controller);
        serialized.FindProperty("howToPlayPanel").objectReferenceValue = panel.gameObject;
        serialized.ApplyModifiedProperties();
        Connect(play, controller.PlayGame);
        Connect(help, controller.ShowHowToPlay);
        Connect(close, controller.CloseHowToPlay);
        Undo.RecordObject(panel.gameObject, "Hide help panel");
        panel.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = play.gameObject;
        Debug.Log("WordIQ buttons repaired: PlayGame, ShowHowToPlay, CloseHowToPlay. Save the scene (Ctrl+S), then test in Play Mode.");
    }

    static Button FindButton(Transform menu, string path)
    {
        Transform child = menu.Find(path);
        return child == null ? null : child.GetComponent<Button>();
    }

    static void Connect(Button button, UnityAction action)
    {
        Undo.RecordObject(button, "Connect menu button");
        button.onClick = new Button.ButtonClickedEvent();
        UnityEventTools.AddPersistentListener(button.onClick, action);
        button.interactable = true;
        EditorUtility.SetDirty(button);
        if (button.targetGraphic != null)
        {
            Undo.RecordObject(button.targetGraphic, "Enable button clicks");
            button.targetGraphic.raycastTarget = true;
            EditorUtility.SetDirty(button.targetGraphic);
        }
    }
}
