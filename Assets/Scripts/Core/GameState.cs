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

    public static int Day { get; private set; }
    // Raised after any real change to Day or the flags, including Reset and Restore.
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

    public static void AdvanceDay()
    {
        if (Day == LastDay) throw new InvalidOperationException($"Day {LastDay} is the last day.");
        Day++;
        Changed?.Invoke();
    }

    public static SaveData Capture()
    {
        var list = new List<string>(flags);
        list.Sort(StringComparer.Ordinal); // keeps the save file stable between saves
        return new SaveData { day = Day, flags = list };
    }

    // Validates everything first so a bad save leaves the current state untouched.
    public static void Restore(SaveData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.day < FirstDay || data.day > LastDay)
            throw new ArgumentOutOfRangeException(nameof(data), $"Day {data.day} is outside {FirstDay}..{LastDay}.");
        var restored = new HashSet<string>();
        if (data.flags != null)
            foreach (var flag in data.flags) restored.Add(Checked(flag));

        Day = data.day;
        flags.Clear();
        flags.UnionWith(restored);
        Changed?.Invoke();
    }

    // A blank name is always a wiring mistake, such as an unset serialized field.
    static string Checked(string flag) =>
        string.IsNullOrWhiteSpace(flag) ? throw new ArgumentException("Flag name is empty.", nameof(flag)) : flag;
}
