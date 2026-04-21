using System.Collections.Generic;
using UnityEngine;

public class UpgradeUIHandler : MonoBehaviour
{
    public static UpgradeUIHandler Instance { get; private set; }

    [SerializeField] private GameObject upgradeContainer;
    [SerializeField] private GameObject upgradeDisplayPrefab;
    [SerializeField] private GameObject upgradeCanvas;
    [SerializeField] private bool pauseWhileShown = true;
    [SerializeField] private float cardStaggerSeconds = 0.08f;
    [SerializeField] private float cardFadeSeconds = 0.22f;

    private List<GameObject> _upgradeDisplays = new List<GameObject>();
    private float _savedTimeScale = 1f;
    private bool _timeScalePaused;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void DisplayUpgrades(List<UpgradeDisplaySO> upgradeOptions)
    {
        if (!upgradeCanvas.activeSelf) upgradeCanvas.SetActive(true);

        if (pauseWhileShown && !_timeScalePaused)
        {
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            _timeScalePaused = true;
        }

        try
        {
            PopulateUpgradeOptions(upgradeOptions);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            HideUpgrades();
        }
    }

    public void HideUpgrades()
    {
        ClearUpgradeOptions();
        if (upgradeCanvas.activeSelf) upgradeCanvas.SetActive(false);

        if (_timeScalePaused)
        {
            Time.timeScale = _savedTimeScale > 0f ? _savedTimeScale : 1f;
            _timeScalePaused = false;
        }
    }

    private void PopulateUpgradeOptions(List<UpgradeDisplaySO> upgradeOptions)
    {
        for (int i = 0; i < upgradeOptions.Count; i++)
        {
            UpgradeDisplaySO option = upgradeOptions[i];
            GameObject displayGO = Instantiate(upgradeDisplayPrefab, upgradeContainer.transform);
            UpgradeDisplay display = displayGO.GetComponent<UpgradeDisplay>();
            display.UpdateDisplay(option);

            UpgradeDisplaySO captured = option;
            display.OnClicked = () => OnUpgradeCardClicked(captured);

            var cg = displayGO.GetComponent<CanvasGroup>();
            if (cg == null) cg = displayGO.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(FadeInCard(cg, i * cardStaggerSeconds));

            _upgradeDisplays.Add(displayGO);
        }
    }

    private System.Collections.IEnumerator FadeInCard(CanvasGroup cg, float delaySeconds)
    {
        if (cg == null) yield break;

        float t = 0f;
        while (t < delaySeconds)
        {
            if (cg == null) yield break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        float dur = Mathf.Max(0.02f, cardFadeSeconds);
        float u = 0f;
        while (u < 1f)
        {
            if (cg == null) yield break;
            u += Time.unscaledDeltaTime / dur;
            cg.alpha = Mathf.Clamp01(u);
            yield return null;
        }

        if (cg != null) cg.alpha = 1f;
    }

    private void OnUpgradeCardClicked(UpgradeDisplaySO selected)
    {
        int newStack = UpgradeManager.Instance.ApplyUpgradeFromDisplay(selected)
            ? UpgradeManager.Instance.GetStack(selected.upgradeID)
            : 0;

        AudioManager.Instance?.PlayUpgradeSelected();

        EventBus<UpgradeSelectedEvent>.Raise(new UpgradeSelectedEvent
        {
            UpgradeID = selected.upgradeID,
            UpgradeName = selected.upgradeName,
            NewStackCount = newStack
        });

        HideUpgrades();
    }

    private void ClearUpgradeOptions()
    {
        foreach (var display in _upgradeDisplays)
            if (display != null) Destroy(display);
        _upgradeDisplays.Clear();
    }
}
