using System;

namespace TiltDrop.Core
{
    /// <summary>Persistent pacing state (saved).</summary>
    public sealed class AdPacingState
    {
        public int SessionIndex;          // 1 = first session ever
        public int WinsSinceInterstitial;
        public long LastInterstitialUtcTicks;
    }

    public enum AdMoment { AfterWin, AfterFail, AfterDaily, AfterStore, AppStart }

    /// <summary>GDD §6.2 interstitial rules. Pure and unit-tested.</summary>
    public sealed class AdPacingPolicy
    {
        readonly GameConfig cfg;
        readonly AdPacingState st;

        public AdPacingPolicy(GameConfig cfg, AdPacingState st) { this.cfg = cfg; this.st = st; }

        public void OnSessionStart() => st.SessionIndex++;
        public void OnLevelWon() => st.WinsSinceInterstitial++;

        public bool ShouldShowInterstitial(AdMoment moment, int levelNumber, bool removeAds, DateTime utcNow)
        {
            if (removeAds) return false;
            if (moment != AdMoment.AfterWin) return false;                       // never after fail/daily/store
            if (st.SessionIndex <= 1 && !cfg.AdInterFirstSession) return false;  // never in the first session
            if (levelNumber < cfg.AdInterMinLevel) return false;
            if (st.WinsSinceInterstitial < cfg.AdInterLevelInterval) return false;
            if (st.LastInterstitialUtcTicks > 0 && (utcNow - new DateTime(st.LastInterstitialUtcTicks, DateTimeKind.Utc)).TotalSeconds < cfg.AdInterCooldownSeconds) return false;
            return true;
        }

        public void OnInterstitialShown(DateTime utcNow)
        {
            st.WinsSinceInterstitial = 0;
            st.LastInterstitialUtcTicks = utcNow.Ticks;
        }
    }
}
