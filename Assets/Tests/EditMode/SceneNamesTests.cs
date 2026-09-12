using System;
using NUnit.Framework;

public class SceneNamesTests
{
    [TestCase(0, "Dream_00")]
    [TestCase(5, "Dream_05")]
    public void DreamForPadsTheDay(int day, string expected) => Assert.AreEqual(expected, SceneNames.DreamFor(day));

    [TestCase(-1)]
    [TestCase(6)]
    public void DreamForRejectsDaysOutsideTheGame(int day) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneNames.DreamFor(day));

    [TestCase("Dream_03", true)]
    [TestCase("Reality", false)]
    [TestCase(null, false)]
    public void IsDreamReadsTheSceneName(string scene, bool expected) => Assert.AreEqual(expected, SceneNames.IsDream(scene));
}
