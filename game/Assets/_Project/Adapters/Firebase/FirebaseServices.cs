// Firebase Analytics, Remote Config, Crashlytics. Compiled only with TD_FIREBASE.
// Written against the Firebase Unity SDK 11/12 API; re-check after upgrades.
using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Analytics;
using Firebase.Crashlytics;
using Firebase.Extensions;
using Firebase.RemoteConfig;
using TiltDrop.Core;
using UnityEngine;

namespace TiltDrop.Adapters
{
    static class FirebaseBoot
    {
        static bool started, ready;
        static readonly List<Action> Waiting = new List<Action>();

        public static void WhenReady(Action a)
        {
            if (ready) { a(); return; }
            Waiting.Add(a);
            if (started) return;
            started = true;
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(t =>
            {
                if (t.Result != DependencyStatus.Available) { Debug.LogError("Firebase unavailable: " + t.Result); return; }
                ready = true;
                foreach (var w in Waiting) w();
                Waiting.Clear();
            });
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            ServiceRegistry.Register<IAnalytics>(() => new FirebaseAnalyticsService());
            ServiceRegistry.Register<IRemoteConfig>(() => new FirebaseRemoteConfigService());
            ServiceRegistry.Register<ICrashReporter>(() => new CrashlyticsService());
        }
    }

    public sealed class FirebaseAnalyticsService : IAnalytics
    {
        bool enabled;

        // Collection is disabled by default in the manifests (docs/SETUP.md) until consent allows it.
        public void SetEnabled(bool e)
        {
            enabled = e;
            FirebaseBoot.WhenReady(() =>
            {
                var status = e ? ConsentStatus.Granted : ConsentStatus.Denied;
                FirebaseAnalytics.SetConsent(new Dictionary<ConsentType, ConsentStatus>
                {
                    { ConsentType.AnalyticsStorage, status },
                    { ConsentType.AdStorage, status },
                    { ConsentType.AdUserData, status },
                    { ConsentType.AdPersonalization, status },
                });
                FirebaseAnalytics.SetAnalyticsCollectionEnabled(e);
            });
        }

        public void Log(string name, IDictionary<string, object> p = null)
        {
            if (!enabled) return;
            FirebaseBoot.WhenReady(() =>
            {
                if (p == null) { FirebaseAnalytics.LogEvent(name); return; }
                var list = new List<Parameter>();
                foreach (var kv in p)
                {
                    switch (kv.Value)
                    {
                        case int i: list.Add(new Parameter(kv.Key, i)); break;
                        case long l: list.Add(new Parameter(kv.Key, l)); break;
                        case float f: list.Add(new Parameter(kv.Key, f)); break;
                        case double d: list.Add(new Parameter(kv.Key, d)); break;
                        case bool b: list.Add(new Parameter(kv.Key, b ? 1L : 0L)); break;
                        default: list.Add(new Parameter(kv.Key, kv.Value?.ToString() ?? "")); break;
                    }
                }
                FirebaseAnalytics.LogEvent(name, list.ToArray());
            });
        }
    }

    public sealed class FirebaseRemoteConfigService : IRemoteConfig
    {
        public void Fetch(IDictionary<string, string> defaults, Action<IDictionary<string, string>> done)
        {
            FirebaseBoot.WhenReady(() =>
            {
                var rc = FirebaseRemoteConfig.DefaultInstance;
                var defs = new Dictionary<string, object>();
                foreach (var kv in defaults) defs[kv.Key] = kv.Value;
                rc.SetDefaultsAsync(defs).ContinueWithOnMainThread(_ =>
                    rc.FetchAndActivateAsync().ContinueWithOnMainThread(t =>
                    {
                        var values = new Dictionary<string, string>();
                        foreach (var kv in rc.AllValues) values[kv.Key] = kv.Value.StringValue;
                        done(values);
                    }));
            });
        }
    }

    public sealed class CrashlyticsService : ICrashReporter
    {
        public void SetEnabled(bool e) => FirebaseBoot.WhenReady(() => Crashlytics.IsCrashlyticsCollectionEnabled = e);
        public void Log(string m) => FirebaseBoot.WhenReady(() => Crashlytics.Log(m));
        public void Record(Exception e) => FirebaseBoot.WhenReady(() => Crashlytics.LogException(e));
    }
}
