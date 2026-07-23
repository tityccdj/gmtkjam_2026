using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class Sound
{
    public string name;
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0.1f, 3f)] public float pitch = 1f;
    public bool loop = false;
}

public class AudioManager : Singleton<AudioManager>
{
    private const string SoundsResourcesPath = "sounds";

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;
    
    [Header("Sound Library")]
    [SerializeField] private Sound[] sounds;
    
    [Header("Volume Settings")]
    [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;
    
    [Header("Fade Settings")]
    [SerializeField] private float fadeDuration = 1f;
    
    private Dictionary<string, Sound> soundDictionary;
    private List<AudioSource> sfxSourcePool;
    private readonly Dictionary<string, AudioSource> loopSources = new Dictionary<string, AudioSource>();
    private readonly Dictionary<AudioSource, string> activeSourceSounds = new Dictionary<AudioSource, string>();
    private string currentMusicSoundName;
    private float currentMusicVolumeMultiplier = 1f;
    private const int POOL_SIZE = 10;
    
    protected override void Awake()
    {
        base.Awake();
        
        // Initialize audio sources if not assigned
        if (musicSource == null)
        {
            GameObject musicObj = new GameObject("MusicSource");
            musicObj.transform.SetParent(transform);
            musicSource = musicObj.AddComponent<AudioSource>();
            musicSource.loop = true;
        }
        
        if (sfxSource == null)
        {
            GameObject sfxObj = new GameObject("SFXSource");
            sfxObj.transform.SetParent(transform);
            sfxSource = sfxObj.AddComponent<AudioSource>();
        }
        
        // Initialize sound dictionary
        soundDictionary = new Dictionary<string, Sound>();
        foreach (Sound sound in sounds)
        {
            if (!string.IsNullOrWhiteSpace(sound.name) && sound.clip != null && !soundDictionary.ContainsKey(sound.name))
            {
                soundDictionary.Add(sound.name, sound);
            }
        }

        AudioClip[] loadedClips = Resources.LoadAll<AudioClip>(SoundsResourcesPath);
        foreach (AudioClip clip in loadedClips)
        {
            if (clip == null || soundDictionary.ContainsKey(clip.name))
            {
                continue;
            }

            soundDictionary.Add(clip.name, new Sound
            {
                name = clip.name,
                clip = clip,
                volume = 1f,
                pitch = 1f,
                loop = false
            });
        }
        
        // Initialize SFX pool
        sfxSourcePool = new List<AudioSource>();
        for (int i = 0; i < POOL_SIZE; i++)
        {
            GameObject poolObj = new GameObject($"SFXPool_{i}");
            poolObj.transform.SetParent(transform);
            AudioSource source = poolObj.AddComponent<AudioSource>();
            sfxSourcePool.Add(source);
        }
        
        ApplyVolumeSettings();
    }
    
    #region Music Control
    
    /// <summary>
    /// Play background music by name
    /// </summary>
    public void PlayMusic(string soundName, bool fadeIn = false)
    {
        PlayMusic(soundName, 1f, true, fadeIn);
    }

    public void PlayMusic(string soundName, float volumeMultiplier, bool loop = true, bool fadeIn = false)
    {
        if (soundDictionary.TryGetValue(soundName, out Sound sound))
        {
            currentMusicSoundName = soundName;
            currentMusicVolumeMultiplier = Mathf.Clamp01(volumeMultiplier);

            if (fadeIn)
            {
                musicSource.volume = 0;
                musicSource.clip = sound.clip;
                musicSource.pitch = sound.pitch;
                musicSource.loop = loop;
                musicSource.Play();
                FadeMusic(musicVolume * masterVolume * sound.volume * currentMusicVolumeMultiplier, fadeDuration);
            }
            else
            {
                musicSource.clip = sound.clip;
                musicSource.volume = musicVolume * masterVolume * sound.volume * currentMusicVolumeMultiplier;
                musicSource.pitch = sound.pitch;
                musicSource.loop = loop;
                musicSource.Play();
            }
        }
        else
        {
            Debug.LogWarning($"Sound '{soundName}' not found!");
        }
    }
    
    /// <summary>
    /// Stop music
    /// </summary>
    public void StopMusic(bool fadeOut = false)
    {
        currentMusicSoundName = null;
        currentMusicVolumeMultiplier = 1f;
        if (fadeOut)
        {
            FadeMusic(0f, fadeDuration, () => musicSource.Stop());
        }
        else
        {
            musicSource.Stop();
        }
    }
    
    /// <summary>
    /// Pause music
    /// </summary>
    public void PauseMusic()
    {
        musicSource.Pause();
    }
    
    /// <summary>
    /// Resume music
    /// </summary>
    public void ResumeMusic()
    {
        musicSource.UnPause();
    }
    
    /// <summary>
    /// Fade music to target volume
    /// </summary>
    private void FadeMusic(float targetVolume, float duration, System.Action onComplete = null)
    {
        LeanTween.cancel(musicSource.gameObject);
        LeanTween.value(musicSource.gameObject, musicSource.volume, targetVolume, duration)
            .setOnUpdate((float val) => musicSource.volume = val)
            .setOnComplete(onComplete);
    }
    
    #endregion
    
    #region SFX Control
    
    /// <summary>
    /// Play sound effect by name
    /// </summary>
    public void PlaySFX(string soundName)
    {
        if (soundDictionary.TryGetValue(soundName, out Sound sound))
        {
            AudioSource source = GetAvailableSource();
            ConfigureSource(source, sound, soundName, sound.pitch);
            source.Play();
        }
        else
        {
            Debug.LogWarning($"Sound '{soundName}' not found!");
        }
    }
    
    /// <summary>
    /// Play sound effect with random pitch variation
    /// </summary>
    public void PlaySFXWithPitchVariation(string soundName, float pitchVariation = 0.1f)
    {
        if (soundDictionary.TryGetValue(soundName, out Sound sound))
        {
            AudioSource source = GetAvailableSource();
            ConfigureSource(source, sound, soundName, sound.pitch + Random.Range(-pitchVariation, pitchVariation));
            source.Play();
        }
        else
        {
            Debug.LogWarning($"Sound '{soundName}' not found!");
        }
    }
    
    /// <summary>
    /// Play one shot sound effect (doesn't interrupt current sound)
    /// </summary>
    public void PlaySFXOneShot(string soundName)
    {
        if (soundDictionary.TryGetValue(soundName, out Sound sound))
        {
            sfxSource.PlayOneShot(sound.clip, sfxVolume * masterVolume * sound.volume);
        }
        else
        {
            Debug.LogWarning($"Sound '{soundName}' not found!");
        }
    }

    public void PlayLoop(string soundName, string channelKey)
    {
        if (string.IsNullOrWhiteSpace(channelKey))
        {
            channelKey = soundName;
        }

        if (!soundDictionary.TryGetValue(soundName, out Sound sound))
        {
            Debug.LogWarning($"Sound '{soundName}' not found!");
            return;
        }

        if (!loopSources.TryGetValue(channelKey, out AudioSource source) || source == null)
        {
            GameObject loopObject = new GameObject($"Loop_{channelKey}");
            loopObject.transform.SetParent(transform);
            source = loopObject.AddComponent<AudioSource>();
            loopSources[channelKey] = source;
        }

        if (source.isPlaying && source.clip == sound.clip)
        {
            UpdateLoopSourceVolume(source, soundName);
            return;
        }

        ConfigureSource(source, sound, soundName, sound.pitch);
        source.loop = true;
        source.Play();
    }

    public void StopLoop(string channelKey)
    {
        if (string.IsNullOrWhiteSpace(channelKey))
        {
            return;
        }

        if (!loopSources.TryGetValue(channelKey, out AudioSource source) || source == null)
        {
            return;
        }

        source.Stop();
        activeSourceSounds.Remove(source);
    }
    
    /// <summary>
    /// Stop all sound effects
    /// </summary>
    public void StopAllSFX()
    {
        sfxSource.Stop();
        foreach (AudioSource source in sfxSourcePool)
        {
            source.Stop();
        }

        foreach (AudioSource source in loopSources.Values.Where(source => source != null))
        {
            source.Stop();
        }

        activeSourceSounds.Clear();
    }
    
    /// <summary>
    /// Get available audio source from pool
    /// </summary>
    private AudioSource GetAvailableSource()
    {
        foreach (AudioSource source in sfxSourcePool)
        {
            if (!source.isPlaying)
            {
                return source;
            }
        }
        return sfxSource; // Fallback to main SFX source
    }
    
    #endregion
    
    #region Volume Control
    
    /// <summary>
    /// Set master volume
    /// </summary>
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        ApplyVolumeSettings();
    }
    
