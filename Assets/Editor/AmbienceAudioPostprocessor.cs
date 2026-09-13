using System.IO;
using UnityEditor;
using UnityEngine;

// Forces import settings for every audio file under Assets/Audio/Ambience/ so ambience loops
// don't sit in memory as raw PCM. Scoped to that one folder deliberately: Assets/Audio/ also
// holds the user's own music files (e.g. a placeholder .mp3, gitignored so it isn't in the
// repo), which need their own settings, not forced mono/Vorbis. Same shape as
// ArtTexturePostprocessor.cs: settings are reapplied on every (re)import, so a value changed by
// hand in the Inspector snaps back on the next reimport -- same trap as the texture
// postprocessor. Edit this script instead.
public class AmbienceAudioPostprocessor : AssetPostprocessor
{
    const string AudioRoot = "Assets/Audio/Ambience/";
    // Below this, decompressing the whole clip into memory is cheap; at or above it (our
    // longer ambience loops), Streaming avoids holding minutes of decoded audio resident for
    // as long as the loop keeps playing.
    const double StreamingThresholdSeconds = 10.0;

    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(AudioRoot)) return;

        var importer = (AudioImporter)assetImporter;
        importer.forceToMono = true;
        importer.loadInBackground = true;

        var settings = importer.defaultSampleSettings;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = 0.45f; // ambience is low-frequency and loops under other sound; doesn't need more
        // Long loops stream from disk instead of decompressing fully into memory; short
        // one-shots decompress once and stay resident, which is cheap for a couple of seconds
        // of audio and avoids Streaming's per-play I/O for something played often.
        settings.loadType = ReadWavSeconds(assetPath) >= StreamingThresholdSeconds
            ? AudioClipLoadType.Streaming
            : AudioClipLoadType.CompressedInMemory;
        importer.defaultSampleSettings = settings;
    }

    // Reads duration straight from the WAV header (fmt + data chunks) instead of from the
    // decoded AudioClip: OnPreprocessAudio runs before Unity has decoded anything, so
    // AudioClip.length isn't available yet at the point import settings must be chosen.
    static double ReadWavSeconds(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);
            if (new string(reader.ReadChars(4)) != "RIFF") return 0.0;
            reader.ReadInt32();
            if (new string(reader.ReadChars(4)) != "WAVE") return 0.0;

            int channels = 0, sampleRate = 0, bitsPerSample = 0;
            long dataBytes = 0;
            while (stream.Position <= stream.Length - 8)
            {
                var id = new string(reader.ReadChars(4));
                var size = reader.ReadInt32();
                if (id == "fmt ")
                {
                    var chunkEnd = stream.Position + size;
                    reader.ReadInt16(); // format tag
                    channels = reader.ReadInt16();
                    sampleRate = reader.ReadInt32();
                    reader.ReadInt32(); // byte rate
                    reader.ReadInt16(); // block align
                    bitsPerSample = reader.ReadInt16();
                    stream.Position = chunkEnd;
                }
                else if (id == "data")
                {
                    dataBytes = size;
                    break;
                }
                else
                {
                    stream.Position += size;
                }
            }
            if (sampleRate <= 0 || channels <= 0 || bitsPerSample <= 0) return 0.0;
            return dataBytes / (double)(sampleRate * channels * (bitsPerSample / 8));
        }
        catch
        {
            // Not a WAV, or a header we don't recognize: fall through to the short-clip default.
            return 0.0;
        }
    }
}
