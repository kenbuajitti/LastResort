using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Also protects ordinary File > Build Profiles Web builds.
public sealed class StoryIQWebBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => 1000;
    const string Template = "PROJECT:IQGamesResponsive";
    static string[] Scenes => new[] { "Assets/Scenes/LastResortMenu.unity" };

    static void Configure()
    {
        if (!File.Exists("Assets/WebGLTemplates/IQGamesResponsive/index.html"))
            throw new BuildFailedException("StoryIQ responsive Web template is missing.");
        PlayerSettings.productName = "StoryIQ";
        PlayerSettings.WebGL.template = Template;
        PlayerSettings.defaultWebScreenWidth = 960;
        PlayerSettings.defaultWebScreenHeight = 600;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
    }

    [MenuItem("IQ Games/Build StoryIQ for itch.io")]
    public static void Build()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            throw new BuildFailedException("Install Web Build Support for this Unity version using Unity Hub.");
        if (Scenes.Length == 0) throw new BuildFailedException("Enable your menu and game scenes in File > Build Profiles > Scene List first.");
        foreach (string scene in Scenes)
            if (!File.Exists(scene)) throw new BuildFailedException("Missing scene: " + scene);
        Configure();
        AssetDatabase.SaveAssets();
        // Fresh directory prevents an older build's files being bundled in the new ZIP.
        string output = Path.GetFullPath(Path.Combine("Builds", "StoryIQ-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")));
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = Scenes,
            locationPathName = output,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException("StoryIQ Web build failed. See the first error in the Console.");
        if (!Application.isBatchMode) EditorUtility.RevealInFinder(output + ".zip");
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL) return;
        Configure();
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL) return;
        string output = Path.GetFullPath(report.summary.outputPath);
        string index = Path.Combine(output, "index.html");
        if (!File.Exists(index) || !File.ReadAllText(index).Contains("iqgames-responsive-v3"))
            throw new BuildFailedException("The active Build Profile overrode the StoryIQ template. Select IQGamesResponsive in its Player Settings and rebuild.");
        string build = Path.Combine(output, "Build");
        if (!Directory.Exists(build) || !Directory.GetFiles(build, "*.wasm*").Any())
            throw new BuildFailedException("Web build is missing WebAssembly output; no upload ZIP was created.");
        string zip = output.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".zip";
        string temp = zip + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                foreach (string file in Directory.GetFiles(output, "*", SearchOption.AllDirectories))
                {
                    string relative = file.Substring(output.TrimEnd(Path.DirectorySeparatorChar).Length + 1).Replace('\\', '/');
                    if (relative.Split('/').Any(part => part.Contains("DoNotShip"))) continue;
                    var entry = archive.CreateEntry(relative, System.IO.Compression.CompressionLevel.Optimal);
                    using (var input = File.OpenRead(file))
                    using (var target = entry.Open()) input.CopyTo(target);
                }
            }
            if (File.Exists(zip)) File.Delete(zip);
            File.Move(temp, zip);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        Debug.Log("StoryIQ itch.io upload ready: " + zip);
    }
}
