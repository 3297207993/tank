using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// CI（GitHub Actions）打包入口。
/// 用法：Unity.exe -batchmode -nographics -quit -projectPath &lt;工程&gt;
///       -executeMethod BuildScript.BuildWindows -releaseVersion 1.0.0 -buildOutput &lt;目录&gt;
/// 成功时由 -quit 退出（退出码 0），失败时退出码 1。
/// </summary>
public static class BuildScript
{
    /// <summary>
    /// 构建 Windows 64 位版本。
    /// 场景取自 Build Settings 中已启用的项，顺序保持不变。
    /// </summary>
    public static void BuildWindows()
    {
        try
        {
            string version = GetCommandLineArg("-releaseVersion");
            if (!string.IsNullOrEmpty(version))
            {
                PlayerSettings.bundleVersion = version;
                Debug.Log($"[Build] bundleVersion = {version}");
            }

            string outputDir = GetCommandLineArg("-buildOutput");
            if (string.IsNullOrEmpty(outputDir))
            {
                // Application.dataPath 在编辑器里是 <工程>/Assets，比当前工作目录可靠。
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                outputDir = Path.Combine(projectRoot, "Build", "Windows");
            }
            outputDir = Path.GetFullPath(outputDir);
            if (Directory.Exists(outputDir))
            {
                // 避免上一次构建的残留文件被打进压缩包。
                Directory.Delete(outputDir, true);
            }
            Directory.CreateDirectory(outputDir);

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("Build Settings 里没有启用的场景");
            }

            // 和本地手动构建保持一致：Mono 后端，Windows x64。
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            string exePath = Path.Combine(outputDir, "tank.exe");
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            Debug.Log($"[Build] Unity {Application.unityVersion}，输出 {exePath}，场景：{string.Join(", ", scenes)}");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"[Build] 结果 {summary.result}，大小 {summary.totalSize} 字节，耗时 {summary.totalTime}，错误 {summary.totalErrors}");
            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"构建失败：{summary.result}，错误数 {summary.totalErrors}");
            }

            Debug.Log("[Build] 完成：" + summary.outputPath);
        }
        catch (Exception exception)
        {
            Debug.LogError("[Build] 构建异常：" + exception);
            EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// 读取命令行参数，例如 -releaseVersion 1.0.0 返回 "1.0.0"。
    /// </summary>
    static string GetCommandLineArg(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }
        return null;
    }
}
