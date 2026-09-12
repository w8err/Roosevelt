using System;
using System.Collections.Generic;
using UnityEngine;

// Steps through a DialogueData. Plain C# so tests can drive it with their own flag set;
// in the game pass GameState.HasFlag and GameState.SetFlag. Blank flag names mean
// "no flag" and never reach the callbacks.
public sealed class DialogueRunner
{
    // More automatic jumps than this in a row means the fallbacks loop.
    const int MaxJumps = 64;

    readonly DialogueData data;
    readonly Func<string, bool> hasFlag;
    readonly Action<string> setFlag;
    readonly Dictionary<string, DialogueNode> byId = new();
    readonly List<DialogueChoice> visibleChoices = new();

    // Null before Start and after the dialogue ends.
    public DialogueNode Current { get; private set; }
    public IReadOnlyList<DialogueChoice> VisibleChoices => visibleChoices;
    public bool IsFinished { get; private set; }

    public DialogueRunner(DialogueData data, Func<string, bool> hasFlag, Action<string> setFlag)
    {
        this.data = data != null ? data : throw new ArgumentNullException(nameof(data));
        this.hasFlag = hasFlag ?? throw new ArgumentNullException(nameof(hasFlag));
        this.setFlag = setFlag ?? throw new ArgumentNullException(nameof(setFlag));
        if (data.nodes == null) return;
        // The first node with an id wins; the importer warns about duplicates.
        foreach (var node in data.nodes)
            if (!string.IsNullOrEmpty(node.id) && !byId.ContainsKey(node.id)) byId.Add(node.id, node);
    }

    public void Start()
    {
        IsFinished = false;
        Enter(data.startNode);
    }

    // Moves on from a node that shows no choices.
    public void Advance()
    {
        if (IsFinished || Current == null) return;
        if (visibleChoices.Count > 0)
        {
            Debug.LogWarning($"[Dialogue] {data.name}: '{Current.id}' is waiting for a choice.");
            return;
        }
        Enter(Current.next);
    }

    // index is into VisibleChoices, not the node's full choice list.
    public void Choose(int index)
    {
        if (IsFinished || Current == null) return;
        if (index < 0 || index >= visibleChoices.Count) throw new ArgumentOutOfRangeException(nameof(index));
        var choice = visibleChoices[index];
        SetFlag(choice.setFlag);
        Enter(choice.next);
    }

    void Enter(string id)
    {
        for (var jumps = 0; ; jumps++)
        {
            if (string.IsNullOrEmpty(id))
            {
                Finish();
                return;
            }
            if (jumps > MaxJumps)
            {
                Fail($"jumped more than {MaxJumps} times in a row at '{id}'; the fallbacks loop.");
                return;
            }
            if (!byId.TryGetValue(id, out var node))
            {
                Fail($"there is no node '{id}'.");
                return;
            }
            if (!HasFlag(node.requiredFlag))
            {
                id = node.fallback;
                continue;
            }

            Current = node;
            SetFlag(node.setFlag);
            visibleChoices.Clear();
            if (node.choices != null)
                foreach (var choice in node.choices)
                    if (HasFlag(choice.requiredFlag)) visibleChoices.Add(choice);
            return;
        }
    }

    bool HasFlag(string flag) => string.IsNullOrWhiteSpace(flag) || hasFlag(flag);

    void SetFlag(string flag)
    {
        if (!string.IsNullOrWhiteSpace(flag)) setFlag(flag);
    }

    void Fail(string message)
    {
        Debug.LogError($"[Dialogue] {data.name}: {message}");
        Finish();
    }

    void Finish()
    {
        Current = null;
        visibleChoices.Clear();
        IsFinished = true;
    }
}
