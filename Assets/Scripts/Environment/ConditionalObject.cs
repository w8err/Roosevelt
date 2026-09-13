using UnityEngine;

// Turns this GameObject on or off based on GameState: Day range, a required/forbidden flag and a
// required/forbidden item. All AND together; a blank field or -1 day bound never excludes. Leaving
// every field at its default (minDay = maxDay = -1, no flag/item names) always keeps the object on.
//
// The trap: SetActive(false) disables this component along with everything else on the object, so a
// naive OnEnable/OnDisable subscription to GameState.Changed un-subscribes itself the moment the
// condition first fails — nothing is left listening to hear the flag come back later, and the object
// never returns. Unity's automatic message dispatch (Update, OnEnable, OnDisable, ...) does stop on a
// disabled component, but a plain C# event subscription is just a delegate in a list: invoking it
// doesn't check whether the target's GameObject is active. So instead of standing up a separate
// always-alive registry (or splitting this into a parent that never disables and a child that does),
// this component subscribes once in Awake and only unsubscribes in OnDestroy — the subscription (and
// therefore the ability to reactivate itself) survives every SetActive(false) it does to itself.
public class ConditionalObject : MonoBehaviour
{
    [SerializeField] int minDay = -1;
    [SerializeField] int maxDay = -1;
    [SerializeField] string requiredFlag;
    [SerializeField] string forbiddenFlag;
    [SerializeField] string requiredItem;
    [SerializeField] string forbiddenItem;

    void Awake()
    {
        GameState.Changed += Evaluate;
        Evaluate(); // covers both a fresh scene load and one that just restored a save
    }

    void OnDestroy() => GameState.Changed -= Evaluate;

    void Evaluate() => gameObject.SetActive(IsSatisfied());

    bool IsSatisfied()
    {
        if (minDay >= 0 && GameState.Day < minDay) return false;
        if (maxDay >= 0 && GameState.Day > maxDay) return false;
        if (!string.IsNullOrEmpty(requiredFlag) && !GameState.HasFlag(requiredFlag)) return false;
        if (!string.IsNullOrEmpty(forbiddenFlag) && GameState.HasFlag(forbiddenFlag)) return false;
        if (!string.IsNullOrEmpty(requiredItem) && !GameState.HasItem(requiredItem)) return false;
        if (!string.IsNullOrEmpty(forbiddenItem) && GameState.HasItem(forbiddenItem)) return false;
        return true;
    }
}
