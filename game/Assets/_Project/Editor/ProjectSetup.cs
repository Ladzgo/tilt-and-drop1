using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TiltDrop.EditorTools
{
    /// <summary>Applies every project setting from code (nothing to click) and keeps level packs in sync.
    /// Runs automatically once per editor session; also available under the "Tilt & Drop" menu.</summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string BundleId = "com.laszlo.tiltdrop"; // placeholder - change before release
        public const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        public const string LevelsDst = "Assets/_Project/Resources/Levels";

        static ProjectSetup()
        {
            if (Application.isBatchMode) return; // CI calls BuildScript explicitly
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool("tiltdrop.setup", false)) return;
                SessionState.SetBool("tiltdrop.setup", true);
                SyncLevelPacks(false);
                EnsureScene();
                if (PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != BundleId) ApplySettings();
            };
        }

        static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        [MenuItem("Tilt & Drop/Apply Project Settings")]
        public static void ApplySettings()
        {
            PlayerSettings.companyName = "Laszlo";
            PlayerSettings.productName = "Tilt & Drop";
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1" || PlayerSettings.bundleVersion == "1.0")
                PlayerSettings.bundleVersion = "0.1.0";
            foreach (var t in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS })
            {
                PlayerSettings.SetApplicationIdentifier(t, BundleId);
                PlayerSettings.SetScriptingBackend(t, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetManagedStrippingLevel(t, ManagedStrippingLevel.Medium);
            }
            // portrait only
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.runInBackground = false;
            // Android
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto; // highest installed; must meet Play's current requirement
            EditorUserBuildSettings.buildAppBundle = true;
            // iOS
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly; // no iPad screenshots needed at launch
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.hideHomeButton = false;
            // splash (Unity 6 allows disabling it on all plans)
            try { PlayerSettings.SplashScreen.show = false; } catch { /* plan does not allow */ }
            SetLegacyInputEnabled();
            EnsureScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[Tilt & Drop] project settings applied");
        }

        /// <summary>The game uses the legacy Input API; make sure it is enabled ("Both" if the template chose the new system).</summary>
        static void SetLegacyInputEnabled()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var p = so.FindProperty("activeInputHandler");
            if (p != null && p.intValue == 1)
            {
                p.intValue = 2;
                so.ApplyModifiedProperties();
                Debug.LogWarning("[Tilt & Drop] Active Input Handling set to 'Both'. Restart the editor when prompted.");
            }
        }

        [MenuItem("Tilt & Drop/Sync Level Packs")]
        public static void SyncLevelPacksMenu() => SyncLevelPacks(true);

        /// <summary>Copies /shared/levels (*.tdp.gz, manifest.json) into Resources/Levels as TextAssets.</summary>
        public static bool SyncLevelPacks(bool verbose)
        {
            string src = Path.Combine(RepoRoot, "shared", "levels");
            if (!Directory.Exists(src)) { Debug.LogError($"[Tilt & Drop] {src} not found"); return false; }
            string dst = Path.GetFullPath(Path.Combine(Application.dataPath, "..", LevelsDst));
            Directory.CreateDirectory(dst);
            int copied = 0;
            foreach (var f in Directory.GetFiles(src))
            {
                string name = Path.GetFileName(f);
                string target;
                if (name == "manifest.json") target = Path.Combine(dst, "manifest.json");
                else if (name.EndsWith(".tdp.gz")) target = Path.Combine(dst, name.Substring(0, name.IndexOf('.')) + ".bytes");
                else continue;
                if (File.Exists(target) && new FileInfo(target).Length == new FileInfo(f).Length && File.ReadAllBytes(target).AsSpanEquals(File.ReadAllBytes(f))) continue;
                File.Copy(f, target, true);
                copied++;
            }
            if (copied > 0) AssetDatabase.Refresh();
            if (verbose || copied > 0) Debug.Log($"[Tilt & Drop] level packs synced ({copied} updated)");
            return true;
        }

        static bool AsSpanEquals(this byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>The single (empty) scene; GameRoot builds everything at runtime.</summary>
        public static void EnsureScene()
        {
            bool valid = File.Exists(ScenePath) && new FileInfo(ScenePath).Length > 100;
            if (!valid)
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            var list = EditorBuildSettings.scenes;
            if (list.Length != 1 || list[0].path != ScenePath || !list[0].enabled)
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
