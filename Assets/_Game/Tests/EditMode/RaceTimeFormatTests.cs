using NUnit.Framework;
using Apexline.Race;

namespace Apexline.Tests
{
    /// <summary>
    /// Lap times are the most-read text in the game. A formatting regression here
    /// is immediately visible to every player, so the edge cases are pinned.
    /// </summary>
    public class RaceTimeFormatTests
    {
        [Test]
        public void FormatTime_ShowsMinutesSecondsMilliseconds()
        {
            Assert.AreEqual("1:23.456", RaceManager.FormatTime(83.456f));
        }

        [Test]
        public void FormatTime_PadsSecondsBelowTen()
        {
            Assert.AreEqual("1:03.000", RaceManager.FormatTime(63f),
                "seconds must be zero-padded or the time reads as 1:3.000");
        }

        [Test]
        public void FormatTime_HandlesSubMinute()
        {
            Assert.AreEqual("0:42.500", RaceManager.FormatTime(42.5f));
        }

        [Test]
        public void FormatTime_ReturnsPlaceholder_ForUnsetTimes()
        {
            Assert.AreEqual("--:--.---", RaceManager.FormatTime(0f));
            Assert.AreEqual("--:--.---", RaceManager.FormatTime(-5f));
            Assert.AreEqual("--:--.---", RaceManager.FormatTime(float.MaxValue),
                "bestLapTime is seeded to float.MaxValue and must not render as a number");
            Assert.AreEqual("--:--.---", RaceManager.FormatTime(float.PositiveInfinity));
        }
    }
}
