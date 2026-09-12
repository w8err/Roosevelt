using System;
using System.Collections.Generic;
using NUnit.Framework;

public class GameStateTests
{
    int changes;

    void Count() => changes++;

    [SetUp]
    public void SetUp()
    {
        GameState.Reset();
        changes = 0;
        GameState.Changed += Count;
    }

    [TearDown]
    public void TearDown() => GameState.Changed -= Count;

    [Test]
    public void StartsAtDayZeroWithoutFlags()
    {
        Assert.AreEqual(GameState.FirstDay, GameState.Day);
        Assert.IsFalse(GameState.HasFlag(GameFlags.TalkedToLobbyNPC));
    }

    [Test]
    public void SetFlagIsVisibleAndNotifiesOnce()
    {
        GameState.SetFlag(GameFlags.TalkedToLobbyNPC);
        GameState.SetFlag(GameFlags.TalkedToLobbyNPC);
        Assert.IsTrue(GameState.HasFlag(GameFlags.TalkedToLobbyNPC));
        Assert.AreEqual(1, changes);
    }

    [Test]
    public void ClearFlagRemovesIt()
    {
        GameState.SetFlag(GameFlags.DreamSatChair);
        GameState.ClearFlag(GameFlags.DreamSatChair);
        GameState.ClearFlag(GameFlags.DreamSatChair);
        Assert.IsFalse(GameState.HasFlag(GameFlags.DreamSatChair));
        Assert.AreEqual(2, changes);
    }

    [Test]
    public void AdvanceDayStopsAtLastDay()
    {
        for (var day = GameState.FirstDay; day < GameState.LastDay; day++) GameState.AdvanceDay();
        Assert.AreEqual(GameState.LastDay, GameState.Day);
        Assert.Throws<InvalidOperationException>(GameState.AdvanceDay);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void BlankFlagNameThrows(string flag)
    {
        Assert.Throws<ArgumentException>(() => GameState.SetFlag(flag));
        Assert.Throws<ArgumentException>(() => GameState.HasFlag(flag));
    }

    [Test]
    public void ResetClearsDayAndFlags()
    {
        GameState.AdvanceDay();
        GameState.SetFlag(GameFlags.TalkedToLobbyNPC);
        GameState.Reset();
        Assert.AreEqual(GameState.FirstDay, GameState.Day);
        Assert.IsFalse(GameState.HasFlag(GameFlags.TalkedToLobbyNPC));
    }

    [Test]
    public void RestoreReplacesState()
    {
        GameState.SetFlag("Stale");
        GameState.Restore(new SaveData { day = 3, flags = new List<string> { GameFlags.DreamSatChair } });
        Assert.AreEqual(3, GameState.Day);
        Assert.IsTrue(GameState.HasFlag(GameFlags.DreamSatChair));
        Assert.IsFalse(GameState.HasFlag("Stale"));
    }

    [Test]
    public void RestoreRejectsBadDataWithoutChangingState()
    {
        GameState.SetFlag(GameFlags.TalkedToLobbyNPC);
        Assert.Throws<ArgumentOutOfRangeException>(() => GameState.Restore(new SaveData { day = GameState.LastDay + 1 }));
        Assert.Throws<ArgumentException>(() => GameState.Restore(new SaveData { day = 1, flags = new List<string> { "" } }));
        Assert.AreEqual(GameState.FirstDay, GameState.Day);
        Assert.IsTrue(GameState.HasFlag(GameFlags.TalkedToLobbyNPC));
    }
}
