using UnityEngine;

// Simple component to play a one-shot SFX on its own AudioSource and self-destroy after a duration
public class SFXPlayer : MonoBehaviour
{
    [Tooltip("AudioSource used to play this SFX. If null, one will be added on Awake.")]
    public AudioSource source;

    [Tooltip("Lifetime in seconds before this GameObject is destroyed.")]
    public float lifeTime = 10f;

    float timer;

    void Awake()
    {
        if (source == null)
        {
            source = gameObject.GetComponent<AudioSource>();
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
            }
        }
        // Ensure it's configured for one-shot 2D sound by default; can be overridden per prefab
        source.loop = false;
        source.playOnAwake = false;
        source.spatialBlend = 0f; // 2D sound
    }

    public void Init(AudioClip clip, float volume)
    {
        if (source == null) Awake();
        if (clip == null)
        {
            return;
        }
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.Play();
        timer = 0f;
        // Optionally, we could reduce lifetime to clip length if it's shorter than lifeTime
        // but requirement says 10s duration; we will still auto-destroy at lifeTime.
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= lifeTime)
        {
            Destroy(gameObject);
        }
    }
}
