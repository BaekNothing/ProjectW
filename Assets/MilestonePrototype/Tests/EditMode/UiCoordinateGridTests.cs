using NUnit.Framework;

namespace ProjectW.MilestonePrototype.Tests
{
    public sealed class UiCoordinateGridTests
    {
        [TestCase(0, "A")]
        [TestCase(25, "Z")]
        [TestCase(26, "AA")]
        [TestCase(27, "AB")]
        [TestCase(701, "ZZ")]
        public void RowLabel_UsesSpreadsheetStyleLetters(int row, string expected)
        {
            Assert.That(UiCoordinateGrid.RowLabel(row), Is.EqualTo(expected));
        }

        [Test]
        public void RowLabel_ReturnsEmptyForNegativeRows()
        {
            Assert.That(UiCoordinateGrid.RowLabel(-1), Is.Empty);
        }
    }
}
