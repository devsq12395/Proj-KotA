using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundsController : MonoBehaviour
{
    public static SoundsController I;
    public void Awake() { I = this; }

    bool IsSoundDebounceExempt(string soundId)
    { 
        if (string.IsNullOrEmpty(soundId)) return true;
        switch (soundId)
        {
            case "shoot-plasma-1": case "shoot-plasma-2": case "shoot-plasma-3":
                return true;
        }
        return false;
    }

    // Dedicated BGM audio source (assign in scene to the object named 'AudioSourceBGM')
    public AudioSource audioSourceBGM;
    // SFX prefab to instantiate per sound; must contain an AudioSource and SFXPlayer
    public GameObject sfxPrefab;


    public bool soundOn, musicOn;
    [Range(0f,1f)] public float musicVolume = 1f;
    [Range(0f,1f)] public float sfxVolume = 1f;

    private List<AudioClip> musicPlaylist = new List<AudioClip>();
    private int currentMusicIndex;
    private bool isPlayingMusic;
    private bool wasPausedByFocusLoss;

    private Dictionary<string, float> lastPlayedTime = new Dictionary<string, float>();
    private float soundCooldown;

    private Dictionary<string, int> currentSoundCounts = new Dictionary<string, int>();
    private int maxSoundInstances = 2;

    class OwnedSfx
    {
        public string soundId;
        public SFXPlayer player;
    }

    readonly Dictionary<int, List<OwnedSfx>> _ownedSfx = new Dictionary<int, List<OwnedSfx>>();

    void Start()
    {
        soundCooldown = 0.1f;

        // Load persisted volumes from SaveController (if present)
        LoadVolumesFromSave();

        // Initialize playlist from bgmGame if any clips are assigned in the Inspector
        if (DB_Sounds.I != null && DB_Sounds.I.bgmGame != null && DB_Sounds.I.bgmGame.Length > 0)
        {
            musicPlaylist.AddRange(DB_Sounds.I.bgmGame);
        }

        if (musicOn && musicPlaylist.Count > 0)
        {
            currentMusicIndex = 0;
            PlayCurrentMusic();
        }
    }

    void Update()
    {
        if (wasPausedByFocusLoss) return;

        if (musicOn && isPlayingMusic && audioSourceBGM != null && !audioSourceBGM.isPlaying && musicPlaylist.Count > 0)
        {
            // Advance to next track and loop back to start when we reach the end
            currentMusicIndex = (currentMusicIndex + 1) % musicPlaylist.Count;
            PlayCurrentMusic();
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            wasPausedByFocusLoss = true;
        }
        else
        {
            if (wasPausedByFocusLoss && isPlayingMusic && audioSourceBGM != null)
            {
                if (!audioSourceBGM.isPlaying)
                {
                    audioSourceBGM.UnPause();
                }
            }
            wasPausedByFocusLoss = false;
        }
    }

    AudioClip GetClip(string soundId)
    {
        if (DB_Sounds.I == null) return null;
        return DB_Sounds.I.GetClip(soundId);
    }

    public void PlaySoundInGame(string _sound, Vector2 _pos){
        // Ensure we have an audio source and a camera
        if (!EnsureAudioSource()) return;
        Camera cam = Camera.main;
        if (cam == null)
        {
            var go = GameObject.Find("Main Camera");
            if (go != null) cam = go.GetComponent<Camera>();
        }
        if (cam == null) { return; }

        // Convert world position to viewport space and check visibility (0..1 and in front of camera)
        Vector3 world = new Vector3(_pos.x, _pos.y, 0f);
        Vector3 vp = cam.WorldToViewportPoint(world);
        bool inView = (vp.z > 0f) && (vp.x >= 0f && vp.x <= 1f) && (vp.y >= 0f && vp.y <= 1f);
        if (!inView) { return; }

        // Play SFX at position using SFX prefab
        PlaySFXAt(_sound, _pos);
    }

    public SFXPlayer PlayLoopForOwner(string soundId, InGameObject owner)
    {
        if (owner == null) return null;
        if (!EnsureAudioSource()) return null;
        if (string.IsNullOrEmpty(soundId)) return null;

        int ownerId = owner.id;
        if (ownerId != 0 && _ownedSfx.TryGetValue(ownerId, out var list) && list != null)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var e = list[i];
                if (e == null || e.player == null)
                {
                    list.RemoveAt(i);
                    continue;
                }
                if (e.soundId != soundId) continue;
                if (e.player.source != null && e.player.source.isPlaying) return e.player;
                Destroy(e.player.gameObject);
                list.RemoveAt(i);
            }
            if (list.Count == 0) _ownedSfx.Remove(ownerId);
        }

        Vector2 pos = new Vector2(owner.transform.position.x, owner.transform.position.y);
        return PlaySoundInGameForOwner(soundId, owner, true);
    }

    public void StopLoopForOwner(string soundId, InGameObject owner)
    {
        StopSound(soundId, owner);
    }

    public SFXPlayer PlaySoundInGameForOwner(string soundId, InGameObject owner, bool loop = false)
    {
        if (owner == null) return null;
        if (!EnsureAudioSource()) return null;

        Camera cam = Camera.main;
        if (cam == null)
        {
            var go = GameObject.Find("Main Camera");
            if (go != null) cam = go.GetComponent<Camera>();
        }
        if (cam == null) { return null; }

        Vector2 pos = new Vector2(owner.transform.position.x, owner.transform.position.y);
        Vector3 world = new Vector3(pos.x, pos.y, 0f);
        Vector3 vp = cam.WorldToViewportPoint(world);
        bool inView = (vp.z > 0f) && (vp.x >= 0f && vp.x <= 1f) && (vp.y >= 0f && vp.y <= 1f);
        if (!inView) { return null; }

        return PlaySFXAt(soundId, pos, owner, loop);
    }

    public void PlaySound(string _sound)
    {
        if (!EnsureAudioSource()) return;

        // Debounce all SFX to prevent multiple instances within cooldown window
        if (!IsSoundDebounceExempt(_sound) && lastPlayedTime.ContainsKey(_sound) && Time.time - lastPlayedTime[_sound] < soundCooldown)
        {
            return;
        }

        // Check for "big-hit" sound instance limit
        if (_sound == "big-hit" && currentSoundCounts.ContainsKey(_sound) && currentSoundCounts[_sound] >= maxSoundInstances)
        {
            return;
        }

        AudioClip clipToPlay = GetClip(_sound);

        if (clipToPlay != null)
        {
            // Spawn SFX at camera position when no world position is provided
            Vector2 pos2D = Vector2.zero;
            var cam = Camera.main;
            if (cam != null)
            {
                var p = cam.transform.position;
                pos2D = new Vector2(p.x, p.y);
            }
            SpawnSFXInstance(clipToPlay, pos2D, Mathf.Clamp01(sfxVolume));
            lastPlayedTime[_sound] = Time.time;

            if (_sound == "big-hit")
            {
                if (!currentSoundCounts.ContainsKey(_sound))
                {
                    currentSoundCounts[_sound] = 0;
                }
                currentSoundCounts[_sound]++;
                StartCoroutine(RemoveSoundCount(_sound, clipToPlay.length));
            }
        }
        else
        {
            return;
        }
    }

    // Internal: plays an SFX with position (used by PlaySoundInGame)
    void PlaySFXAt(string soundId, Vector2 pos)
    {
        // Debounce to prevent multiple instances within cooldown window
        if (!IsSoundDebounceExempt(soundId) && lastPlayedTime.ContainsKey(soundId) && Time.time - lastPlayedTime[soundId] < soundCooldown)
        {
            return;
        }
        AudioClip clip = GetClip(soundId);
        if (clip == null)
        {
            return;
        }
        SpawnSFXInstance(clip, pos, Mathf.Clamp01(sfxVolume));
        lastPlayedTime[soundId] = Time.time;
    }

    SFXPlayer PlaySFXAt(string soundId, Vector2 pos, InGameObject owner, bool loop)
    {
        if (!IsSoundDebounceExempt(soundId) && lastPlayedTime.ContainsKey(soundId) && Time.time - lastPlayedTime[soundId] < soundCooldown)
        {
            return null;
        }
        AudioClip clip = GetClip(soundId);
        if (clip == null) return null;

        int ownerId = owner != null ? owner.id : 0;
        var inst = SpawnSFXInstance(clip, pos, Mathf.Clamp01(sfxVolume), loop);
        lastPlayedTime[soundId] = Time.time;
        if (inst != null && ownerId != 0)
        {
            if (!_ownedSfx.TryGetValue(ownerId, out var list) || list == null)
            {
                list = new List<OwnedSfx>();
                _ownedSfx[ownerId] = list;
            }
            list.Add(new OwnedSfx { soundId = soundId, player = inst });
        }
        return inst;
    }

    // Spawns the SFX prefab and instructs it to play the clip, auto-destroying after 10s
    void SpawnSFXInstance(AudioClip clip, Vector2 pos, float volume)
    {
        if (sfxPrefab == null)
        {
            return;
        }
        Vector3 world = new Vector3(pos.x, pos.y, -19.5f);
        var go = Instantiate(sfxPrefab, world, Quaternion.identity);
        var player = go.GetComponent<SFXPlayer>();
        if (player == null) player = go.AddComponent<SFXPlayer>();
        player.Init(clip, volume);
    }

    SFXPlayer SpawnSFXInstance(AudioClip clip, Vector2 pos, float volume, bool loop)
    {
        if (sfxPrefab == null)
        {
            return null;
        }
        Vector3 world = new Vector3(pos.x, pos.y, -19.5f);
        var go = Instantiate(sfxPrefab, world, Quaternion.identity);
        var player = go.GetComponent<SFXPlayer>();
        if (player == null) player = go.AddComponent<SFXPlayer>();
        if (player.source != null) player.source.loop = loop;
        if (loop)
        {
            player.lifeTime = 99999f;
        }
        player.Init(clip, volume);
        return player;
    }

    public void StopSound(string soundId, InGameObject owner)
    {
        if (owner == null) return;
        int ownerId = owner.id;
        if (!_ownedSfx.TryGetValue(ownerId, out var list) || list == null || list.Count == 0) return;

        for (int i = list.Count - 1; i >= 0; i--)
        {
            var e = list[i];
            if (e == null || e.player == null)
            {
                list.RemoveAt(i);
                continue;
            }
            if (!string.IsNullOrEmpty(soundId) && e.soundId != soundId) continue;

            if (e.player.source != null) e.player.source.Stop();
            Destroy(e.player.gameObject);
            list.RemoveAt(i);
        }

        if (list.Count == 0) _ownedSfx.Remove(ownerId);
    }

    public void StopAllSoundsForOwner(InGameObject owner)
    {
        if (owner == null) return;
        int ownerId = owner.id;
        if (!_ownedSfx.TryGetValue(ownerId, out var list) || list == null) return;

        for (int i = list.Count - 1; i >= 0; i--)
        {
            var e = list[i];
            if (e == null || e.player == null) continue;
            if (e.player.source != null) e.player.source.Stop();
            Destroy(e.player.gameObject);
        }
        _ownedSfx.Remove(ownerId);
    }

    private IEnumerator RemoveSoundCount(string _sound, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (currentSoundCounts.ContainsKey(_sound))
        {
            currentSoundCounts[_sound]--;
        }
    }

    // --- Simple music playlist API ---

    void PlayCurrentMusic()
    {
        if (!EnsureAudioSource())
        {
            isPlayingMusic = false;
            return;
        }

        if (!musicOn || musicPlaylist.Count == 0)
        {
            isPlayingMusic = false;
            return;
        }

        if (currentMusicIndex < 0 || currentMusicIndex >= musicPlaylist.Count)
        {
            currentMusicIndex = 0;
        }

        AudioClip clip = musicPlaylist[currentMusicIndex];
        if (clip == null)
        {
            isPlayingMusic = false;
            return;
        }

        audioSourceBGM.loop = false;
        audioSourceBGM.clip = clip;
        audioSourceBGM.volume = Mathf.Clamp01(musicVolume);
        audioSourceBGM.Play();
        isPlayingMusic = true;
    }

    public void AddMusicToPlaylist(string clipName)
    {
        if (string.IsNullOrEmpty(clipName)) return;

        AudioClip clip = FindMusicClipByName(clipName);
        if (clip == null) return;

        musicPlaylist.Add(clip);

        // If nothing is playing but music is enabled, start with this track
        if (musicOn && !isPlayingMusic)
        {
            currentMusicIndex = musicPlaylist.Count - 1;
            PlayCurrentMusic();
        }
    }

    // Replace the current playlist with a new ordered list of clip names and start playback.
    // Useful when switching from Lobby to Game so lobby BGM does not continue.
    public void SetMusicPlaylist(System.Collections.Generic.IEnumerable<string> clipNames)
    {
        StopAllMusic();
        musicPlaylist.Clear();
        if (clipNames != null)
        {
            foreach (var name in clipNames)
            {
                if (string.IsNullOrEmpty(name)) continue;
                var clip = FindMusicClipByName(name);
                if (clip != null)
                {
                    musicPlaylist.Add(clip);
                }
            }
        }

        if (musicOn && musicPlaylist.Count > 0)
        {
            currentMusicIndex = 0;
            PlayCurrentMusic();
        }
    }

    AudioClip FindMusicClipByName(string clipName)
    {
        if (DB_Sounds.I == null) return null;
        return DB_Sounds.I.FindMusicClipByName(clipName);
    }

    bool EnsureAudioSource()
    {
        if (audioSourceBGM != null) return true;

        // Prefer a scene object named 'AudioSourceBGM'
        var go = GameObject.Find("AudioSourceBGM");
        if (go != null) audioSourceBGM = go.GetComponent<AudioSource>();
        if (audioSourceBGM == null)
        {
            return false;
        }
        audioSourceBGM.loop = false;
        audioSourceBGM.volume = Mathf.Clamp01(musicVolume);
        return true;
    }

    // --- Volume Settings Integration ---
    public void LoadVolumesFromSave()
    {
        if (SaveController.I == null) { return; }
        // musicVolume
        object mv = SaveController.I.GetValue("settings.musicVolume");
        musicVolume = ConvertToFloat01(mv, musicVolume);
        // sfxVolume
        object sv = SaveController.I.GetValue("settings.sfxVolume");
        sfxVolume = ConvertToFloat01(sv, sfxVolume);

        // If source exists, immediately apply music volume
        if (audioSourceBGM != null)
        {
            audioSourceBGM.volume = Mathf.Clamp01(musicVolume);
        }
    }

    public void SetMusicVolume(float v)
    {
        musicVolume = Mathf.Clamp01(v);
        if (audioSourceBGM != null) audioSourceBGM.volume = musicVolume;
    }

    public void SetSFXVolume(float v)
    {
        sfxVolume = Mathf.Clamp01(v);
    }

    private float ConvertToFloat01(object val, float fallback)
    {
        if (val == null) return Mathf.Clamp01(fallback);
        try
        {
            if (val is float f) return Mathf.Clamp01(f);
            if (val is double d) return Mathf.Clamp01((float)d);
            if (val is int i) return Mathf.Clamp01(i);
            if (val is long l) return Mathf.Clamp01((float)l);
            if (val is string s)
            {
                if (float.TryParse(s, out var parsed)) return Mathf.Clamp01(parsed);
            }
        }
        catch {}
        return Mathf.Clamp01(fallback);
    }

    public void StopAllMusic()
    {
        isPlayingMusic = false;
        if (audioSourceBGM != null) audioSourceBGM.Stop();
    }

    // Backwards-compatible wrappers for existing code
    public void play_bgm(string unused = null)
    {
        if (!musicOn || musicPlaylist.Count == 0) return;
        currentMusicIndex = 0;
        PlayCurrentMusic();
    }

    public void stop_bgm()
    {
        StopAllMusic();
    }

    public void resume_bgm()
    {
        if (!musicOn || musicPlaylist.Count == 0) return;
        if (!isPlayingMusic)
        {
            PlayCurrentMusic();
        }
    }
}
