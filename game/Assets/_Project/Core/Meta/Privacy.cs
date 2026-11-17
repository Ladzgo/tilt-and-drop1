using System;

namespace TiltDrop.Core
{
    public enum AgeBand : byte { Unknown = 0, Under13 = 1, Under16 = 2, Adult = 3 }

    /// <summary>What the app may do for this user (docs/COMPLIANCE.md §2–3).</summary>
    public struct PrivacyDecision
    {
        public bool InitMax;            // AppLovin MAX (16+ only)
        public bool InitAdMobDirect;    // Google Mobile Ads with under-age tags (under 16)
        public bool PersonalizedAds;
        public bool ChildDirected;      // tagForChildDirectedTreatment (under 13)
        public bool RequestAtt;         // iOS ATT prompt
        public bool Analytics;
        public bool Crashlytics;
        public bool Purchases;
        public bool ShowConsentForm;    // run UMP via MAX flow
    }

    public static class PrivacyRules
    {
        public const int ConsentAge = 16; // Slovakia, GDPR Art. 8 - applied globally

        public static AgeBand FromBirth(int year, int month, DateTime today)
        {
            if (year < 1900 || year > today.Year || month < 1 || month > 12) return AgeBand.Unknown;
            int age = today.Year - year - (today.Month < month ? 1 : 0);
            if (age < 13) return AgeBand.Under13;
            if (age < ConsentAge) return AgeBand.Under16;
            return AgeBand.Adult;
        }

        /// <summary>The age answer may only move to a younger band (prevents gaming the gate).</summary>
        public static AgeBand Update(AgeBand stored, AgeBand answer)
        {
            if (stored == AgeBand.Unknown) return answer;
            if (answer == AgeBand.Unknown) return stored;
            return (AgeBand)Math.Min((int)stored, (int)answer);
        }

        /// <param name="personalizedConsent">TCF/UMP result allows personalized ads (16+ only).</param>
        /// <param name="analyticsConsent">TCF/UMP result allows measurement (purposes 1, 8, 9).</param>
        /// <param name="crashlyticsOptOut">User opted out in Settings.</param>
        public static PrivacyDecision Decide(AgeBand band, bool isIos, bool personalizedConsent, bool analyticsConsent, bool crashlyticsOptOut)
        {
            if (band == AgeBand.Adult)
            {
                return new PrivacyDecision
                {
                    InitMax = true,
                    ShowConsentForm = true,
                    PersonalizedAds = personalizedConsent,
                    RequestAtt = isIos && personalizedConsent,
                    Analytics = analyticsConsent,
                    Crashlytics = !crashlyticsOptOut,
                    Purchases = true,
                };
            }
            // Under 16, Under 13 and Unknown (age screen not answered yet) -> most protective path.
            return new PrivacyDecision
            {
                InitAdMobDirect = band != AgeBand.Unknown,
                ChildDirected = band == AgeBand.Under13,
                PersonalizedAds = false,
                RequestAtt = false,
                Analytics = false,
                Crashlytics = false,
                Purchases = false,
            };
        }
    }
}
