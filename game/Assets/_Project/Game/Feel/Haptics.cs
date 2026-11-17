using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace TiltDrop.Game
{
    public enum Haptic { Light, Medium, Heavy, Success, Failure }

    /// <summary>Haptics hook: iOS UIFeedbackGenerator via Plugins/iOS/TiltDropNative.mm, Android VibrationEffect.</summary>
    public static class Haptics
    {
        public static bool Enabled = true;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void TD_Haptic(int style);
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject vibrator;
        static int sdk;
#endif

        public static void Play(Haptic h)
        {
            if (!Enabled) return;
#if UNITY_IOS && !UNITY_EDITOR
            TD_Haptic((int)h);
#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (vibrator == null)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    using (var ver = new AndroidJavaClass("android.os.Build$VERSION")) sdk = ver.GetStatic<int>("SDK_INT");
                }
                long ms = h == Haptic.Light ? 12 : h == Haptic.Medium ? 22 : h == Haptic.Heavy ? 40 : 30;
                int amp = h == Haptic.Light ? 60 : h == Haptic.Medium ? 140 : 255;
                if (sdk >= 26)
                {
                    using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", ms, amp))
                        vibrator.Call("vibrate", effect);
                }
                else vibrator.Call("vibrate", ms);
            }
            catch (System.Exception) { /* no vibrator */ }
#endif
        }

        // Referencing Handheld.Vibrate makes Unity add the VIBRATE permission on Android.
        public static void Fallback() { if (Enabled) Handheld.Vibrate(); }
    }
}
