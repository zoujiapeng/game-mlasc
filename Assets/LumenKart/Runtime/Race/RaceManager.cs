using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LumenKart
{
    [DisallowMultipleComponent]
    public sealed class RaceManager : MonoBehaviour
    {
        [SerializeField] private TrackPath trackPath;
        [SerializeField] private RaceHUD hud;
        [SerializeField, Min(1)] private int totalLaps = 3;
        [SerializeField] private AudioSource uiAudioSource;

        private readonly List<RaceProgress> racers = new();
        private readonly List<RaceProgress> finishers = new();
        private readonly List<RaceProgress> rankingBuffer = new();
        private RaceProgress player;
        private float raceTime;
        private bool raceStarted;
        private bool raceFinished;
        private bool paused;

        public static RaceManager Instance { get; private set; }
        public TrackPath TrackPath => trackPath;
        public int TotalLaps => totalLaps;
        public float RaceTime => raceTime;
        public bool RaceStarted => raceStarted;
        public bool RaceFinished => raceFinished;
        public bool IsPaused => paused;
        public IReadOnlyList<RaceProgress> Racers => racers;
        public IReadOnlyList<RaceProgress> Finishers => finishers;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Application.targetFrameRate = 120;
            Time.fixedDeltaTime = 1f / 60f;
            EnsureAudioSource();
        }

        private IEnumerator Start()
        {
            yield return null;
            RaceCheckpoint.RebuildRegistry();
            foreach (RaceProgress racer in racers)
            {
                racer.Controller.SetControlsEnabled(false);
            }

            yield return new WaitForSecondsRealtime(0.65f);
            yield return CountdownStep("3", false);
            yield return CountdownStep("2", false);
            yield return CountdownStep("1", false);

            raceStarted = true;
            foreach (RaceProgress racer in racers)
            {
                racer.Controller.SetControlsEnabled(true);
            }

            if (hud != null)
            {
                hud.SetCountdown("GO");
            }

            uiAudioSource.PlayOneShot(ProceduralAudioFactory.CreateCountdown(true), 0.38f);
            yield return new WaitForSecondsRealtime(0.72f);
            if (hud != null)
            {
                hud.SetCountdown(string.Empty);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                RestartRace();
                return;
            }

            if (raceStarted && !raceFinished && !paused)
            {
                raceTime += Time.deltaTime;
            }

            RefreshRanking();
            UpdateHud();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            Time.timeScale = 1f;
        }

        public void Configure(TrackPath path, RaceHUD raceHud, int laps, AudioSource uiAudio = null)
        {
            trackPath = path;
            hud = raceHud;
            totalLaps = Mathf.Max(1, laps);
            uiAudioSource = uiAudio;
            EnsureAudioSource();
        }

        public void Register(RaceProgress racer)
        {
            if (racer == null || racers.Contains(racer))
            {
                return;
            }

            racers.Add(racer);
            if (!raceStarted && racer.Controller != null)
            {
                racer.Controller.SetControlsEnabled(false);
            }

            if (racer.IsPlayer)
            {
                player = racer;
            }
        }

        public void NotifyFinished(RaceProgress racer)
        {
            if (racer == null || finishers.Contains(racer))
            {
                return;
            }

            finishers.Add(racer);
            racer.SetFinishPosition(finishers.Count, raceTime);
            racer.Controller.SetControlsEnabled(false);

            if (racer.IsPlayer)
            {
                raceFinished = true;
                if (hud != null)
                {
                    hud.ShowResults(finishers, racers.Count, racer.FinishPosition, raceTime);
                }
            }
        }

        public float GetCatchupMultiplier(RaceProgress racer)
        {
            if (racer == null || racer.IsPlayer || player == null || !raceStarted)
            {
                return 1f;
            }

            float sampleGap = player.ProgressScore - racer.ProgressScore;
            float normalizedGap = trackPath != null && trackPath.Count > 0
                ? sampleGap / trackPath.Count
                : 0f;

            if (normalizedGap > 0f)
            {
                return 1f + Mathf.Min(0.075f, normalizedGap * 0.026f);
            }

            return 1f - Mathf.Min(0.025f, -normalizedGap * 0.012f);
        }

        public RaceProgress FindNearestOpponent(
            RaceProgress source,
            Vector3 origin,
            Vector3 forward,
            float maximumDistance,
            float minimumForwardDot)
        {
            RaceProgress best = null;
            float bestDistance = maximumDistance * maximumDistance;

            foreach (RaceProgress candidate in racers)
            {
                if (candidate == null || candidate == source || candidate.HasFinished)
                {
                    continue;
                }

                Vector3 offset = candidate.transform.position - origin;
                float squareDistance = offset.sqrMagnitude;
                if (squareDistance >= bestDistance)
                {
                    continue;
                }

                if (offset.sqrMagnitude > 0.001f && Vector3.Dot(forward, offset.normalized) < minimumForwardDot)
                {
                    continue;
                }

                best = candidate;
                bestDistance = squareDistance;
            }

            return best;
        }

        public void TogglePause()
        {
            if (!raceStarted || raceFinished)
            {
                return;
            }

            paused = !paused;
            Time.timeScale = paused ? 0f : 1f;
            if (hud != null)
            {
                hud.SetPaused(paused);
            }
        }

        public void RestartRace()
        {
            Time.timeScale = 1f;
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                return;
            }

            if (activeScene.buildIndex >= 0)
            {
                SceneManager.LoadScene(activeScene.buildIndex);
            }
            else
            {
                SceneManager.LoadScene(activeScene.name);
            }
        }

        private IEnumerator CountdownStep(string label, bool high)
        {
            if (hud != null)
            {
                hud.SetCountdown(label);
            }

            uiAudioSource.PlayOneShot(ProceduralAudioFactory.CreateCountdown(high), 0.3f);
            yield return new WaitForSecondsRealtime(0.92f);
        }

        private void RefreshRanking()
        {
            if (racers.Count == 0)
            {
                return;
            }

            rankingBuffer.Clear();
            rankingBuffer.AddRange(racers.Where(racer => racer != null));
            rankingBuffer.Sort(CompareRacers);

            for (int i = 0; i < rankingBuffer.Count; i++)
            {
                rankingBuffer[i].SetRacePosition(i + 1);
            }
        }

        private static int CompareRacers(RaceProgress left, RaceProgress right)
        {
            if (left.HasFinished || right.HasFinished)
            {
                if (left.HasFinished && right.HasFinished)
                {
                    return left.FinishPosition.CompareTo(right.FinishPosition);
                }

                return left.HasFinished ? -1 : 1;
            }

            int score = right.ProgressScore.CompareTo(left.ProgressScore);
            if (score != 0)
            {
                return score;
            }

            return left.DistanceToNextCheckpoint.CompareTo(right.DistanceToNextCheckpoint);
        }

        private void UpdateHud()
        {
            if (hud == null || player == null)
            {
                return;
            }

            KartItemInventory inventory = player.GetComponent<KartItemInventory>();
            hud.SetRaceState(
                player.RacePosition,
                racers.Count,
                player.CurrentLap,
                totalLaps,
                raceTime,
                inventory != null ? inventory.DisplayName : "EMPTY",
                player.Controller.DriftCharge01,
                player.Controller.DriftTier,
                player.IsDrivingWrongWay);
        }

        private void EnsureAudioSource()
        {
            if (uiAudioSource != null)
            {
                return;
            }

            uiAudioSource = GetComponent<AudioSource>();
            if (uiAudioSource == null)
            {
                uiAudioSource = gameObject.AddComponent<AudioSource>();
            }

            uiAudioSource.playOnAwake = false;
            uiAudioSource.spatialBlend = 0f;
        }
    }
}
