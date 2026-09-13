using UnityEngine;

// Fires a random one-shot (a creak, an unknown noise) from clips at random intervals -- e.g.
// Dream_00's Wood_Creak/Unknown sets. LabSceneBuilder creates one of these per sound entry in a
// layout's JSON and sets these fields by name via SerializedObject, so the field names below
// are a contract with it -- don't rename without telling it.
public class RandomSoundEmitter : MonoBehaviour
{
    [SerializeField] AudioClip[] clips;
    [Tooltip("Seconds between one-shots, picked randomly in this range each time.")]
    [SerializeField] float intervalMin = 8f;
    [SerializeField] float intervalMax = 20f;
    [Range(0f, 1f)] [SerializeField] float volume = 0.6f;
    [Tooltip("0 = always play from this object's own position (e.g. a specific creaking tree). Above 0 = play from a random point within this many meters of the player each time, so the sound seems to come from a random nearby direction (e.g. Unknown one-shots).")]
    [SerializeField] float radius;
    [Tooltip("3D falloff, same shape as AmbientLoop: full volume up to minDistance, linearly down to silent at maxDistance. Unity's default AudioSource.PlayClipAtPoint falloff (minDistance 1, logarithmic) made distant one-shots much quieter than intended, e.g. Wood_Creak at 9m barely audible at volume 0.4.")]
    [SerializeField] float minDistance = 3f;
    [SerializeField] float maxDistance = 15f;

    AudioSource source;
    float nextFireTime;

    void Awake()
    {
        // Only actually plays through when radius == 0 (fixed position); built unconditionally
        // since it's cheap and keeps that case's Update() below simple. Unlike the radius > 0
        // temp source in PlayAtPoint, this one keeps tracking Ambience.Volume continuously, so a
        // fixed-position one-shot fades correctly even if a fade happens mid-playback.
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
    }

    void OnEnable() => ScheduleNext();

    void Update()
    {
        source.volume = Ambience.Volume;
        if (clips == null || clips.Length == 0) return;
        if (Time.time < nextFireTime) return;
        Fire();
        ScheduleNext();
    }

    void ScheduleNext() => nextFireTime = Time.time + Random.Range(intervalMin, intervalMax);

    void Fire()
    {
        var clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;

        if (radius <= 0f)
        {
            // source.volume above already tracks Ambience.Volume every frame; PlayOneShot's
            // own volume argument is a scale multiplied on top of it.
            source.PlayOneShot(clip, volume);
            return;
        }

        var player = FindAnyObjectByType<FirstPersonController>();
        var pos = player != null ? player.transform.position + Random.insideUnitSphere * radius : transform.position;
        PlayAtPoint(clip, pos, volume * Ambience.Volume, minDistance, maxDistance);
    }

    // AudioSource.PlayClipAtPoint doesn't expose rolloff/distance settings (it uses Unity's
    // default minDistance 1 + logarithmic falloff), so this is our own equivalent with the same
    // linear falloff as AmbientLoop, destroyed once the clip finishes. Its volume is captured
    // once at the moment it starts rather than re-read from Ambience.Volume afterward -- fine
    // for a one-shot a couple of seconds long.
    static void PlayAtPoint(AudioClip clip, Vector3 position, float volume, float minDistance, float maxDistance)
    {
        var go = new GameObject($"OneShot_{clip.name}");
        go.transform.position = position;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = minDistance;
        src.maxDistance = maxDistance;
        src.Play();
        Object.Destroy(go, clip.length);
    }
}
