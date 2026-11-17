#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace TiltDrop.EditorTools
{
    /// <summary>Info.plist keys required for review and for consent-gated SDK collection (COMPLIANCE §2).</summary>
    public static class IosPostProcess
    {
        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;
            root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            if (!root.values.ContainsKey("NSUserTrackingUsageDescription"))
                root.SetString("NSUserTrackingUsageDescription", "Your data will be used to show you more relevant ads.");
#if TD_FIREBASE
            // collection starts only after the in-game consent/age decision enables it
            root.SetBoolean("FIREBASE_ANALYTICS_COLLECTION_ENABLED", false);
            root.SetBoolean("FirebaseCrashlyticsCollectionEnabled", false);
            root.SetBoolean("GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_PERSONALIZATION_SIGNALS", false);
#endif
            plist.WriteToFile(plistPath);
        }
    }
}
#endif
