using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.AssetImporters;
using UnityEngine;

// Turns a .dialogue JSON file (UTF-8) into a read-only DialogueData asset.
// Broken JSON is an import error; dangling links and duplicate ids are warnings.
[ScriptedImporter(1, "dialogue")]
public sealed class DialogueImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        DialogueData data;
        try
        {
            data = DialogueData.FromJson(File.ReadAllText(ctx.assetPath));
            foreach (var problem in data.FindProblems()) ctx.LogImportWarning($"{ctx.assetPath}: {problem}");
            ValidateFlags(ctx.assetPath, data);
        }
        catch (ArgumentException e)
        {
            ctx.LogImportError($"{ctx.assetPath} is not valid dialogue JSON: {e.Message}");
            data = ScriptableObject.CreateInstance<DialogueData>();
        }
        data.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
        ctx.AddObjectToAsset("dialogue", data);
        ctx.SetMainObject(data);
    }

    static void ValidateFlags(string path, DialogueData data)
    {
        var usedFlags = new HashSet<string>();
        var list = data.nodes ?? new List<DialogueNode>();
        foreach (var node in list)
        {
            if (!string.IsNullOrEmpty(node.requiredFlag)) usedFlags.Add(node.requiredFlag);
            if (!string.IsNullOrEmpty(node.setFlag)) usedFlags.Add(node.setFlag);
            if (node.choices != null)
                foreach (var choice in node.choices)
                {
                    if (!string.IsNullOrEmpty(choice.requiredFlag)) usedFlags.Add(choice.requiredFlag);
                    if (!string.IsNullOrEmpty(choice.setFlag)) usedFlags.Add(choice.setFlag);
                }
        }

        var defined = new HashSet<string>(typeof(GameFlags).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)));

        var undefined = usedFlags.Where(f => !defined.Contains(f)).ToList();
        foreach (var flag in undefined)
            Debug.LogWarning($"[DialogueImporter] {path}: flag '{flag}' is used but not defined in GameFlags");
    }
}
