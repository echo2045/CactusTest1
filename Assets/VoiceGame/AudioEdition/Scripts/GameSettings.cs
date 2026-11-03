using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoiceGame
{
    /// <summary>
    /// Settings saved between sessions. Volume moves in 10 percent steps, so every level has a
    /// recorded announcement. It never reaches zero, because a blind player who muted the game
    /// could no longer hear what they were doing.
    /// </summary>
    public static class GameSettings
    {
        public const string VolumeKey = "VoiceGame.VolumePercent";
        public const int MinVolumePercent = 10;
        public const int MaxVolumePercent = 100;
        const int VolumeStepPercent = 10;

        static readonly string[] AbsolutePrefixes = { "set the volume to ", "set volume to ", "volume to ", "volume at ", "volume " };

        static readonly HashSet<string> Louder = new HashSet<string>
        {
            "louder", "lauder", "up", "volume up", "turn it up", "turn up", "turn up the volume", "turn the volume up",
            "increase volume", "increase the volume", "more volume", "too quiet",
        };

        static readonly HashSet<string> Quieter = new HashSet<string>
        {
            "quieter", "quiter", "softer", "down", "volume down", "turn it down", "turn down", "turn down the volume",
            "turn the volume down", "decrease volume", "decrease the volume", "less volume", "too loud",
        };

        static readonly HashSet<string> Loudest = new HashSet<string> { "loudest", "maximum", "maximum volume", "max volume", "full volume" };

        static readonly HashSet<string> Quietest = new HashSet<string> { "quietest", "minimum", "minimum volume", "min volume" };

        static readonly Dictionary<string, int> NumberWords = new Dictionary<string, int>
        {
            ["ten"] = 10, ["twenty"] = 20, ["thirty"] = 30, ["forty"] = 40, ["fifty"] = 50,
            ["sixty"] = 60, ["seventy"] = 70, ["eighty"] = 80, ["ninety"] = 90, ["hundred"] = 100, ["one hundred"] = 100,
        };

        public static int VolumePercent { get; private set; } = MaxVolumePercent;

        public static string VolumeLine => "volume_" + VolumePercent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Load()
        {
            VolumePercent = Snap(PlayerPrefs.GetInt(VolumeKey, MaxVolumePercent));
            AudioListener.volume = VolumePercent / 100f;
        }

        public static void SetVolumePercent(int percent)
        {
            VolumePercent = Snap(percent);
            AudioListener.volume = VolumePercent / 100f;
            PlayerPrefs.SetInt(VolumeKey, VolumePercent);
            PlayerPrefs.Save();
        }

        /// <summary>Moves one step and returns the narration line that announces the result.</summary>
        public static string StepVolume(int direction)
        {
            var before = VolumePercent;
            SetVolumePercent(before + Math.Sign(direction) * VolumeStepPercent);
            if (VolumePercent != before) return VolumeLine;
            return direction > 0 ? "volume_max" : "volume_min";
        }

        public static int Snap(int percent)
        {
            var stepped = (int)Math.Round(percent / (double)VolumeStepPercent, MidpointRounding.AwayFromZero) * VolumeStepPercent;
            return Math.Clamp(stepped, MinVolumePercent, MaxVolumePercent);
        }

        /// <summary>Carries out a spoken volume request, if the phrase is one.</summary>
        public static bool TryVoice(string phrase, bool allowBareNumber, out string line)
        {
            line = null;
            if (Louder.Contains(phrase)) line = StepVolume(1);
            else if (Quieter.Contains(phrase)) line = StepVolume(-1);
            else if (Loudest.Contains(phrase)) line = SetAndAnnounce(MaxVolumePercent);
            else if (Quietest.Contains(phrase)) line = SetAndAnnounce(MinVolumePercent);
            else if (TryNumber(phrase, allowBareNumber, out var percent)) line = SetAndAnnounce(percent);
            return line != null;
        }

        static string SetAndAnnounce(int percent)
        {
            SetVolumePercent(percent);
            return VolumeLine;
        }

        static bool TryNumber(string phrase, bool allowBare, out int percent)
        {
            percent = 0;
            var marked = false;
            foreach (var prefix in AbsolutePrefixes)
            {
                if (!phrase.StartsWith(prefix, StringComparison.Ordinal)) continue;
                phrase = phrase.Substring(prefix.Length);
                marked = true;
                break;
            }
            if (phrase.EndsWith(" percent", StringComparison.Ordinal))
            {
                phrase = phrase.Substring(0, phrase.Length - " percent".Length);
                marked = true;
            }
            if (!marked && !allowBare) return false;
            if (NumberWords.TryGetValue(phrase, out percent)) return true;
            if (!int.TryParse(phrase, out percent) || percent < 0 || percent > MaxVolumePercent) return false;
            if (!marked && percent < MinVolumePercent) return false;
            // Speech recognition mixes up fifty and fifteen, and only tens are valid here.
            if (percent >= 13 && percent <= 19) percent = (percent - 10) * 10;
            return true;
        }
    }
}
