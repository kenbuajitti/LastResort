#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Run once after importing this update, before creating the next player build.
public static class StoryIQEnsembleSplash
{
    [MenuItem("Tools/StoryIQ/Apply Ensemble Splash")]
    public static void Apply()
    {
        const string path = "Assets/Resources/StoryIQ/Ensemble.png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("StoryIQ ensemble artwork is missing: " + path);
            return;
        }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) { Debug.LogError("Could not load the StoryIQ ensemble sprite."); return; }
        PlayerSettings.SplashScreen.show = true;
        PlayerSettings.SplashScreen.showUnityLogo = false;
        PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(5f, sprite) };
        PlayerSettings.SplashScreen.backgroundColor = new Color(.025f, .04f, .10f);
        AssetDatabase.SaveAssets();
        Debug.Log("StoryIQ ensemble splash applied. Rebuild the player to include it.");
    }
}
#endif
