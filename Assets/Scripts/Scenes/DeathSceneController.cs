using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DeathSceneController : MonoBehaviour
{
    public enum ChipKind { BestFloor, BiggestHit, TimeSurvived }

    [System.Serializable]
    public class ChipEntry
    {
        public ChipKind kind;
        public RectTransform root;
        public TextMeshProUGUI valueText;
        public CanvasGroup canvasGroup;
    }

    [SerializeField] private List<ChipEntry> chipEntries = new();
    [SerializeField] private float chipFadeDuration = 0.22f;
    [SerializeField] private float chipStagger = 0.12f;

    private void Start()
    {
        PopulateRecap();
        StartCoroutine(RevealChips());
    }

    public void RetryGame()
    {
        AudioManager.Instance?.PlayUiButton();
        SceneManager.LoadScene("GameplayLoop");
    }

    public void GoToMainMenu()
    {
        AudioManager.Instance?.PlayUiButton();
        SceneManager.LoadScene("MenuScene");
    }

    private void PopulateRecap()
    {
        if (RunStatsTracker.Instance == null) return;

        int bestFloor = Mathf.Max(0, RunStatsTracker.Instance.HighestFloorIndex + 1);
        int biggestHit = Mathf.RoundToInt(RunStatsTracker.Instance.BiggestHit);
        float survivedSeconds = RunStatsTracker.Instance.TotalTimeSeconds;
        int mins = Mathf.FloorToInt(survivedSeconds / 60f);
        int secs = Mathf.FloorToInt(survivedSeconds % 60f);

        foreach (var entry in chipEntries)
        {
            if (entry == null || entry.valueText == null) continue;

            entry.valueText.text = entry.kind switch
            {
                ChipKind.BestFloor => bestFloor.ToString(),
                ChipKind.BiggestHit => biggestHit.ToString(),
                ChipKind.TimeSurvived => $"{mins:00}:{secs:00}",
                _ => string.Empty
            };

            if (entry.canvasGroup != null) entry.canvasGroup.alpha = 0f;
        }
    }

    private IEnumerator RevealChips()
    {
        foreach (var entry in chipEntries)
        {
            if (entry == null) continue;
            yield return FadeIn(entry.canvasGroup);
            yield return new WaitForSecondsRealtime(chipStagger);
        }
    }

    private IEnumerator FadeIn(CanvasGroup cg)
    {
        if (cg == null) yield break;

        float dur = Mathf.Max(0.02f, chipFadeDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Clamp01(t / dur);
            yield return null;
        }
        cg.alpha = 1f;
    }
}
