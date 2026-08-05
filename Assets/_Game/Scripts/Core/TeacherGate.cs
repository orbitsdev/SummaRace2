using System;
using System.Security.Cryptography;
using System.Text;
using SummaRace.Constants;
using UnityEngine;

namespace SummaRace.Core
{
    /// <summary>
    /// The teacher's PIN and the actions behind it (GDD §8.3). Sessions open one at a time so
    /// learners cannot self-advance — that is what keeps app exposure aligned with the ten
    /// scheduled classroom sessions, so it is an internal-validity control, not a convenience.
    /// The raw PIN is never stored, only a salted hash — which is why the only way past a
    /// forgotten PIN is ResetDevice(), and why setting one has to be deliberate.
    /// </summary>
    public static class TeacherGate
    {
        private const string Salt = "SummaRace:teacher:";

        /// <summary>Shortest PIN a teacher may choose. GameText.TeacherSetPin quotes this.</summary>
        public const int MinPinLength = 4;

        /// <summary>True once a PIN exists — first launch has none, so the teacher sets it.</summary>
        public static bool HasPin()
        {
            var settings = LoadSettings();
            return settings != null && !string.IsNullOrEmpty(settings.teacherPinHash);
        }

        public static bool SetPin(string pin)
        {
            if (!IsPinAcceptable(pin) || SaveManager.Instance == null) return false;

            var settings = LoadSettings();
            settings.teacherPinHash = Hash(pin);
            SaveManager.Instance.SaveSettings(settings);
            return true;
        }

        public static bool VerifyPin(string pin)
        {
            if (string.IsNullOrEmpty(pin)) return false;

            var settings = LoadSettings();
            if (settings == null || string.IsNullOrEmpty(settings.teacherPinHash)) return false;

            return string.Equals(settings.teacherPinHash, Hash(pin), StringComparison.Ordinal);
        }

        /// <summary>
        /// Opens the next session for the active learner. Returns the session now unlocked, or 0
        /// when nothing changed (no learner, or all ten already open).
        /// </summary>
        public static int UnlockNextSession()
        {
            var learner = GameManager.Instance != null ? GameManager.Instance.CurrentLearner : null;
            if (learner == null || learner.unlockedSession >= GameRules.SessionCount) return 0;

            learner.unlockedSession++;
            GameManager.Instance.PersistProfiles();
            EventBus.Raise(new SessionUnlocked { sessionNumber = learner.unlockedSession });
            return learner.unlockedSession;
        }

        /// <summary>
        /// Erases this tablet and re-arms the gate: profiles, logs and the PIN itself go, so a
        /// new PIN can be set. A salted hash cannot be read back, so a forgotten (or a
        /// learner-invented) PIN has no gentle recovery — without this the device could never
        /// unlock another session or export another log again, and the only remaining escape
        /// would be clearing app data from Android settings, which costs exactly the same data
        /// with none of the warning. Destructive by design; the caller states the cost first.
        /// </summary>
        public static void ResetDevice()
        {
            if (SaveManager.Instance != null) SaveManager.Instance.DeleteAllData();
            ClearPin();
            // Rebuild the default profile so the next screen still has a learner to read,
            // exactly as the post-study wipe does — a reset must not leave the game profileless.
            if (GameManager.Instance != null) GameManager.Instance.InitProfiles();
        }

        /// <summary>A PIN has to be memorable for a teacher but not a single keypress.</summary>
        public static bool IsPinAcceptable(string pin) =>
            !string.IsNullOrWhiteSpace(pin) && pin.Trim().Length >= MinPinLength;

        /// <summary>
        /// Compares the two halves of PIN setup. Lives here because Hash() trims before
        /// hashing: comparing raw text would report "1234 " against "1234" as a mismatch even
        /// though both store the same hash.
        /// </summary>
        public static bool PinsMatch(string first, string second) =>
            IsPinAcceptable(first) && second != null &&
            string.Equals(first.Trim(), second.Trim(), StringComparison.Ordinal);

        /// <summary>Drops the stored hash so the gate falls back to "set a PIN".</summary>
        private static void ClearPin()
        {
            if (SaveManager.Instance == null) return;

            var settings = LoadSettings();
            settings.teacherPinHash = null;
            SaveManager.Instance.SaveSettings(settings);
        }

        private static Data.AppSettings LoadSettings() =>
            SaveManager.Instance != null ? SaveManager.Instance.LoadSettings() : null;

        private static string Hash(string pin)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(Salt + pin.Trim()));
                var text = new StringBuilder(bytes.Length * 2);
                for (int i = 0; i < bytes.Length; i++) text.Append(bytes[i].ToString("x2"));
                return text.ToString();
            }
        }
    }
}
