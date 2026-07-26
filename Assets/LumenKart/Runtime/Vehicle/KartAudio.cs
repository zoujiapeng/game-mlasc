using System;
using UnityEngine;

namespace LumenKart
{
    [DisallowMultipleComponent]
    public sealed class KartAudio : MonoBehaviour
    {
        [SerializeField] private KartController controller;
        [SerializeField] private AudioSource engineSource;
        [SerializeField] private AudioSource surfaceSource;
        [SerializeField] private AudioSource oneShotSource;

        private AudioClip boostClip;
        private AudioClip impactClip;

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponent<KartController>();
            }

            EnsureSources();
            engineSource.clip = ProceduralAudioFactory.CreateEngineLoop();
            surfaceSource.clip = ProceduralAudioFactory.CreateNoiseLoop("Tire texture", 0.7f, 1138);
            boostClip = ProceduralAudioFactory.CreateWhoosh();
            impactClip = ProceduralAudioFactory.CreateImpact();

            engineSource.loop = true;
            surfaceSource.loop = true;
            engineSource.Play();
            surfaceSource.Play();

            if (controller != null)
            {
                controller.BoostStarted += HandleBoost;
                controller.SpinOutStarted += HandleSpinOut;
            }
        }

        private void OnDestroy()
        {
            if (controller != null)
            {
                controller.BoostStarted -= HandleBoost;
                controller.SpinOutStarted -= HandleSpinOut;
            }
        }

        private void Update()
        {
            if (controller == null || engineSource == null || surfaceSource == null)
            {
                return;
            }

            float speed = controller.NormalizedSpeed;
            engineSource.pitch = Mathf.Lerp(0.72f, 1.72f, speed) + (controller.IsBoosting ? 0.12f : 0f);
            engineSource.volume = Mathf.Lerp(0.08f, 0.32f, speed) + controller.CurrentInput.Throttle * 0.05f;

            float skid = controller.IsDrifting
                ? Mathf.Lerp(0.08f, 0.35f, speed)
                : Mathf.Abs(controller.CurrentSteering) * speed * 0.035f;
            surfaceSource.pitch = Mathf.Lerp(0.82f, 1.25f, speed);
            surfaceSource.volume = Mathf.MoveTowards(surfaceSource.volume, skid, Time.deltaTime * 1.8f);
        }

        public void Configure(KartController kartController)
        {
            controller = kartController;
        }

        public void PlayImpact(float strength)
        {
            if (oneShotSource != null && impactClip != null)
            {
                oneShotSource.PlayOneShot(impactClip, Mathf.Clamp01(strength) * 0.35f);
            }
        }

        private void HandleBoost(float duration)
        {
            if (oneShotSource != null && boostClip != null)
            {
                oneShotSource.PlayOneShot(boostClip, Mathf.Clamp01(0.18f + duration * 0.12f));
            }
        }

        private void HandleSpinOut()
        {
            if (oneShotSource != null && impactClip != null)
            {
                oneShotSource.PlayOneShot(impactClip, 0.3f);
            }
        }

        private void EnsureSources()
        {
            if (engineSource == null)
            {
                engineSource = CreateSource("Engine Audio", true);
            }

            if (surfaceSource == null)
            {
                surfaceSource = CreateSource("Surface Audio", true);
            }

            if (oneShotSource == null)
            {
                oneShotSource = CreateSource("One Shot Audio", false);
            }
        }

        private AudioSource CreateSource(string sourceName, bool loop)
        {
            GameObject child = new(sourceName);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 1f;
            source.minDistance = 4f;
            source.maxDistance = 42f;
            source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }
    }

    public static class ProceduralAudioFactory
    {
        private const int SampleRate = 22050;
        private static AudioClip engineLoop;
        private static AudioClip noiseLoop;
        private static AudioClip whoosh;
        private static AudioClip impact;
        private static AudioClip countdownHigh;
        private static AudioClip countdownLow;

        public static AudioClip CreateEngineLoop()
        {
            if (engineLoop != null)
            {
                return engineLoop;
            }

            int length = SampleRate;
            float[] data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                float phase = t * Mathf.PI * 2f;
                float pulse = Mathf.Sin(phase * 46f) * 0.42f;
                pulse += Mathf.Sin(phase * 92f + 0.4f) * 0.2f;
                pulse += Mathf.Sin(phase * 23f) * 0.1f;
                data[i] = (float)Math.Tanh(pulse * 1.35f) * 0.3f;
            }

            engineLoop = MakeClip("Procedural engine", data);
            return engineLoop;
        }

        public static AudioClip CreateNoiseLoop(string clipName, float seconds, int seed)
        {
            if (noiseLoop != null)
            {
                return noiseLoop;
            }

            int length = Mathf.Max(512, Mathf.RoundToInt(SampleRate * seconds));
            float[] data = new float[length];
            System.Random random = new(seed);
            float filtered = 0f;
            for (int i = 0; i < length; i++)
            {
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                filtered = Mathf.Lerp(filtered, white, 0.18f);
                data[i] = filtered * 0.18f;
            }

            noiseLoop = MakeClip(clipName, data);
            return noiseLoop;
        }

        public static AudioClip CreateWhoosh()
        {
            if (whoosh != null)
            {
                return whoosh;
            }

            int length = Mathf.RoundToInt(SampleRate * 0.55f);
            float[] data = new float[length];
            System.Random random = new(7021);
            float filtered = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)length;
                float envelope = Mathf.Sin(t * Mathf.PI) * (1f - t * 0.25f);
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                filtered = Mathf.Lerp(filtered, white, Mathf.Lerp(0.04f, 0.3f, t));
                float tone = Mathf.Sin(t * t * 380f) * 0.22f;
                data[i] = (filtered * 0.5f + tone) * envelope * 0.45f;
            }

            whoosh = MakeClip("Procedural boost", data);
            return whoosh;
        }

        public static AudioClip CreateImpact()
        {
            if (impact != null)
            {
                return impact;
            }

            int length = Mathf.RoundToInt(SampleRate * 0.28f);
            float[] data = new float[length];
            System.Random random = new(1914);
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)length;
                float envelope = Mathf.Exp(-t * 9f);
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                float tone = Mathf.Sin(i / (float)SampleRate * Mathf.PI * 2f * 92f);
                data[i] = (noise * 0.55f + tone * 0.45f) * envelope * 0.5f;
            }

            impact = MakeClip("Procedural impact", data);
            return impact;
        }

        public static AudioClip CreateCountdown(bool high)
        {
            if (high && countdownHigh != null)
            {
                return countdownHigh;
            }

            if (!high && countdownLow != null)
            {
                return countdownLow;
            }

            float frequency = high ? 720f : 510f;
            int length = Mathf.RoundToInt(SampleRate * 0.18f);
            float[] data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = Mathf.Exp(-t * 18f);
                data[i] = Mathf.Sin(t * Mathf.PI * 2f * frequency) * envelope * 0.34f;
            }

            AudioClip clip = MakeClip(high ? "Countdown high" : "Countdown low", data);
            if (high)
            {
                countdownHigh = clip;
            }
            else
            {
                countdownLow = clip;
            }

            return clip;
        }

        private static AudioClip MakeClip(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
