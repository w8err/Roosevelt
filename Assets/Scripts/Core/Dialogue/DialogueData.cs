using System;
using System.Collections.Generic;
using UnityEngine;

// One conversation, imported from a .dialogue JSON file by DialogueImporter.
// The JSON is the source; the asset is rebuilt on every import, so edit the file.
public sealed class DialogueData : ScriptableObject
{
    public string startNode;
    public List<DialogueNode> nodes = new();

    // Throws ArgumentException when the JSON cannot be parsed.
    public static DialogueData FromJson(string json)
    {
        var data = CreateInstance<DialogueData>();
        try
        {
            JsonUtility.FromJsonOverwrite(json, data);
        }
        catch
        {
            DestroyImmediate(data);
            throw;
        }
        return data;
    }

    // Authoring mistakes worth a warning on import: no start, duplicate ids, links to nowhere.
    public List<string> FindProblems()
    {
        var problems = new List<string>();
        var list = nodes ?? new List<DialogueNode>();
        var ids = new HashSet<string>();
        foreach (var node in list)
        {
            if (string.IsNullOrEmpty(node.id)) problems.Add("A node has no id.");
            else if (!ids.Add(node.id)) problems.Add($"Node id '{node.id}' is used twice.");
        }

        if (string.IsNullOrEmpty(startNode)) problems.Add("startNode is empty.");
        else if (!ids.Contains(startNode)) problems.Add($"startNode '{startNode}' is not a node.");

        foreach (var node in list)
        {
            CheckLink(node.id, "fallback", node.fallback);
            CheckLink(node.id, "next", node.next);
            if (node.choices == null) continue;
            for (var i = 0; i < node.choices.Count; i++) CheckLink(node.id, $"choice {i} next", node.choices[i].next);
        }
        return problems;

        void CheckLink(string from, string field, string target)
        {
            if (!string.IsNullOrEmpty(target) && !ids.Contains(target))
                problems.Add($"Node '{from}' {field} points to missing node '{target}'.");
        }
    }
}

[Serializable]
public sealed class DialogueNode
{
    public string id;
    public string speaker;
    public string text;
    // Entering without this flag jumps straight to fallback, or ends the dialogue if there is none.
    public string requiredFlag;
    public string fallback;
    // Turned on when the node is shown.
    public string setFlag;
    // Where Advance goes when no choice is visible. Empty ends the dialogue.
    public string next;
    public List<DialogueChoice> choices = new();
}

[Serializable]
public sealed class DialogueChoice
{
    public string text;
    // The choice is hidden unless this flag is on.
    public string requiredFlag;
    // Turned on when the choice is picked.
    public string setFlag;
    public string next;
}
