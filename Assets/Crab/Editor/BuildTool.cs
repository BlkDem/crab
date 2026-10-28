// Player builds: Windows (64-bit, Mono) and WebGL. Crab → Build → …, or from the command line:
// -executeMethod Crab.EditorTools.BuildTool.Windows (or .WebGL). The scenes are those of the build
// settings (SceneBuilder.BuildAll sets them); the output goes to Builds/<platform>.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Crab.EditorTools
{
    public static class BuildTool
    {
        public const string Version = "1.0.0";

        [MenuItem("Crab/Build/Windows")]
        public static void Windows()
        {
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.visibleInBackground = true;
            Build(BuildTarget.StandaloneWindows64, "Builds/Windows/CrabVsHell.exe", BuildOptions.CompressWithLz4HC);
        }

        [MenuItem("Crab/Build/WebGL")]
        public static void WebGL()
        {
            // plain hosting: gzip with a fallback, so no special server headers are needed
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
            PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
            PlayerSettings.WebGL.maximumMemorySize = 4096;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.WebGL.template = "PROJECT:Crab";   // Assets/WebGLTemplates/Crab: full window, a title card while loading
            PlayerSettings.runInBackground = false;
            Build(BuildTarget.WebGL, "Builds/WebGL", BuildOptions.None);
        }

        static void Build(BuildTarget target, string path, BuildOptions options)
        {
            // URP strips its resources by the quality levels of the active target, not of the build's:
            // a Windows build made while WebGL is active (no PC level) loses SSAO and never starts
            if (EditorUserBuildSettings.activeBuildTarget != target)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(target), target);
            ModelUnpack.All();
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.enableFrameTimingStats = true;   // the benchmark reads the cpu and gpu times
            PlayerSettings.productName = "Краб против ада";
            PlayerSettings.companyName = "umolab";
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) throw new Exception("no scenes in the build settings: run SceneBuilder.BuildAll first");
            var dir = target == BuildTarget.WebGL ? path : Path.GetDirectoryName(path);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            var started = DateTime.Now;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = target,
                options = options,
            });
            var s = report.summary;
            long bytes = Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
            Debug.Log($"BUILD {target}: {s.result}, {bytes / 1048576.0:0.0} MB on disk, {s.totalErrors} errors, {(DateTime.Now - started).TotalMinutes:0.0} min → {path}");
            if (s.result != BuildResult.Succeeded)
            {
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            // the heaviest assets, to know what to trim
            foreach (var a in report.packedAssets.SelectMany(p => p.contents).GroupBy(c => c.sourceAssetPath)
                .Select(g => (path: g.Key, size: g.Sum(c => (long)c.packedSize))).OrderByDescending(x => x.size).Take(15))
                Debug.Log($"BUILD heavy: {a.size / 1048576.0:0.0} MB {a.path}");
        }
    }
}
