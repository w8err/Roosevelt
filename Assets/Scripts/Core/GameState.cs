using System;
using System.Collections.Generic;
using UnityEngine;

// Progress carried across Reality and the Dream_0X scenes. Whether the player is
// awake or dreaming is not stored here; the loaded scene already says that.
public static class GameState
{
    public const int FirstDay = 0;
    public const int LastDay = 5;

    static readonly HashSet<string> flags = new();
    // What the player is currently carrying, not their progress — kept separate from flags so a
    // future inventory UI can enumerate exactly this and nothing else.
    static readonly HashSet<string> items = new();

    public static int Day { get; private set; }
    // Raised after any real change to Day, the flags or the items, including Reset and Restore.
    public static event Action Changed;

    // Domain reload is off on entering play mode, so statics would keep the last session's values.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForPlay()
    {
        Changed = null;
        Reset();
    }

    public static void Reset()
    {
        Day = FirstDay;
        flags.Clear();
        items.Clear();
        Changed?.Invoke();
    }

    public static bool HasFlag(string flag) => flags.Contains(Checked(flag));

    public static void SetFlag(string flag)
    {
        if (flags.Add(Checked(flag))) Changed?.Invoke();
    }

    public static void ClearFlag(string flag)
    {
        if (flags.Remove(Checked(flag))) Changed?.Invoke();
    }

    public static bool HasItem(string item) => items.Contains(Checked(item));

    public static void AddItem(string item)
    {
        if (items.Add(Checked(item))) Changed?.Invoke();
    }

    public static void RemoveItem(string item)
    {
        if (items.Remove(Checked(item))) Changed?.Invoke();
    }

    // For a future inventory UI to enumerate. Order is not guaranteed; sort at the call site if needed.
    public static IReadOnlyCollection<string> Items => items;

    public static void AdvanceDay()
    {
        if (Day == LastDay) throw new InvalidOperationException($"Day {LastDay} is the last day.");
        Day++;
        Changed?.Invoke();
    }

    public static SaveData Capture()
    {
        var flagList = new List<string>(flags);
        flagList.Sort(StringComparer.Ordinal); // keeps the save file stable between saves
        var itemList = new List<string>(items);
        itemList.Sort(StringComparer.Ordinal);
        return new SaveData { day = Day, flags = flagList, items = itemList };
    }

    // Validates everything first so a bad save leaves the current state untouched.
    public static void Restore(SaveData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.day < FirstDay || data.day > LastDay)
            throw new ArgumentOutOfRangeException(nameof(data), $"Day {data.day} is outside {FirstDay}..{LastDay}.");
        var restoredFlags = new HashSet<string>();
        if (data.flags != null)
            foreach (var flag in data.flags) restoredFlags.Add(Checked(flag));
        var restoredItems = new HashSet<string>();
        if (data.items != null)
            foreach (var item in data.items) restoredItems.Add(Checked(item));

        Day = data.day;
        flags.Clear();
        flags.UnionWith(restoredFlags);
        items.Clear();
        items.UnionWith(restoredItems);
        Changed?.Invoke();
    }

    // A blank name is always a wiring mistake, such as an unset serialized field.
    static string Checked(string name) =>
        string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Flag or item name is empty.", nameof(name)) : name;
}