    /// <summary>
    /// Set music volume
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        ApplyVolumeSettings();
    }
    
    /// <summary>
    /// Set SFX volume
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        ApplyVolumeSettings();
    }
    
    /// <summary>
    /// Apply volume settings to audio sources
    /// </summary>
    private void ApplyVolumeSettings()
    {
        if (musicSource != null)
        {
            if (!string.IsNullOrWhiteSpace(currentMusicSoundName) && soundDictionary.TryGetValue(currentMusicSoundName, out Sound musicSound))
            {
                musicSource.volume = musicVolume * masterVolume * musicSound.volume * currentMusicVolumeMultiplier;
            }
            else
            {
                musicSource.volume = musicVolume * masterVolume;
            }
        }
        if (sfxSource != null)
        {
            sfxSource.volume = sfxVolume * masterVolume;
        }

        foreach (KeyValuePair<AudioSource, string> pair in activeSourceSounds)
        {
            if (pair.Key == null)
            {
                continue;
            }

            UpdateLoopSourceVolume(pair.Key, pair.Value);
        }
    }
    
    /// <summary>
    /// Get current master volume
    /// </summary>
    public float GetMasterVolume() => masterVolume;
    
    /// <summary>
    /// Get current music volume
    /// </summary>
    public float GetMusicVolume() => musicVolume;
    
    /// <summary>
    /// Get current SFX volume
    /// </summary>
    public float GetSFXVolume() => sfxVolume;
    
    #endregion
    
    #region Utility
    
    /// <summary>
    /// Check if music is playing
    /// </summary>
    public bool IsMusicPlaying() => musicSource.isPlaying;
    
    /// <summary>
    /// Mute/unmute all audio
    /// </summary>
    public void SetMute(bool mute)
    {
        AudioListener.volume = mute ? 0 : 1;
    }

    private void ConfigureSource(AudioSource source, Sound sound, string soundName, float pitch)
    {
        source.clip = sound.clip;
        source.volume = sfxVolume * masterVolume * sound.volume;
        source.pitch = pitch;
        source.loop = sound.loop;
        activeSourceSounds[source] = soundName;
    }

    private void UpdateLoopSourceVolume(AudioSource source, string soundName)
    {
        if (source == null || string.IsNullOrWhiteSpace(soundName))
        {
            return;
        }

        if (soundDictionary.TryGetValue(soundName, out Sound sound))
        {
            source.volume = sfxVolume * masterVolume * sound.volume;
        }
    }
    
    #endregion
}
