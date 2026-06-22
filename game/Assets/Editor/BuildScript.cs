using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// WebGL 빌드 자동화 (CLI -executeMethod BuildScript.BuildWebGL)
public class BuildScript
{
    public static void BuildWebGL()
    {
        // 빈 씬 생성/저장 (게임은 RuntimeInitialize로 자동 구성됨)
        Directory.CreateDirectory("Assets/Scenes");
        string scenePath = "Assets/Scenes/Main.unity";
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, scenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };

        // 어떤 호스팅에서도 로드되도록 압축 비활성화
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.productName = "Tower";

        string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../webgl-build"));
        if (Directory.Exists(outDir)) Directory.Delete(outDir, true);

        var opts = new BuildPlayerOptions
        {
            scenes = new[] { scenePath },
            locationPathName = outDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(opts);
        Debug.Log("BUILD_RESULT=" + report.summary.result + " SIZE=" + report.summary.totalSize);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }
}
