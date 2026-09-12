using System;
using System.IO;
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
}
