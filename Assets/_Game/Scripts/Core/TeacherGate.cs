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
    /// The raw PIN is never stored, only a salted hash.
    /// </summary>
    public static class TeacherGate
    {
        private const string Salt = "SummaRace:teacher:";

        /// <summary>True once a PIN exists — first launch has none, so the teacher sets it.</summary>
        public static bool HasPin()
        {
            var settings = LoadSettings();
            return settings != null && !string.IsNullOrEmpty(settings.teacherPinHash);
        }

        public static bool SetPin(string pin)
        {
            if (!IsPlausible(pin) || SaveManager.Instance == null) return false;

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

        /// <summary>A PIN has to be memorable for a teacher but not a single keypress.</summary>
        private static bool IsPlausible(string pin) =>
            !string.IsNullOrWhiteSpace(pin) && pin.Trim().Length >= 4;

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
