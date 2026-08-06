using UnityEngine;

namespace SummaRace.Core
{
    /// <summary>
    /// A short vibration on the moments worth feeling (GDD §11.4: "tiny vibration on collect and
    /// star pops, toggleable, default ON"). <c>AppSettings.haptics</c> has existed since the model
    /// was written and was read by nobody — the same shape of gap that left
    /// <c>narrationVolume</c> declared, persisted and ignored until F50.
    ///
    /// WHY NOT <c>Handheld.Vibrate()</c>. It is the obvious one-liner and it is wrong here: on
    /// Android it is a fixed ~500ms buzz with no duration argument. The GDD asks for a *tiny*
    /// vibration on collect, and a race has five collects — half a second of buzzing, five times,
    /// while a nine-year-old is trying to read. That is worse than no haptics at all, which is
    /// why this goes through <c>VibrationEffect</c> instead and why there is deliberately **no
    /// fallback to Handheld.Vibrate**. If the short path is unavailable, this does nothing.
    ///
    /// Everything here is best-effort and silent on failure. Haptics are a garnish; they must
    /// never throw into gameplay, never log noise on a device that has no vibrator, and never
    /// block a learner. The whole class degrades to a no-op in the Editor, on a device without a
    /// vibrator, and on any Android where the reflection fails.
    /// </summary>
    public static class Haptics
    {
        /// <summary>Collect / correct answer. Short enough to read as a tick, not a buzz.</summary>
        public const long Light = 18L;

        /// <summary>A star landing. Still short — three of these play in a row on Results.</summary>
        public const long Medium = 28L;

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject _vibrator;
        private static bool _resolved;
        private static bool _hasEffects;   // VibrationEffect exists (API 26+; our min SDK is 26)
#endif

        /// <summary>Honours the learner's/teacher's setting, re-read each time rather than
        /// cached: the toggle can change between two vibrations and a stale copy would keep
        /// buzzing a device someone has just asked to stop.</summary>
        private static bool Enabled
        {
            get
            {
                var save = SaveManager.Instance;
                if (save == null) return false;          // editor-direct play: no settings, no buzz
                var settings = save.LoadSettings();
                return settings != null && settings.haptics;
            }
        }

        /// <summary>One short tick. <paramref name="milliseconds"/> is a duration, not an
        /// intensity — see <see cref="Light"/> / <see cref="Medium"/>.</summary>
        public static void Play(long milliseconds)
        {
            if (milliseconds <= 0L || !Enabled) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                Resolve();
                if (_vibrator == null) return;
                if (!_vibrator.Call<bool>("hasVibrator")) return;

                if (_hasEffects)
                {
                    using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                    {
                        // -1 = DEFAULT_AMPLITUDE: let the device pick, rather than forcing a
                        // strength that feels different on every tablet.
                        var effect = effectClass.CallStatic<AndroidJavaObject>(
                            "createOneShot", milliseconds, -1);
                        _vibrator.Call("vibrate", effect);
                    }
                }
                else
                {
                    // Pre-26 path. Our min SDK is 26 so this should be unreachable; kept because
                    // the cost is one branch and the alternative is a crash on a device we did
                    // not anticipate.
                    _vibrator.Call("vibrate", milliseconds);
                }
            }
            catch
            {
                // A device without a vibrator, a manufacturer that renamed something, a
                // permission quirk. Silence is the correct outcome for a garnish.
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;   // set first: a throw below must not retry on every collect
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    _hasEffects = version.GetStatic<int>("SDK_INT") >= 26;
                }
            }
            catch
            {
                _vibrator = null;
            }
        }
#endif
    }
}
