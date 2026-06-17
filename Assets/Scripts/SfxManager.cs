using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// SfxManager —— 纯代码合成音效
// 使用 AudioClip.Create 在运行时生成短音效片段，无需外部音频资源。
// =====================================================================
public class SfxManager : MonoBehaviour
{
    public static SfxManager Instance;

    readonly Dictionary<SfxKind, AudioClip> cache = new Dictionary<SfxKind, AudioClip>();
    AudioSource source;
    float masterVolume = 0.8f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        source = GetComponent<AudioSource>();
        if (!source) source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = 1f;
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
    }

    public float GetMasterVolume()
    {
        return masterVolume;
    }

    public void Play(SfxKind kind, float vol = 1f)
    {
        if (source == null || masterVolume <= 0.001f) return;
        if (!cache.TryGetValue(kind, out var clip) || clip == null)
        {
            clip = BuildClip(kind);
            cache[kind] = clip;
        }
        source.PlayOneShot(clip, Mathf.Clamp01(vol) * masterVolume);
    }

    AudioClip BuildClip(SfxKind kind)
    {
        const int sampleRate = 44100;
        float length = GetLength(kind);
        int samples = Mathf.Max(256, Mathf.CeilToInt(length * sampleRate));
        float[] data = new float[samples];

        switch (kind)
        {
            case SfxKind.Place: SynthBlip(data, sampleRate, 520f, 760f, 0.20f, Waveform.Sine, 0.7f); break;
            case SfxKind.Shoot: SynthBlip(data, sampleRate, 220f, 110f, 0.10f, Waveform.Square, 0.5f); break;
            case SfxKind.LaserHum: SynthPulse(data, sampleRate, 170f, 0.22f, 0.18f, 0.35f); break;
            case SfxKind.Hit: SynthNoiseHit(data, sampleRate, 0.09f, 0.45f); break;
            case SfxKind.Kill: SynthChordRise(data, sampleRate, 480f, 720f, 0.22f, 0.45f); break;
            case SfxKind.WaveStart: SynthArp(data, sampleRate, new[] { 220f, 330f, 440f }, 0.30f, 0.42f); break;
            case SfxKind.WaveClear: SynthArp(data, sampleRate, new[] { 392f, 523f, 659f, 784f }, 0.38f, 0.5f); break;
            case SfxKind.Error: SynthBlip(data, sampleRate, 190f, 130f, 0.16f, Waveform.Saw, 0.5f); break;
            case SfxKind.Sell: SynthBlip(data, sampleRate, 420f, 260f, 0.18f, Waveform.Sine, 0.5f); break;
            case SfxKind.Upgrade: SynthArp(data, sampleRate, new[] { 330f, 494f, 660f }, 0.26f, 0.45f); break;
            case SfxKind.Reflect: SynthPulse(data, sampleRate, 900f, 0.06f, 0.10f, 0.6f); break;
            case SfxKind.BossRoar: SynthRoar(data, sampleRate, 92f, 0.70f, 0.55f); break;
            case SfxKind.CoreHit: SynthNoiseHit(data, sampleRate, 0.26f, 0.65f); break;
            case SfxKind.CardDraw: SynthArp(data, sampleRate, new[] { 523f, 659f, 880f }, 0.34f, 0.45f); break;
            case SfxKind.CardPick: SynthArp(data, sampleRate, new[] { 440f, 554f, 659f, 880f }, 0.30f, 0.48f); break;
            default: SynthBlip(data, sampleRate, 440f, 440f, 0.12f, Waveform.Sine, 0.3f); break;
        }

        AudioClip clip = AudioClip.Create("sfx_" + kind, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    float GetLength(SfxKind kind)
    {
        switch (kind)
        {
            case SfxKind.BossRoar: return 0.75f;
            case SfxKind.WaveStart:
            case SfxKind.WaveClear:
            case SfxKind.CardDraw:
            case SfxKind.CardPick:
                return 0.42f;
            case SfxKind.CoreHit: return 0.26f;
            default: return 0.22f;
        }
    }

    enum Waveform { Sine, Square, Saw }

    void SynthBlip(float[] data, int rate, float startFreq, float endFreq, float decay, Waveform wave, float amp)
    {
        float phase = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)rate;
            float env = Mathf.Exp(-t / Mathf.Max(0.01f, decay));
            float freq = Mathf.Lerp(startFreq, endFreq, t / (data.Length / (float)rate));
            phase += 2f * Mathf.PI * freq / rate;
            data[i] = SampleWave(phase, wave) * env * amp;
        }
    }

    void SynthPulse(float[] data, int rate, float freq, float length, float decay, float amp)
    {
        float phase = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)rate;
            float env = Mathf.Clamp01(1f - t / Mathf.Max(0.01f, length));
            env *= Mathf.Exp(-t / Mathf.Max(0.01f, decay));
            phase += 2f * Mathf.PI * freq / rate;
            float pulse = Mathf.Sign(Mathf.Sin(phase)) * 0.5f + Mathf.Sin(phase * 2f) * 0.35f;
            data[i] = pulse * env * amp;
        }
    }

    void SynthNoiseHit(float[] data, int rate, float decay, float amp)
    {
        uint seed = 2463534242u;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)rate;
            float env = Mathf.Exp(-t / Mathf.Max(0.01f, decay));
            seed = seed * 1664525u + 1013904223u;
            float noise = ((seed >> 8) & 0xFFFF) / 32768f - 1f;
            data[i] = noise * env * amp;
        }
    }

    void SynthChordRise(float[] data, int rate, float a, float b, float decay, float amp)
    {
        float ph1 = 0f, ph2 = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)rate;
            float env = Mathf.Exp(-t / Mathf.Max(0.01f, decay));
            float f1 = Mathf.Lerp(a, b, t / (data.Length / (float)rate));
            float f2 = f1 * 1.25f;
            ph1 += 2f * Mathf.PI * f1 / rate;
            ph2 += 2f * Mathf.PI * f2 / rate;
            data[i] = (Mathf.Sin(ph1) + Mathf.Sin(ph2) * 0.8f) * env * amp * 0.5f;
        }
    }

    void SynthArp(float[] data, int rate, float[] notes, float noteLen, float amp)
    {
        float[] phases = new float[notes.Length];
        int steps = Mathf.Max(1, Mathf.CeilToInt(data.Length / Mathf.Max(1f, noteLen * rate)));
        for (int i = 0; i < data.Length; i++)
        {
            int idx = Mathf.Min(notes.Length - 1, Mathf.FloorToInt(i / Mathf.Max(1f, noteLen * rate)));
            float freq = notes[Mathf.Min(idx, notes.Length - 1)];
            phases[idx] += 2f * Mathf.PI * freq / rate;
            float localT = (i % Mathf.Max(1, Mathf.RoundToInt(noteLen * rate))) / (noteLen * rate);
            float env = Mathf.Exp(-localT * 4f);
            data[i] = Mathf.Sin(phases[idx]) * env * amp;
        }
    }

    void SynthRoar(float[] data, int rate, float baseFreq, float decay, float amp)
    {
        float phase = 0f;
        uint seed = 362436069u;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)rate;
            float env = Mathf.Exp(-t / Mathf.Max(0.01f, decay));
            float freq = baseFreq + Mathf.Sin(t * 18f) * 12f;
            phase += 2f * Mathf.PI * freq / rate;
            seed = seed * 1103515245u + 12345u;
            float noise = (((seed >> 16) & 0x7FFF) / 16384f - 1f) * 0.35f;
            float body = Mathf.Sin(phase) * 0.7f + Mathf.Sin(phase * 0.5f) * 0.4f;
            data[i] = (body + noise) * env * amp;
        }
    }

    float SampleWave(float phase, Waveform wave)
    {
        switch (wave)
        {
            case Waveform.Square: return Mathf.Sign(Mathf.Sin(phase));
            case Waveform.Saw:
                float wrapped = phase / (2f * Mathf.PI);
                return 2f * (wrapped - Mathf.Floor(wrapped + 0.5f));
            default: return Mathf.Sin(phase);
        }
    }
}
