namespace SummaRace.Tests.EditMode
{
    using System.Reflection;
    using NUnit.Framework;
    using SummaRace.Core;
    using SummaRace.Data;
    using UnityEngine;

    /// <summary>
    /// The export backfills an empty <c>participantCode</c> by string replacement, not by
    /// parsing and re-serialising the row — because a row written by an older build carries
    /// fields this build's <see cref="SessionLog"/> may not declare, and a JsonUtility round
    /// trip would silently drop them. Study data is append-only; the export copies rows.
    ///
    /// The cost of that choice is a hidden coupling: <c>SaveManager.EmptyCodeToken</c> has to be
    /// byte-for-byte what JsonUtility actually emits for an unset code. If <c>AppendLog</c> ever
    /// switches to prettyPrint, or the field is renamed, the token stops matching and the
    /// backfill quietly does nothing — a safe direction to fail in, but a silent one, and the
    /// symptom (rows that cannot be joined to a paper pretest) only appears during analysis.
    ///
    /// So assert the coupling instead of documenting it. This is the whole reason the test
    /// exists; it is not testing JsonUtility.
    /// </summary>
    public class ExportBackfillTests
    {
        private static string EmptyCodeToken()
        {
            var field = typeof(SaveManager).GetField(
                "EmptyCodeToken", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field,
                "SaveManager.EmptyCodeToken has been renamed or removed. If the export no longer " +
                "backfills participant codes by string replacement, delete this test; if it does, " +
                "point the test at the new name.");
            return (string)field.GetRawConstantValue();
        }

        [Test]
        public void EmptyParticipantCodeSerialisesExactlyAsTheBackfillTokenExpects()
        {
            var row = new SessionLog();   // participantCode left unset — the case being matched
            var json = JsonUtility.ToJson(row);

            StringAssert.Contains(EmptyCodeToken(), json,
                "A log row with no participant code does not contain the token the export " +
                "searches for, so no row would ever be backfilled. The most likely cause is " +
                "AppendLog switching to prettyPrint (which inserts spaces JsonUtility.ToJson(x) " +
                "does not) or the field being renamed.");
        }

        [Test]
        public void ASetParticipantCodeDoesNotLookLikeAnEmptyOne()
        {
            var row = new SessionLog { participantCode = "P07" };
            var json = JsonUtility.ToJson(row);

            StringAssert.DoesNotContain(EmptyCodeToken(), json,
                "A row that already carries a participant code matches the empty-code token, so " +
                "the export would overwrite one child's code with another's. The backfill must " +
                "only ever fill a blank.");
        }
    }
}
