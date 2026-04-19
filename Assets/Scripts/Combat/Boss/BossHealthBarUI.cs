using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class BossHealthBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private Image healthImage;
    [SerializeField] private Image shieldImage;
    [SerializeField] private CanvasGroup introFadeGroup;

    [Header("Flashes")]
    [SerializeField] private Color phaseTwoEnterFlash = new Color(1f, 0.95f, 0.55f, 0.88f);
    [SerializeField] private float phaseTwoEnterFlashDuration = 0.5f;
    [SerializeField] private Color phaseThreeEnterFlash = new Color(1f, 0.35f, 0.15f, 0.92f);
    [SerializeField] private float phaseThreeEnterFlashDuration = 0.78f;
    [SerializeField] private Color shieldRestoreFlashColor = new Color(0.55f, 0.9f, 1f, 0.75f);
    [SerializeField] private float shieldRestoreFlashDuration = 0.35f;
    [SerializeField] private Color shieldRestoreFlashColorPhase3 = new Color(1f, 0.65f, 0.25f, 0.82f);
    [SerializeField] private float shieldRestoreFlashDurationPhase3 = 0.48f;

    private Color _healthBaseColor;
    private Color _shieldBaseColor;
    private Coroutine _healthFlashRoutine;
    private Coroutine _shieldFlashRoutine;
    private bool _initialized;

    private void Awake()
    {
        if (!introFadeGroup)
        {
            introFadeGroup = GetComponent<CanvasGroup>();
            if (!introFadeGroup && rootCanvas)
                introFadeGroup = rootCanvas.GetComponent<CanvasGroup>();
        }
        CacheBaseColors();
    }

    private void CacheBaseColors()
    {
        if (healthImage) _healthBaseColor = healthImage.color;
        if (shieldImage) _shieldBaseColor = shieldImage.color;
        _initialized = true;
    }

    public void Bind(WormBossController boss)
    {
        if (!_initialized) CacheBaseColors();
        SetHealth(boss.HealthNormalized);
        SetShield(boss.CurrentShieldNormalized);
        if (introFadeGroup)
        {
            if (rootCanvas) rootCanvas.enabled = true;
            gameObject.SetActive(true);
        }
        else
            ShowBar();
    }

    public void HideForIntroFade()
    {
        if (introFadeGroup)
        {
            introFadeGroup.alpha = 0f;
            introFadeGroup.blocksRaycasts = false;
            if (rootCanvas) rootCanvas.enabled = true;
            gameObject.SetActive(true);
        }
        else
            HideBar();
    }

    public IEnumerator FadeInFromIntro(float duration)
    {
        duration = Mathf.Max(0.01f, duration);
        if (introFadeGroup)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                introFadeGroup.alpha = Mathf.Clamp01(t / duration);
                yield return null;
            }
            introFadeGroup.alpha = 1f;
            introFadeGroup.blocksRaycasts = true;
        }
        else
            ShowBar();
    }

    public IEnumerator FadeOutForDeath(float duration)
    {
        duration = Mathf.Max(0.01f, duration);
        if (introFadeGroup)
        {
            float start = introFadeGroup.alpha;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                introFadeGroup.alpha = Mathf.Lerp(start, 0f, u);
                yield return null;
            }
            introFadeGroup.alpha = 0f;
            introFadeGroup.blocksRaycasts = false;
        }
        HideBar();
    }

    public void SetHealth(float normalized)
    {
        if (!healthImage) return;
        healthImage.fillAmount = Mathf.Clamp01(normalized);
    }

    public IEnumerator AnimateHealthFillTo(float targetNormalized, float duration, float fromNormalized)
    {
        if (!healthImage) yield break;
        duration = Mathf.Max(0.02f, duration);
        float from = Mathf.Clamp01(fromNormalized);
        float to = Mathf.Clamp01(targetNormalized);
        float t = 0f;
        while (t < duration && healthImage)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float s = Mathf.SmoothStep(0f, 1f, u);
            healthImage.fillAmount = Mathf.Lerp(from, to, s);
            yield return null;
        }
        if (healthImage)
            healthImage.fillAmount = to;
    }

    public void SetShield(float normalized)
    {
        if (!shieldImage) return;
        shieldImage.fillAmount = Mathf.Clamp01(normalized);
    }

    public void SetShieldVisible(bool visible)
    {
        if (shieldImage) shieldImage.enabled = visible;
    }

    public void NotifyPhaseChange(int phase)
    {
        if (!healthImage) return;
        Color flash = phase >= 3 ? phaseThreeEnterFlash : phaseTwoEnterFlash;
        float duration = phase >= 3 ? phaseThreeEnterFlashDuration : phaseTwoEnterFlashDuration;
        if (_healthFlashRoutine != null) StopCoroutine(_healthFlashRoutine);
        _healthFlashRoutine = StartCoroutine(FlashImage(healthImage, _healthBaseColor, flash, duration));
    }

    public void NotifyShieldRestored(int currentBossPhase = 2)
    {
        if (!shieldImage) return;
        Color c = currentBossPhase >= 3 ? shieldRestoreFlashColorPhase3 : shieldRestoreFlashColor;
        float d = currentBossPhase >= 3 ? shieldRestoreFlashDurationPhase3 : shieldRestoreFlashDuration;
        if (_shieldFlashRoutine != null) StopCoroutine(_shieldFlashRoutine);
        _shieldFlashRoutine = StartCoroutine(FlashImage(shieldImage, _shieldBaseColor, c, d));
    }

    public void NotifyShieldBroken()
    {
        if (!shieldImage) return;
        if (_shieldFlashRoutine != null) StopCoroutine(_shieldFlashRoutine);
        _shieldFlashRoutine = StartCoroutine(FlashImage(shieldImage, _shieldBaseColor, new Color(1f, 1f, 1f, 0.9f), 0.22f));
    }

    public void ShowBar()
    {
        if (rootCanvas) rootCanvas.enabled = true;
        gameObject.SetActive(true);
    }

    public void HideBar()
    {
        if (rootCanvas) rootCanvas.enabled = false;
    }

    private static IEnumerator FlashImage(Image target, Color baseColor, Color flashColor, float duration)
    {
        if (!target) yield break;
        float t = 0f;
        while (t < duration && target)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float flashAmount = Mathf.Sin(u * Mathf.PI);
            target.color = Color.Lerp(baseColor, flashColor, flashAmount);
            yield return null;
        }
        if (target) target.color = baseColor;
    }
}
