using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class DialogueRunnerTests
{
    readonly HashSet<string> flags = new();
    DialogueData data;

    [SetUp]
    public void SetUp()
    {
        flags.Clear();
        data = ScriptableObject.CreateInstance<DialogueData>();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(data);

    DialogueRunner Start(string startNode, params DialogueNode[] nodes)
    {
        data.startNode = startNode;
        data.nodes = new List<DialogueNode>(nodes);
        var runner = new DialogueRunner(data, flags.Contains, flag => flags.Add(flag));
        runner.Start();
        return runner;
    }

    static DialogueNode Node(string id, string next = null) => new DialogueNode { id = id, text = id, next = next };

    static DialogueChoice Choice(string text, string next = null, string requiredFlag = null, string setFlag = null) =>
        new DialogueChoice { text = text, next = next, requiredFlag = requiredFlag, setFlag = setFlag };

    [Test]
    public void AdvanceFollowsNextUntilTheEnd()
    {
        var runner = Start("a", Node("a", "b"), Node("b"));
        Assert.AreEqual("a", runner.Current.id);
        runner.Advance();
        Assert.AreEqual("b", runner.Current.id);
        runner.Advance();
        Assert.IsTrue(runner.IsFinished);
        Assert.IsNull(runner.Current);
    }

    [Test]
    public void MissingRequiredFlagJumpsToFallback()
    {
        var again = Node("again");
        again.requiredFlag = GameFlags.TalkedToLobbyNPC;
        again.fallback = "first";
        var nodes = new[] { again, Node("first") };

        Assert.AreEqual("first", Start("again", nodes).Current.id);
        flags.Add(GameFlags.TalkedToLobbyNPC);
        Assert.AreEqual("again", Start("again", nodes).Current.id);
    }

    [Test]
    public void MissingRequiredFlagWithoutFallbackEnds()
    {
        var locked = Node("locked");
        locked.requiredFlag = "Key";
        Assert.IsTrue(Start("locked", locked).IsFinished);
    }

    [Test]
    public void ShownNodeSetsItsFlag()
    {
        var node = Node("a");
        node.setFlag = GameFlags.TalkedToLobbyNPC;
        Start("a", node);
        Assert.IsTrue(flags.Contains(GameFlags.TalkedToLobbyNPC));
    }

    [Test]
    public void ChoicesWithoutTheirFlagAreHidden()
    {
        var node = Node("ask");
        node.choices = new List<DialogueChoice> { Choice("open"), Choice("secret", requiredFlag: "Key") };

        Assert.AreEqual(1, Start("ask", node).VisibleChoices.Count);
        flags.Add("Key");
        Assert.AreEqual(2, Start("ask", node).VisibleChoices.Count);
    }

    [Test]
    public void ChooseSetsTheChoiceFlagAndFollowsItsNext()
    {
        var ask = Node("ask");
        ask.choices = new List<DialogueChoice> { Choice("hidden", "never", requiredFlag: "Key"), Choice("yes", "after", setFlag: "SaidYes") };
        var runner = Start("ask", ask, Node("after"), Node("never"));

        runner.Choose(0); // the first visible choice, not the node's first choice
        Assert.IsTrue(flags.Contains("SaidYes"));
        Assert.AreEqual("after", runner.Current.id);
    }

    [Test]
    public void AdvanceWaitsWhileChoicesAreVisible()
    {
        var ask = Node("ask", "skipped");
        ask.choices = new List<DialogueChoice> { Choice("ok") };
        var runner = Start("ask", ask, Node("skipped"));

        LogAssert.Expect(LogType.Warning, new Regex("waiting for a choice"));
        runner.Advance();
        Assert.AreEqual("ask", runner.Current.id);
    }

    [Test]
    public void MissingNodeLogsAnErrorAndEnds()
    {
        var runner = Start("a", Node("a", "nowhere"));
        LogAssert.Expect(LogType.Error, new Regex("no node 'nowhere'"));
        runner.Advance();
        Assert.IsTrue(runner.IsFinished);
    }

    [Test]
    public void FallbackLoopLogsAnErrorAndEnds()
    {
        var a = Node("a");
        a.requiredFlag = "Never";
        a.fallback = "b";
        var b = Node("b");
        b.requiredFlag = "Never";
        b.fallback = "a";

        LogAssert.Expect(LogType.Error, new Regex("more than 64"));
        Assert.IsTrue(Start("a", a, b).IsFinished);
    }

    [Test]
    public void BlankFlagsNeverReachGameState()
    {
        GameState.Reset();
        data.startNode = "a";
        var a = new DialogueNode { id = "a", requiredFlag = "", setFlag = " ", next = "" };
        a.choices = new List<DialogueChoice> { Choice("go", requiredFlag: "", setFlag: "") };
        data.nodes = new List<DialogueNode> { a };

        // GameState throws on blank names, so reaching it would fail the test.
        var runner = new DialogueRunner(data, GameState.HasFlag, GameState.SetFlag);
        runner.Start();
        Assert.AreEqual(1, runner.VisibleChoices.Count);
        runner.Choose(0);
        Assert.IsTrue(runner.IsFinished);
    }

    [Test]
    public void ChooseWithOutOfRangeIndexThrows()
    {
        var ask = Node("ask");
        ask.choices = new List<DialogueChoice> { Choice("ok") };
        var runner = Start("ask", ask);

        Assert.Throws<System.ArgumentOutOfRangeException>(() => runner.Choose(1));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => runner.Choose(-1));
    }
}
