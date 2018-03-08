using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace CodingGameBottersOfTheUniverse.Tests
{
    [TestFixture]
    public class UnitTests
    {
        [Test]
        public void IsInFrontOf_Team1AndInFront_Works()
        {
            var behind = new Unit {X = 1, Y = 1};
            var infront = new Unit {X = 2, Y = 1};

            Assert.That(infront.IsInFrontOf(behind, 0), Is.True);
        }

        [Test]
        public void IsInFrontOf_Team1AndBehind_Works()
        {
            var beind = new Unit {X = 1, Y = 1};
            var infront = new Unit { X = 2, Y = 1 };

            Assert.That(beind.IsInFrontOf(infront, 0), Is.False);
        }

        [Test]
        public void IsInFrontOf_Team2AndBehind_Works()
        {
            var infront = new Unit {X = 1, Y = 1};
            var behind = new Unit {X = 2, Y = 1};

            Assert.That(infront.IsInFrontOf(behind, 1), Is.True);
        }

        [Test]
        public void IsInFrontOf_Team2AndInFront_Works()
        {
            var infront = new Unit {X = 1, Y = 1};
            var behind = new Unit {X = 2, Y = 1};

            Assert.That(behind.IsInFrontOf(infront, 1), Is.False);
        }
    }
}
