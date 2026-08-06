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

        /// <summary>
        /// Stores the salted hash of a new PIN. Returns true only once the hash is verified on
        /// disk: SaveManager.SaveSettings swallows a failed write (it raises SaveFailed and
        /// returns void), so this used to report success unconditionally — and the screen then
        /// told the researcher "PIN saved, write it in the study notes, it cannot be read back"
        /// for a PIN the device does not have. On the next launch that tablet is back on the
        /// setup step with nobody expecting it, which is the one state where a learner can claim
        /// the gate. Cheap to be sure: read it back.
        /// </summary>
        public static bool SetPin(string pin)
        {
            if (!IsPinAcceptable(pin) || SaveManager.Instance == null) return false;

            var settings = LoadSettings();
            if (settings == null) return false;

            string hash = Hash(pin);
            // A null hash means hashing itself failed. Storing it would clear the gate AND pass
            // the read-back check below (null equals null), so the screen would report a PIN set
            // on a tablet that has none — the single state a learner can claim the gate from.
            if (string.IsNullOrEmpty(hash)) return false;
            settings.teacherPinHash = hash;
            SaveManager.Instance.SaveSettings(settings);

            var stored = LoadSettings();   // re-read from disk, not from the object just written
            return stored != null && string.Equals(stored.teacherPinHash, hash, StringComparison.Ordinal);
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
        /// when nothing changed (no learner, all ten already open, or the write failed).
        /// <para>
        /// The write is checked and rolled back on failure. Session gating is an internal-validity
        /// control (GDD §8.3): a teacher told "Session 4 is open" who then hands over a tablet
        /// that is still on 3 has a child unable to do the day's work, and the teacher has no
        /// reason to doubt the screen. Unlocking is also not idempotent from the teacher's side —
        /// they would simply tap it again and, on a working write, skip a session.
        /// </para>
        /// </summary>
        public static int UnlockNextSession() => UnlockNextSession(out _);

        /// <param name="saveFailed">True when the unlock was legitimate but did not reach disk.
        /// Without this the caller cannot tell that from "all ten are already open" and would
        /// tell the teacher the opposite of what happened.</param>
        public static int UnlockNextSession(out bool saveFailed)
        {
            saveFailed = false;
            var learner = GameManager.Instance != null ? GameManager.Instance.CurrentLearner : null;
            if (learner == null || learner.unlockedSession >= GameRules.SessionCount) return 0;

            learner.unlockedSession++;
            if (!GameManager.Instance.PersistProfiles())
            {
                learner.unlockedSession--;
                saveFailed = true;
                return 0;
            }
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
        ///
        /// The PIN is cleared FIRST and the data only after that is confirmed. The other order
        /// has a failure mode worse than either half: a swallowed settings write (SaveSettings
        /// raises SaveFailed and returns void) would leave the tablet erased AND still locked by
        /// the PIN nobody knows — the exact state this gesture exists to escape. Failing before
        /// the delete costs a retry; failing after costs the study's data. Returns false when
        /// the gate could not be cleared, in which case nothing was erased.
        /// </summary>
        public static bool ResetDevice()
        {
            if (!ClearPin()) return false;

            if (SaveManager.Instance != null) SaveManager.Instance.DeleteAllData();
            // Rebuild the default profile so the next screen still has a learner to read,
            // exactly as the post-study wipe does — a reset must not leave the game profileless.
            if (GameManager.Instance != null) GameManager.Instance.InitProfiles();
            return true;
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

        /// <summary>Drops the stored hash so the gate falls back to "set a PIN". Verified on
        /// disk for the same reason SetPin is — the write can fail silently, and a reset that
        /// only appeared to clear the gate is the worst outcome this screen can produce.</summary>
        private static bool ClearPin()
        {
            if (SaveManager.Instance == null) return false;

            var settings = LoadSettings();
            if (settings == null) return false;

            settings.teacherPinHash = null;
            SaveManager.Instance.SaveSettings(settings);
            return !HasPin();
        }

        private static Data.AppSettings LoadSettings() =>
            SaveManager.Instance != null ? SaveManager.Instance.LoadSettings() : null;

        /// <summary>
        /// Salted SHA-256 of the PIN. Returns null if hashing is unavailable — see below.
        ///
        /// <c>SHA256.Create()</c> resolves its implementation through CryptoConfig by
        /// REFLECTION, which is precisely what IL2CPP's managed stripping removes, and Android
        /// ships IL2CPP with stripping on. <c>Assets/Link.xml</c> now preserves the crypto
        /// assemblies so this should not happen; the catch is here because of what a throw
        /// costs if it does. Every adult action on the tablet runs through this method — setting
        /// the PIN at install, unlocking the next session, and EXPORTING THE LOGS — and Unity
        /// swallows an exception thrown inside a UI callback, so the researcher would tap Export
        /// and simply see nothing happen, on the one control that retrieves the whole dataset.
        /// A logged null that the callers refuse on is recoverable; a silent no-op is not.
        ///
        /// None of this is visible in the Editor, which does not strip.
        /// </summary>
        private static string Hash(string pin)
        {
            try
            {
                using (var sha = SHA256.Create())
                {
                    var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(Salt + pin.Trim()));
                    var text = new StringBuilder(bytes.Length * 2);
                    for (int i = 0; i < bytes.Length; i++) text.Append(bytes[i].ToString("x2"));
                    return text.ToString();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("TeacherGate: SHA-256 is unavailable on this build, so the teacher " +
                    "PIN cannot be set or verified and the logs cannot be exported. This is almost " +
                    "certainly IL2CPP managed stripping removing the crypto implementation — check " +
                    "that Assets/Link.xml still preserves System.Security.Cryptography.*. " + e);
                return null;
            }
        }
    }
}
