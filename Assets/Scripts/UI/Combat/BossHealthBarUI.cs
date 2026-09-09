using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Boss HUD presentation for health, shield, phase flashes, and cinematic fades.
/// The controller pushes values explicitly; the HUD does not own or poll combat state.
/// </summary>
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

    /// <summary>
    /// Resolves HUD objects and stores authored colors before any phase flashes occur.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
        CacheBaseColors();
    }

    /// <summary>
    /// Resolves references even when the HUD was initially inactive for the intro.
    /// </summary>
    private void OnEnable()
    {
        ResolveReferences();
    }

    /// <summary>
    /// Uses assigned UI references first, then searches the canvas for BossHealth, BossShield, and a fade group.
    /// </summary>
    private void ResolveReferences()
    {
        if (!rootCanvas)
            rootCanvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);

        if (!healthImage || !shieldImage)
        {
            Image[] images = rootCanvas ? rootCanvas.GetComponentsInChildren<Image>(true) : GetComponentsInChildren<Image>(true);
            foreach (Image image in images)
            {
                if (!image)
                    continue;

                string objectName = image.gameObject.name;
                if (!healthImage && objectName == "BossHealth")
                    healthImage = image;
                else if (!shieldImage && objectName == "BossShield")
                    shieldImage = image;
            }
        }

        if (!introFadeGroup)
        {
            introFadeGroup = GetComponent<CanvasGroup>();
            if (!introFadeGroup && rootCanvas)
                introFadeGroup = rootCanvas.GetComponent<CanvasGroup>();
        }
    }

    /// <summary>
    /// Stores authored fill colors so repeated flashes can return to a stable baseline.
    /// </summary>
    private void CacheBaseColors()
    {
        if (healthImage) _healthBaseColor = healthImage.color;
        if (shieldImage) _shieldBaseColor = shieldImage.color;
        _initialized = true;
    }

    /// <summary>
    /// Copies the boss's current fills and shows the bar before a fight or coordinated intro fade.
    /// </summary>
    public void Bind(WormBossController boss)
    {
        ResolveReferences();
        if (!_initialized) CacheBaseColors();
        SetHealth(boss.HealthNormalized);
        SetShield(boss.CurrentShieldNormalized);
        ShowBar();
    }

    /// <summary>
    /// Keeps a fadeable HUD active at zero alpha so the room intro can animate it later.
    /// </summary>
    public void HideForIntroFade()
    {
        ResolveReferences();
        if (introFadeGroup)
        {
            introFadeGroup.alpha = 0f;
            introFadeGroup.blocksRaycasts = false;
            introFadeGroup.interactable = false;
            if (rootCanvas) rootCanvas.enabled = true;
            gameObject.SetActive(true);
        }
        else
            HideBar();
    }

    /// <summary>
    /// Fades in the boss bar over scaled seconds, with immediate visibility when no CanvasGroup exists.
    /// </summary>
    public IEnumerator FadeInFromIntro(float duration)
    {
        ResolveReferences();
        ShowBar();
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
            introFadeGroup.interactable = false;
        }
        else
            ShowBar();
    }

    /// <summary>
    /// Accepts the room handler's shared fade progress so boss and player HUDs appear together.
    /// </summary>
    public void SetIntroFadeAlpha(float alpha01)
    {
        ResolveReferences();
        if (!introFadeGroup)
            return;
        introFadeGroup.alpha = Mathf.Clamp01(alpha01);
        if (alpha01 > 0.001f && rootCanvas)
            rootCanvas.enabled = true;
    }

    /// <summary>
    /// Fades the group to zero and hides the bar after the boss death animation.
    /// </summary>
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

    /// <summary>
    /// Applies a clamped HP fraction to the health fill.
    /// </summary>
    public void SetHealth(float normalized)
    {
        if (!healthImage) return;
        healthImage.fillAmount = Mathf.Clamp01(normalized);
    }

    /// <summary>
    /// Smoothly drains or fills HP from an explicit starting fraction without changing gameplay health.
    /// </summary>
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

    /// <summary>
    /// Applies a clamped shield fraction to the shield fill.
    /// </summary>
    public void SetShield(float normalized)
    {
        if (!shieldImage) return;
        shieldImage.fillAmount = Mathf.Clamp01(normalized);
    }

    /// <summary>
    /// Controls only the shield image visibility, preserving its current fill.
    /// </summary>
    public void SetShieldVisible(bool visible)
    {
        if (shieldImage) shieldImage.enabled = visible;
    }

    /// <summary>
    /// Restarts a phase-specific flash on the health image.
    /// </summary>
    public void NotifyPhaseChange(int phase)
    {
        if (!healthImage) return;
        Color flash = phase >= 3 ? phaseThreeEnterFlash : phaseTwoEnterFlash;
        float duration = phase >= 3 ? phaseThreeEnterFlashDuration : phaseTwoEnterFlashDuration;
        if (_healthFlashRoutine != null) StopCoroutine(_healthFlashRoutine);
        _healthFlashRoutine = StartCoroutine(FlashImage(healthImage, _healthBaseColor, flash, duration));
    }

    /// <summary>
    /// Restarts the restored-shield flash, using the warmer phase-three palette when applicable.
    /// </summary>
    public void NotifyShieldRestored(int currentBossPhase = 2)
    {
        if (!shieldImage) return;
        Color c = currentBossPhase >= 3 ? shieldRestoreFlashColorPhase3 : shieldRestoreFlashColor;
        float d = currentBossPhase >= 3 ? shieldRestoreFlashDurationPhase3 : shieldRestoreFlashDuration;
        if (_shieldFlashRoutine != null) StopCoroutine(_shieldFlashRoutine);
        _shieldFlashRoutine = StartCoroutine(FlashImage(shieldImage, _shieldBaseColor, c, d));
    }

    /// <summary>
    /// Flashes the shield image white when the controller reports a break; fill is updated separately.
    /// </summary>
    public void NotifyShieldBroken()
    {
        if (!shieldImage) return;
        if (_shieldFlashRoutine != null) StopCoroutine(_shieldFlashRoutine);
        _shieldFlashRoutine = StartCoroutine(FlashImage(shieldImage, _shieldBaseColor, new Color(1f, 1f, 1f, 0.9f), 0.22f));
    }

    /// <summary>
    /// Enables the bar and canvas and restores the fade group to visible, non-interactive HUD state.
    /// </summary>
    public void ShowBar()
    {
        ResolveReferences();
        if (rootCanvas) rootCanvas.enabled = true;
        if (introFadeGroup)
        {
            introFadeGroup.alpha = 1f;
            introFadeGroup.blocksRaycasts = false;
            introFadeGroup.interactable = false;
        }
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Clears fade-group visibility and disables the root canvas when those references are assigned.
    /// </summary>
    public void HideBar()
    {
        if (introFadeGroup)
        {
            introFadeGroup.alpha = 0f;
            introFadeGroup.blocksRaycasts = false;
            introFadeGroup.interactable = false;
        }
        if (rootCanvas) rootCanvas.enabled = false;
    }

    /// <summary>
    /// Pulses an image toward the supplied flash color and returns it to its authored base color.
    /// </summary>
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
