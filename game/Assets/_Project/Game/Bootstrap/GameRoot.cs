using System;
using System.Collections.Generic;
using TiltDrop.Core;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace TiltDrop.Game
{
    /// <summary>Composition root and app flow. Created automatically after the first scene loads,
    /// so the only scene needs nothing in it.</summary>
    public sealed class GameRoot : MonoBehaviour
    {
        public const string PrivacyPolicyUrl = "https://tiltdrop.app/privacy"; // TODO: your hosted policy

        public static GameRoot I { get; private set; }

        public IClock Clock { get; } = new SystemClock();
        public GameConfig Config { get; } = new GameConfig();
        public SaveData Save { get; private set; }
        public LevelCatalog Catalog { get; private set; }
        public Wallet Wallet { get; private set; }
        public AdPacingPolicy Pacing { get; private set; }
        public PrivacyDecision Privacy { get; private set; }
        public IAdsService Ads { get; private set; }
        public IIapService Iap { get; private set; }
        public IAnalytics Analytics { get; private set; }
        public IRemoteConfig Remote { get; private set; }
        public ICrashReporter Crash { get; private set; }

        public Camera Cam { get; private set; }
        public BoardView Board { get; private set; }
        public BoardInput Input { get; private set; }
        public Sfx Sfx { get; private set; }
        public Particles Particles { get; private set; }
        public RectTransform ScreenLayer { get; private set; }
        public RectTransform PopupLayer { get; private set; }
        public string LastDailyShare;

        SaveStore store;
        Canvas canvas;
        GameObject world;
        LevelController level;
        MapScreen map;
        bool dirty;
        float lastSave;
        Action lastStart;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("GameRoot");
            DontDestroyOnLoad(go);
            go.AddComponent<GameRoot>();
        }

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            Input = gameObject.AddComponent<BoardInput>();
            store = new SaveStore();
            Save = store.Load(Clock);
            Wallet = new Wallet(Save.Coins);
            Wallet.Changed += (balance, _) => { Save.Coins = balance; Dirty(); };
            Pacing = new AdPacingPolicy(Config, Save.Ads);
            Pacing.OnSessionStart();

            Ads = new AdsRouter(ServiceRegistry.Resolve<IAdsService>(() => new StubAds()), ServiceRegistry.Resolve<IChildSafeAds>(() => null));
            Iap = ServiceRegistry.Resolve<IIapService>(() => new StubIap());
            Analytics = ServiceRegistry.Resolve<IAnalytics>(() => new StubAnalytics());
            Remote = ServiceRegistry.Resolve<IRemoteConfig>(() => new StubRemoteConfig());
            Crash = ServiceRegistry.Resolve<ICrashReporter>(() => new StubCrash());
            Iap.PurchaseGranted += GrantPurchase;
            Application.logMessageReceived += (msg, trace, type) => { if (type == LogType.Exception) Crash.Log(msg + "\n" + trace); };

            SetupWorld();
            SetupUI();
            Catalog = LevelLoader.Load();
            ApplySettings();
            UIKit.OnAnyClick = () => { Sfx.Play(Sound.Tap); Haptics.Play(Haptic.Light); };
        }

        void Start()
        {
            if (Catalog == null) { Popups.Info("Missing levels", "Level packs were not found. In the Unity editor run: Tilt & Drop > Sync Level Packs."); return; }
            if (Save.AgeBand == AgeBand.Unknown)
                new AgeGateScreen(PopupLayer, Clock.LocalNow, band => { Save.AgeBand = band; Dirty(); SaveNow(); ContinueBoot(); });
            else
                ContinueBoot();
        }

        void ContinueBoot()
        {
            Privacy = Decide();
            Analytics.SetEnabled(false);
            Crash.SetEnabled(Privacy.Crashlytics);
            Ads.Initialize(Privacy, consent =>
            {
                if (Save.AgeBand == AgeBand.Adult)
                {
                    Save.ConsentPersonalized = consent.Personalized;
                    Save.ConsentAnalytics = consent.Analytics;
                }
                RefreshPrivacy();
                Analytics.Log(AnalyticsEvents.Consent, new Dictionary<string, object> { ["result"] = consent.Personalized ? "personalized" : "limited", ["age_band"] = Save.AgeBand.ToString() });
                if (Privacy.Purchases) Iap.Initialize(StoreCatalog.Products, ok => { if (ok) SyncOwnedProducts(); });
                Remote.Fetch(Config.Defaults(), values => Config.Apply(values));
                GoHome();
            });
        }

        PrivacyDecision Decide() => PrivacyRules.Decide(Save.AgeBand, Application.platform == RuntimePlatform.IPhonePlayer, Save.ConsentPersonalized, Save.ConsentAnalytics, Save.CrashlyticsOptOut);

        public void RefreshPrivacy()
        {
            Privacy = Decide();
            Analytics.SetEnabled(Privacy.Analytics);
            Crash.SetEnabled(Privacy.Crashlytics);
            Dirty();
        }

        void GoHome()
        {
            if (Save.Progress.HighestCompleted == 0) StartLevel(1); // FTUE: straight into level 1
            else ShowMap();
        }

        // ---- world / UI setup ------------------------------------------------------------------
        void SetupWorld()
        {
            world = new GameObject("World");
            DontDestroyOnLoad(world);
            Cam = Camera.main;
            if (Cam == null)
            {
                var cgo = new GameObject("Main Camera");
                cgo.tag = "MainCamera";
                Cam = cgo.AddComponent<Camera>();
                cgo.AddComponent<AudioListener>();
                DontDestroyOnLoad(cgo);
            }
            Cam.orthographic = true;
            Cam.orthographicSize = 8f;
            Cam.transform.position = new Vector3(0, 0, -10);
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = Palette.Background;
            Sfx = world.AddComponent<Sfx>();
            Particles = new GameObject("Particles").AddComponent<Particles>();
            Particles.transform.SetParent(world.transform, false);
            var bgo = new GameObject("Board");
            bgo.transform.SetParent(world.transform, false);
            Board = bgo.AddComponent<BoardView>();
            Board.Particles = Particles;
            Board.Sfx = Sfx;
            bgo.SetActive(false);
        }

        void LayoutBoard()
        {
            float h = Cam.orthographicSize * 2f, w = h * Cam.aspect;
            // HUD takes ~16% at the top and ~21% at the bottom of the screen
            float top = Cam.orthographicSize - h * 0.16f, bottom = -Cam.orthographicSize + h * 0.21f;
            float size = Mathf.Min(w * 0.96f, (top - bottom) * 0.98f);
            Board.Layout(new Vector3(0, (top + bottom) * 0.5f, 0), size);
        }

        void SetupUI()
        {
            canvas = UIKit.CreateCanvas("UI", 10);
            var safe = UIKit.Rect("safe", canvas.transform).Stretch();
            safe.gameObject.AddComponent<SafeArea>();
            ScreenLayer = UIKit.Rect("screens", safe).Stretch();
            PopupLayer = UIKit.Rect("popups", safe).Stretch();
        }

        public void ApplySettings()
        {
            var s = Save.Settings;
            Sfx.Enabled = s.Sound;
            Haptics.Enabled = s.Haptics;
            Board.ReducedMotion = s.ReducedMotion;
            Particles.Reduced = s.ReducedMotion;
            Board.Speed = s.Speed == 1 ? 1f : s.Speed == 2 ? 1.5f : 2f;
        }

        // ---- navigation ---------------------------------------------------------------------------
        void ClearScreens()
        {
            level?.Dispose();
            level = null;
            map?.Destroy();
            map = null;
            for (int i = PopupLayer.childCount - 1; i >= 0; i--) Destroy(PopupLayer.GetChild(i).gameObject);
            Tweener.Kill(Board);
        }

        public void ShowMap()
        {
            ClearScreens();
            Board.gameObject.SetActive(false);
            Input.Active = false;
            map = new MapScreen(this, ScreenLayer);
            int today = Days.Of(Clock.LocalNow);
            if (Save.Calendar.CanClaim(today) && Save.Progress.HighestCompleted >= 3) Popups.Calendar(this);
        }

        public void RefreshMap() { if (map != null) ShowMap(); }

        public void StartLevel(int n)
        {
            n = Mathf.Clamp(n, 1, Catalog.LevelCount);
            if (!Save.Progress.IsUnlocked(n)) n = Save.Progress.NextLevel;
            lastStart = () => StartLevel(n);
            Begin(new LevelController(this, Catalog.Get(n, Config), n, false, false));
        }

        public void StartDaily(bool practice)
        {
            int today = Days.Of(Clock.LocalNow);
            int number = DailyPuzzle.Number(Catalog.DailyEpochDay, today);
            int index = DailyPuzzle.LevelIndex(number, Catalog.DailyCount);
            try
            {
                if (MiniJson.Parse(Config.DailyOverride) is Dictionary<string, object> o && o.TryGetValue(Days.ToDate(today).ToString("yyyy-MM-dd"), out var v))
                    index = Convert.ToInt32(v) % Catalog.DailyCount;
            }
            catch (FormatException) { }
            lastStart = () => StartDaily(true);
            Begin(new LevelController(this, Catalog.Daily(index), number, true, practice));
        }

        void Begin(LevelController lc)
        {
            ClearScreens();
            LayoutBoard();
            level = lc;
            level.Start();
        }

        public void Restart() => lastStart?.Invoke();

        /// <summary>After a win: maybe an interstitial (GDD §6.2), then continue.</summary>
        public void AfterWin(int levelNumber, Action then)
        {
            if (Pacing.ShouldShowInterstitial(AdMoment.AfterWin, levelNumber, Save.RemoveAds, Clock.UtcNow) && Ads.IsInterstitialReady)
            {
                Ads.ShowInterstitial("after_win", () =>
                {
                    Pacing.OnInterstitialShown(Clock.UtcNow);
                    Analytics.Log(AnalyticsEvents.AdInterstitial, new Dictionary<string, object> { ["level"] = levelNumber });
                    Dirty();
                    then();
                });
            }
            else then();
        }

        // ---- economy -----------------------------------------------------------------------------
        public void Grant(CalendarReward r)
        {
            if (r.Coins > 0) Wallet.Add(r.Coins);
            Save.Boosters.Undo += r.Undo;
            Save.Boosters.Hammer += r.Hammer;
            if (r.Freeze > 0) Save.DailyStreak.AddEarnedFreeze(r.Freeze);
            Dirty();
        }

        public void ClaimChest(int worldIndex, int idx)
        {
            if (!Save.Progress.CanClaimChest(worldIndex, idx)) return;
            Grant(Save.Progress.ClaimChest(worldIndex, idx));
            Save.Stickers.Add(Stickers.ForChest(worldIndex, idx));
            Sfx.Play(Sound.Coin);
            Popups.Toast("Chest opened! + sticker");
            RefreshMap();
        }

        public void ShowRewarded(string placement, Action<bool> done)
        {
            if (!Ads.IsRewardedReady) { Popups.Toast("No video available right now"); done(false); return; }
            Ads.ShowRewarded(placement, ok =>
            {
                Analytics.Log(AnalyticsEvents.AdRewarded, new Dictionary<string, object> { ["placement"] = placement, ["rewarded"] = ok });
                done(ok);
            });
        }

        public void Buy(string productId)
        {
            if (!Privacy.Purchases) return;
            var def = StoreCatalog.Find(productId);
            if (def == null) return;
            if (productId == StoreCatalog.Piggy && !Save.Piggy.CanBreak(Config)) return;
            Iap.Purchase(productId, outcome =>
            {
                if (outcome == PurchaseOutcome.Failed) Popups.Toast("Purchase failed - you were not charged");
                else if (outcome == PurchaseOutcome.Pending) Popups.Toast("Payment pending - your items arrive when it completes");
            });
        }

        /// <summary>Single place where purchased content is granted (also for deferred/restored transactions).</summary>
        void GrantPurchase(string productId)
        {
            var def = StoreCatalog.Find(productId);
            if (def == null) return;
            {
                if (def.Coins > 0) Wallet.Add(def.Coins);
                Save.Boosters.Undo += def.Undo;
                Save.Boosters.Hammer += def.Hammer;
                if (def.RemovesAds) Save.RemoveAds = true;
                if (def.OncePerUser) Save.StarterBought = true;
                if (productId == StoreCatalog.Piggy) Wallet.Add(Save.Piggy.Break());
                Analytics.Log(AnalyticsEvents.IapPurchase, new Dictionary<string, object> { ["sku"] = productId });
                Sfx.Play(Sound.Coin);
                Popups.Toast("Thank you!");
                Dirty();
                SaveNow();
            }
        }

        void SyncOwnedProducts()
        {
            if (Iap.IsOwned(StoreCatalog.RemoveAds) || Iap.IsOwned(StoreCatalog.NoAdsBundle)) { Save.RemoveAds = true; Dirty(); }
        }

        public void RestorePurchases() => Iap.Restore(ok => { SyncOwnedProducts(); Popups.Toast(ok ? "Purchases restored" : "Restore failed"); });

        // ---- platform -----------------------------------------------------------------------------
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void TD_Share(string text);
#endif

        public void Share(string text)
        {
#if UNITY_IOS && !UNITY_EDITOR
            TD_Share(text);
#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                using (var intent = new AndroidJavaObject("android.content.Intent"))
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                    intent.Call<AndroidJavaObject>("setType", "text/plain");
                    intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text);
                    using (var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Share result"))
                        activity.Call("startActivity", chooser);
                }
            }
            catch (Exception e) { Debug.LogException(e); }
#else
            GUIUtility.systemCopyBuffer = text;
            Popups.Toast("Result copied to clipboard");
#endif
        }

        // ---- persistence ----------------------------------------------------------------------------
        public void Dirty() => dirty = true;

        public void SaveNow()
        {
            Save.Coins = Wallet.Coins;
            store.Save(Save);
            dirty = false;
            lastSave = Time.unscaledTime;
        }

        void Update()
        {
            MainThread.Drain();
            if (dirty && Time.unscaledTime - lastSave > 2f) SaveNow();
        }

        void OnApplicationPause(bool paused) { if (paused && Save != null) SaveNow(); }
        void OnApplicationQuit() { if (Save != null) SaveNow(); }

        public void DeleteAllData()
        {
            store.DeleteAll();
            dirty = false;
            Save = null;
            ClearScreens();
            Destroy(canvas.gameObject);
            Destroy(world);
            I = null;
            Destroy(gameObject);
            Boot();
        }
    }
}
