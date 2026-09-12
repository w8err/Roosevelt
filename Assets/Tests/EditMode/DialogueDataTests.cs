using System;
using NUnit.Framework;
using Object = UnityEngine.Object;

public class DialogueDataTests
{
    const string LobbyJson = @"{
  ""startNode"": ""again"",
  ""nodes"": [
    { ""id"": ""again"", ""speaker"": ""연구원"", ""text"": ""또 뵙네요."", ""requiredFlag"": ""TalkedToLobbyNPC"", ""fallback"": ""first"" },
    { ""id"": ""first"", ""speaker"": ""연구원"", ""text"": ""고맙네. 당신의 헌신."", ""setFlag"": ""TalkedToLobbyNPC"",
      ""choices"": [ { ""text"": ""네"", ""next"": ""again"" }, { ""text"": ""누구시죠?"", ""requiredFlag"": ""Remembered"" } ] }
  ]
}";

    DialogueData data;

    [TearDown]
    public void TearDown()
    {
        if (data != null) Object.DestroyImmediate(data);
    }

    [Test]
    public void FromJsonReadsNodesChoicesAndKoreanText()
    {
        data = DialogueData.FromJson(LobbyJson);
        Assert.AreEqual("again", data.startNode);
        Assert.AreEqual(2, data.nodes.Count);

        var again = data.nodes[0];
        Assert.AreEqual("연구원", again.speaker);
        Assert.AreEqual(GameFlags.TalkedToLobbyNPC, again.requiredFlag);
        Assert.AreEqual("first", again.fallback);

        var first = data.nodes[1];
        Assert.AreEqual("고맙네. 당신의 헌신.", first.text);
        Assert.AreEqual(GameFlags.TalkedToLobbyNPC, first.setFlag);
        Assert.AreEqual(2, first.choices.Count);
        Assert.AreEqual("again", first.choices[0].next);
        Assert.AreEqual("Remembered", first.choices[1].requiredFlag);
    }

    [Test]
    public void FromJsonRejectsBrokenJson()
    {
        Assert.Throws<ArgumentException>(() => DialogueData.FromJson("{ \"startNode\": "));
    }

    [Test]
    public void WellFormedDialogueHasNoProblems()
    {
        data = DialogueData.FromJson(LobbyJson);
        CollectionAssert.IsEmpty(data.FindProblems());
    }

    [Test]
    public void FindProblemsReportsDuplicatesAndBrokenLinks()
    {
        data = DialogueData.FromJson(@"{
  ""startNode"": ""missing"",
  ""nodes"": [
    { ""id"": ""a"", ""next"": ""nowhere"" },
    { ""id"": ""a"", ""fallback"": ""gone"", ""choices"": [ { ""next"": ""lost"" } ] }
  ]
}");
        var problems = string.Join("\n", data.FindProblems());
        StringAssert.Contains("used twice", problems);
        StringAssert.Contains("startNode 'missing'", problems);
        StringAssert.Contains("'nowhere'", problems);
        StringAssert.Contains("'gone'", problems);
        StringAssert.Contains("'lost'", problems);
    }

    [Test]
    public void FindProblemsReportsEmptyStartNode()
    {
        data = DialogueData.FromJson(@"{
  ""startNode"": """",
  ""nodes"": [ { ""id"": ""a"" } ]
}");
        var problems = string.Join("\n", data.FindProblems());
        StringAssert.Contains("startNode is empty.", problems);
    }

    [Test]
    public void FindProblemsReportsNodeWithNoId()
    {
        data = DialogueData.FromJson(@"{
  ""startNode"": ""a"",
  ""nodes"": [ { ""id"": ""a"" }, { ""text"": ""no id"" } ]
}");
        var problems = string.Join("\n", data.FindProblems());
        StringAssert.Contains("A node has no id.", problems);
    }
}
