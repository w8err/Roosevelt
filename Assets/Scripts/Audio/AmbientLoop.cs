using UnityEngine;

// A single looping ambience layer, e.g. a fluorescent hum or a distant drone. LabSceneBuilder
// creates one of these per sound entry in a layout's JSON and sets these fields by name via
// SerializedObject, so the field names below are a contract with it -- don't rename without
// telling it. Builds its own AudioSource in Awake so the builder never has to.
public class AmbientLoop : MonoBehaviour
{
    [SerializeField] AudioClip clip;
    [Range(0f, 1f)] [SerializeField] float volume = 0.5f;
    [Tooltip("false = 2D background, heard everywhere at the same volume (most reality/dream hums). true = a 3D point source that fades out between minDistance and maxDistance (e.g. a specific machine).")]
    [SerializeField] bool spatial;
    [SerializeField] float minDistance = 3f;
    [SerializeField] float maxDistance = 15f;

    AudioSource source;

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = spatial ? 1f : 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
    }

    void OnEnable()
    {
        if (clip != null) source.Play();
    }

    void OnDisable() => source.Stop();

    // Reapplied every frame (not just on Ambience.FadeTo) since Ambience.Volume itself changes
    // continuously during a fade.
    void Update() => source.volume = volume * Ambience.Volume;
}
