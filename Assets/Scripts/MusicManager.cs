using UnityEngine;

// =====================================================================
// MusicManager —— 纯代码背景音乐
// 使用流式 AudioClip 生成鼓点、贝斯、主旋律和环境铺底。
// =====================================================================
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance;

    const int SampleRate = 44100;
    readonly int[] leadSeq = { 69, 72, 76, 72, 67, 72, 74, 72, 69, 72, 76, 79, 76, 72, 67, 64 };
    readonly int[] bossLeadSeq = { 76, 79, 83, 81, 79, 76, 72, 74, 76, 79, 83, 88, 83, 79, 76, 72 };
    readonly int[] bassSeq = { 45, 45, 48, 48, 43, 43, 40, 40 };
    readonly int[] bossBassSeq = { 40, 40, 43, 43, 38, 38, 35, 35 };

    AudioSource source;
    AudioClip clip;
    long sampleCursor;
    float leadPhase;
    float bassPhase;
    float padPhaseA;
    float padPhaseB;
    float volume = 0.55f;
    float targetIntensity;
    float smoothedIntensity;
    bool bossMode;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        source = GetComponent<AudioSource>();
        if (!source) source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        source.ignoreListenerVolume = false;
        source.volume = 1f;

        clip = AudioClip.Create("procedural_music", SampleRate * 2, 2, SampleRate, true, OnAudioRead, OnAudioSetPosition);
        source.clip = clip;
    }

    void OnEnable()
    {
        if (source != null && source.clip != null && !source.isPlaying)
            source.Play();
    }

    void Start()
    {
        if (source != null && source.clip != null && !source.isPlaying)
            source.Play();
    }

    public void SetVolume(float value)
    {
        volume = Mathf.Clamp01(value);
        if (source != null) source.mute = volume <= 0.0001f;
    }

    public float GetVolume()
    {
        return volume;
    }

    public void SetBossMode(bool isBoss)
    {
        bossMode = isBoss;
        targetIntensity = bossMode ? 1f : 0.28f;
    }

    void Update()
    {
        targetIntensity = Mathf.Lerp(targetIntensity, bossMode ? 1f : 0.28f, Time.unscaledDeltaTime * 3f);
    }

    void OnAudioSetPosition(int newPosition)
    {
        sampleCursor = newPosition;
        leadPhase = 0f;
        bassPhase = 0f;
        padPhaseA = 0f;
        padPhaseB = 0f;
    }

    void OnAudioRead(float[] data)
    {
        if (volume <= 0.0001f)
        {
            for (int i = 0; i < data.Length; i++) data[i] = 0f;
            sampleCursor += data.Length / 2;
            return;
        }

        float bpm = bossMode ? 160f : 118f;
        float secondsPerBeat = 60f / bpm;
        float beatSubdivision = secondsPerBeat / 4f;
        var activeLead = bossMode ? bossLeadSeq : leadSeq;
        var activeBass = bossMode ? bossBassSeq : bassSeq;

        for (int i = 0; i < data.Length; i += 2)
        {
            double t = sampleCursor / (double)SampleRate;
            float step16Pos = (float)(t / beatSubdivision);
            float step8Pos = (float)(t / (secondsPerBeat / 2f));
            int step16 = Mathf.FloorToInt(step16Pos) % activeLead.Length;
            int step8 = Mathf.FloorToInt(step8Pos) % activeBass.Length;
            float beatPos = (float)(t / secondsPerBeat);
            float beatFrac = beatPos - Mathf.Floor(beatPos);
            float stepFrac = step16Pos - Mathf.Floor(step16Pos);

            smoothedIntensity += (targetIntensity - smoothedIntensity) * 0.0009f;

            float leadFreq = MidiToFreq(activeLead[step16]);
            float bassFreq = MidiToFreq(activeBass[step8]);
            float padFreqA = MidiToFreq(activeBass[step8] + 12);
            float padFreqB = MidiToFreq(activeLead[(step16 / 4) * 4] - 12);

            leadPhase += 2f * Mathf.PI * leadFreq / SampleRate;
            bassPhase += 2f * Mathf.PI * bassFreq / SampleRate;
            padPhaseA += 2f * Mathf.PI * padFreqA / SampleRate;
            padPhaseB += 2f * Mathf.PI * padFreqB / SampleRate;

            float leadEnv = Mathf.Exp(-stepFrac * 3.8f) * (bossMode ? 0.22f : 0.17f);
            float lead = (Mathf.Sin(leadPhase) * 0.8f + Mathf.Sin(leadPhase * 2f) * 0.2f) * leadEnv;

            float bass = (Mathf.Sin(bassPhase) * 0.75f + Mathf.Sign(Mathf.Sin(bassPhase)) * 0.25f) * 0.18f;
            float pad = (Mathf.Sin(padPhaseA) + Mathf.Sin(padPhaseB)) * 0.055f;
            float ambience = Mathf.Sin((float)t * 0.8f) * 0.015f + Mathf.Sin((float)t * 0.31f) * 0.02f;

            float kick = beatFrac < 0.16f
                ? Mathf.Sin((1f - beatFrac / 0.16f) * 18f) * (1f - beatFrac / 0.16f) * 0.30f
                : 0f;
            float hatFrac = step8Pos - Mathf.Floor(step8Pos);
            float hat = hatFrac < 0.055f ? (0.045f - hatFrac) * 4f : 0f;

            float sample = (lead + bass + pad + ambience) * (0.72f + smoothedIntensity * 0.28f);
            sample += kick * (0.5f + smoothedIntensity * 0.5f);
            sample += hat * (bossMode ? 0.7f : 0.35f);
            sample = Mathf.Clamp(sample * volume, -0.85f, 0.85f);

            data[i] = sample;
            data[i + 1] = sample;
            sampleCursor++;
        }
    }

    float MidiToFreq(int midi)
    {
        return 440f * Mathf.Pow(2f, (midi - 69) / 12f);
    }
}
