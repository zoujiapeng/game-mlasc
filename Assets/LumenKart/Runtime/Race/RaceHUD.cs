using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LumenKart
{
    [DisallowMultipleComponent]
    public sealed class RaceHUD : MonoBehaviour
    {
        [SerializeField] private Text positionText;
        [SerializeField] private Text lapText;
        [SerializeField] private Text timerText;
        [SerializeField] private Text countdownText;
        [SerializeField] private Text itemText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text driftTierText;
        [SerializeField] private Image driftFill;
        [SerializeField] private GameObject resultsPanel;
        [SerializeField] private Text resultsText;
        [SerializeField] private GameObject pausePanel;

        public void Configure(
            Text position,
            Text lap,
            Text timer,
            Text countdown,
            Text item,
            Text status,
            Text driftTier,
            Image driftProgress,
            GameObject resultPanel,
            Text resultLabel,
            GameObject pausedPanel)
        {
            positionText = position;
            lapText = lap;
            timerText = timer;
            countdownText = countdown;
            itemText = item;
            statusText = status;
            driftTierText = driftTier;
            driftFill = driftProgress;
            resultsPanel = resultPanel;
            resultsText = resultLabel;
            pausePanel = pausedPanel;

            resultsPanel?.SetActive(false);
            pausePanel?.SetActive(false);
        }

        public void SetRaceState(
            int position,
            int racerCount,
            int lap,
            int lapCount,
            float time,
            string itemName,
            float driftCharge,
            int driftTier,
            bool wrongWay)
        {
            if (positionText != null)
            {
                positionText.text = $"{position}<size=42> / {racerCount}</size>";
            }

            if (lapText != null)
            {
                lapText.text = $"LAP  {lap} / {lapCount}";
            }

            if (timerText != null)
            {
                timerText.text = FormatTime(time);
            }

            if (itemText != null)
            {
                itemText.text = itemName;
            }

            if (driftFill != null)
            {
                driftFill.fillAmount = Mathf.Clamp01(driftCharge);
            }

            if (driftTierText != null)
            {
                driftTierText.text = driftTier > 0 ? $"TURBO {driftTier}" : "DRIFT";
            }

            if (statusText != null)
            {
                statusText.text = wrongWay ? "WRONG WAY" : string.Empty;
            }
        }

        public void SetCountdown(string value)
        {
            if (countdownText != null)
            {
                countdownText.text = value;
                countdownText.gameObject.SetActive(!string.IsNullOrEmpty(value));
            }
        }

        public void SetPaused(bool value)
        {
            pausePanel?.SetActive(value);
        }

        public void ShowResults(
            IReadOnlyList<RaceProgress> finishOrder,
            int racerCount,
            int playerPosition,
            float playerTime)
        {
            if (resultsPanel != null)
            {
                resultsPanel.SetActive(true);
            }

            if (resultsText == null)
            {
                return;
            }

            System.Text.StringBuilder builder = new();
            builder.AppendLine(playerPosition == 1 ? "CIRCUIT CLEAR" : $"FINISHED  {playerPosition} / {racerCount}");
            builder.AppendLine($"TIME  {FormatTime(playerTime)}");
            builder.AppendLine();

            int visible = Mathf.Min(finishOrder.Count, 8);
            for (int i = 0; i < visible; i++)
            {
                RaceProgress racer = finishOrder[i];
                builder.AppendLine($"{i + 1,2}.  {racer.RacerName,-12}  {FormatTime(racer.FinishTime)}");
            }

            if (finishOrder.Count < racerCount)
            {
                builder.AppendLine("… remaining racers still running");
            }

            builder.AppendLine();
            builder.Append("PRESS R TO RESTART");
            resultsText.text = builder.ToString();
        }

        private static string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float remainder = seconds - minutes * 60f;
            return $"{minutes:00}:{remainder:00.00}";
        }
    }
}
