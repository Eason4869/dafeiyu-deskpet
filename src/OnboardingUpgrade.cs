using System;
using System.Globalization;

namespace Dafeiyu
{
    public sealed class OnboardingUpgradeResult
    {
        public bool Changed {get;private set;}
        public bool ShowGuide {get;private set;}
        internal OnboardingUpgradeResult(bool changed,bool showGuide){Changed=changed;ShowGuide=showGuide;}
    }

    // Call immediately after loading settings and save Changed before opening a
    // window. Recording the trigger, rather than completion, also deduplicates a
    // closed guide and preserves the high-water mark across application downgrades.
    public static class OnboardingUpgrade
    {
        public const string LegacySeries="0.5";
        public static OnboardingUpgradeResult Prepare(PetSettings settings,string version)
        {
            if(settings==null)throw new ArgumentNullException("settings");
            Version current;
            if(!TrySeries(version,out current))return new OnboardingUpgradeResult(false,false);
            bool first=String.IsNullOrEmpty(settings.LastOnboardingSeries) && settings.OnboardingState=="new";
            Version recorded;
            if(!TrySeries(settings.LastOnboardingSeries,out recorded))recorded=new Version(0,5);
            bool show=first || current.CompareTo(recorded)>0;
            string series=Format(show?current:recorded);
            bool changed=settings.LastOnboardingSeries!=series;
            settings.LastOnboardingSeries=series;
            if(show) {
                settings.OnboardingState="new";settings.OnboardingStep=0;settings.OnboardingVersion=1;
                changed=true;
            }
            return new OnboardingUpgradeResult(changed,show);
        }
        static bool TrySeries(string value,out Version series)
        {
            series=null;
            // Reject corrupt input before parsing, including unbounded digit runs.
            if(String.IsNullOrWhiteSpace(value) || value.Length>128)return false;
            Version parsed;if(!Version.TryParse(value.Trim(),out parsed))return false;
            series=new Version(parsed.Major,parsed.Minor);return true;
        }
        static string Format(Version value){return value.Major.ToString(CultureInfo.InvariantCulture)+"."+value.Minor.ToString(CultureInfo.InvariantCulture);}
    }
}
