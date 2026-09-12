using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// A single save slot holding Day and flags. Loading only restores GameState;
// the caller is responsible for starting the player in the Reality dormitory.
public static class SaveSystem
{
    public static string DefaultPath => Path.Combine(Application.persistentDataPath, "save.json");
    public static bool HasSave => File.Exists(DefaultPath);

    public static void Save() => Save(DefaultPath);
    public static bool Load() => Load(DefaultPath);

    public static void Save(string path) => File.WriteAllText(path, JsonUtility.ToJson(GameState.Capture(), true));

    // False when there is no file. A damaged file throws rather than silently starting over.
    public static bool Load(string path)
    {
        if (!File.Exists(path)) return false;
        GameState.Restore(JsonUtility.FromJson<SaveData>(File.ReadAllText(path)));
        return true;
    }
}

[Serializable]
public sealed class SaveData
{
    public int day;
    public List<string> flags = new();
}
