using System;
using System.IO;
using NUnit.Framework;

public class SaveSystemTests
{
    string path;

    [SetUp]
    public void SetUp()
    {
        path = Path.Combine(Path.GetTempPath(), $"roosevelt-save-test-{Guid.NewGuid():N}.json");
        GameState.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(path)) File.Delete(path);
        GameState.Reset();
    }

    [Test]
    public void SaveThenLoadRestoresDayFlagsAndItems()
    {
        GameState.AdvanceDay();
        GameState.SetFlag(GameFlags.DreamSatChair);
        GameState.AddItem("Key");
        SaveSystem.Save(path);

        GameState.Reset();
        Assert.IsTrue(SaveSystem.Load(path));
        Assert.AreEqual(1, GameState.Day);
        Assert.IsTrue(GameState.HasFlag(GameFlags.DreamSatChair));
        Assert.IsTrue(GameState.HasItem("Key"));
    }

    [Test]
    public void LoadWithoutFileKeepsState()
    {
        GameState.SetFlag(GameFlags.TalkedToLobbyNPC);
        GameState.AddItem("Key");
        Assert.IsFalse(SaveSystem.Load(path));
        Assert.IsTrue(GameState.HasFlag(GameFlags.TalkedToLobbyNPC));
        Assert.IsTrue(GameState.HasItem("Key"));
    }
}
