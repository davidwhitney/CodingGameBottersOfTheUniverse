using NUnit.Framework;

namespace CodingGameBottersOfTheUniverse.Tests
{
    [TestFixture]
    public class UnitTests
    {
        [Test]
        public void IsInFrontOf_Team1AndInFront_Works()
        {
            var behind = new Unit {X = 1, Y = 1, Team = 0};
            var infront = new Unit {X = 2, Y = 1, Team = 0};

            Assert.That(infront.IsInFrontOf(behind), Is.True);
        }

        [Test]
        public void IsInFrontOf_Team1AndBehind_Works()
        {
            var beind = new Unit {X = 1, Y = 1, Team = 0 };
            var infront = new Unit { X = 2, Y = 1, Team = 0 };

            Assert.That(beind.IsInFrontOf(infront), Is.False);
        }

        [Test]
        public void IsInFrontOf_Team2AndBehind_Works()
        {
            var infront = new Unit {X = 1, Y = 1, Team = 1 };
            var behind = new Unit {X = 2, Y = 1, Team = 1 };

            Assert.That(infront.IsInFrontOf(behind), Is.True);
        }

        [Test]
        public void IsInFrontOf_Team2AndInFront_Works()
        {
            var infront = new Unit {X = 1, Y = 1, Team = 1 };
            var behind = new Unit {X = 2, Y = 1, Team = 1 };

            Assert.That(behind.IsInFrontOf(infront), Is.False);
        }
    }
}
