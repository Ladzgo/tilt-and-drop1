using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TiltDrop.EditorTools
{
    /// <summary>CI entry point (GameCI `buildMethod: TiltDrop.EditorTools.BuildScript.Build`).
    /// Reads -buildTarget, -customBuildPath, -androidKeystore* args that GameCI passes.</summary>
    public static class BuildScript
    {
        public static void Build()
        {
            var args = Environment.GetCommandLineArgs();
            string Arg(string name, string def = null)
            {
                int i = Array.IndexOf(args, "-" + name);
                return i >= 0 && i + 1 < args.Length ? args[i + 1] : def;
            }

            ProjectSetup.SyncLevelPacks(true);
            ProjectSetup.ApplySettings();

            var target = (BuildTarget)Enum.Parse(typeof(BuildTarget), Arg("buildTarget", EditorUserBuildSettings.activeBuildTarget.ToString()));
            string path = Arg("customBuildPath", target == BuildTarget.Android ? "build/Android/tiltdrop.aab" : "build/iOS");
            string version = Arg("buildVersion");
            if (!string.IsNullOrEmpty(version) && version != "none") PlayerSettings.bundleVersion = version;
            string code = Arg("androidVersionCode");
            if (int.TryParse(code, out int vc)) PlayerSettings.Android.bundleVersionCode = vc;

            if (target == BuildTarget.Android)
            {
                EditorUserBuildSettings.buildAppBundle = path.EndsWith(".aab");
                string ks = Arg("androidKeystoreName");
                if (!string.IsNullOrEmpty(ks))
                {
                    PlayerSettings.Android.useCustomKeystore = true;
                    PlayerSettings.Android.keystoreName = ks;
                    PlayerSettings.Android.keystorePass = Arg("androidKeystorePass");
                    PlayerSettings.Android.keyaliasName = Arg("androidKeyaliasName");
                    PlayerSettings.Android.keyaliasPass = Arg("androidKeyaliasPass");
                }
            }

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[Tilt & Drop] build {report.summary.result}: {report.summary.totalSize} bytes, {report.summary.totalErrors} errors");
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
