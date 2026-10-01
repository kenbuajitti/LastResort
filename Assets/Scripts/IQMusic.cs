using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

// Session-only choice: each fresh launch is silent; scene changes keep the player.
public sealed class IQMusic : MonoBehaviour
{
    static IQMusic instance;
    AudioSource source;
    public bool SoundEnabled { get; private set; }
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern int IQAudioIsRunning();
    [DllImport("__Internal")] static extern void IQAudioResume();
#endif
    public bool IsAudible
    {
        get
        {
            if (!SoundEnabled || source == null || !source.isPlaying || source.mute
                || source.volume <= 0 || AudioListener.pause || AudioListener.volume <= 0) return false;
#if UNITY_WEBGL && !UNITY_EDITOR
            return IQAudioIsRunning() != 0;
#else
            return true;
#endif
        }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; }

    public static IQMusic GetPlayer()
    {
        if (instance == null) new GameObject("StoryIQ Music").AddComponent<IQMusic>();
        return instance;
    }
    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        SoundEnabled = false;
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0;
        source.volume = .12f;
        source.clip = Resources.Load<AudioClip>("IQAudio/Puzzling");
        if (source.clip == null) Debug.LogError("Missing IQAudio/Puzzling music asset.");
    }
    // Called only by the speaker button. Menu/gameplay gestures never enable music.
    public void ToggleSound()
    {
        if (SoundEnabled)
        {
            SoundEnabled = false;
            source.Pause();
            return;
        }
        if (source.clip == null) return;
        SoundEnabled = true;
#if UNITY_WEBGL && !UNITY_EDITOR
        IQAudioResume();
#endif
        source.UnPause();
        if (!source.isPlaying) source.Play();
    }
    void OnDestroy() { if (instance == this) instance = null; }
}
