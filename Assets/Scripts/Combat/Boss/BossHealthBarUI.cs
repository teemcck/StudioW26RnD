using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class BossHealthBarUI : MonoBehaviour
{
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private RectTransform barRoot;
    [SerializeField] private RectTransform fillRect;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image phaseFlashImage;
    [SerializeField] private Text bossNameText;
    [SerializeField] private Text hpValueText;
    [SerializeField] private Image phaseTwoMarker;
    [SerializeField] private Image phaseThreeMarker;

    private WormBossController _boss;
    private int _lastPhase = 1;
    private Color _baseFillColor = new Color(0.96f, 0.24f, 0.24f, 1f);

    public void Bind(WormBossController boss, string bossName, float phaseTwoThreshold, float phaseThreeThreshold)
    {
        _boss = boss;
        EnsureUi();
        if (bossNameText) bossNameText.text = bossName;
        PlaceMarker(phaseTwoMarker, phaseTwoThreshold);
        PlaceMarker(phaseThreeMarker, phaseThreeThreshold);
        ShowBar();
    }

    public void SetHealth(float normalized, float current, float max, int phase)
    {
        EnsureUi();
        float n = Mathf.Clamp01(normalized);
        if (fillRect)
        {
            fillRect.anchorMax = new Vector2(Mathf.Lerp(0.02f, 0.98f, n), fillRect.anchorMax.y);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
        }

        if (phase != _lastPhase)
        {
            _lastPhase = phase;
            ApplyPhaseTheme(phase);
        }

        if (hpValueText) hpValueText.text = $"HP {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}    P{phase}";
    }

    public void NotifyPhaseChange(int phase)
    {
        EnsureUi();
        _lastPhase = phase;
        ApplyPhaseTheme(phase);
    }

    public void ShowBar()
    {
        EnsureUi();
        if (rootCanvas) rootCanvas.enabled = true;
    }

    public void HideBar()
    {
        if (rootCanvas) rootCanvas.enabled = false;
    }

    private void EnsureUi()
    {
        if (!rootCanvas || !barRoot || !fillImage || !bossNameText || !hpValueText || !phaseTwoMarker || !phaseThreeMarker)
            BuildRuntimeUi();

        if (!fillRect && fillImage) fillRect = fillImage.rectTransform;
    }

    private void BuildRuntimeUi()
    {
        if (!rootCanvas)
        {
            var canvasGo = new GameObject("BossHealthCanvas");
            rootCanvas = canvasGo.AddComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        if (!barRoot)
        {
            var barGo = new GameObject("BossBarRoot");
            barGo.transform.SetParent(rootCanvas.transform, false);
            barRoot = barGo.AddComponent<RectTransform>();
            barRoot.anchorMin = new Vector2(0.2f, 0.94f);
            barRoot.anchorMax = new Vector2(0.8f, 0.985f);
            barRoot.offsetMin = Vector2.zero;
            barRoot.offsetMax = Vector2.zero;
        }

        GameObject frame = CreateImage("Frame", barRoot, new Color(0.08f, 0.08f, 0.08f, 0.95f), new Vector2(0f, 0f), new Vector2(1f, 1f));
        CreateImage("Background", frame.GetComponent<RectTransform>(), new Color(0.22f, 0.08f, 0.08f, 1f), new Vector2(0.02f, 0.18f), new Vector2(0.98f, 0.74f));

        GameObject fillGo = CreateImage("Fill", frame.GetComponent<RectTransform>(), new Color(0.96f, 0.24f, 0.24f, 1f), new Vector2(0.02f, 0.18f), new Vector2(0.98f, 0.74f));
        fillImage = fillGo.GetComponent<Image>();
        fillRect = fillGo.GetComponent<RectTransform>();
        fillImage.type = Image.Type.Simple;

        GameObject flashGo = CreateImage("PhaseFlash", frame.GetComponent<RectTransform>(), new Color(1f, 1f, 1f, 0f), new Vector2(0.02f, 0.18f), new Vector2(0.98f, 0.74f));
        phaseFlashImage = flashGo.GetComponent<Image>();

        phaseTwoMarker = CreateImage("Phase2Marker", frame.GetComponent<RectTransform>(), new Color(1f, 0.85f, 0.35f, 1f), new Vector2(0.5f, 0.2f), new Vector2(0.505f, 0.74f)).GetComponent<Image>();
        phaseThreeMarker = CreateImage("Phase3Marker", frame.GetComponent<RectTransform>(), new Color(1f, 0.95f, 0.7f, 1f), new Vector2(0.5f, 0.2f), new Vector2(0.505f, 0.74f)).GetComponent<Image>();

        bossNameText = CreateText("BossName", frame.GetComponent<RectTransform>(), new Vector2(0f, 0.76f), new Vector2(0.5f, 1f), TextAnchor.MiddleLeft, 18);
        hpValueText = CreateText("BossHp", frame.GetComponent<RectTransform>(), new Vector2(0.5f, 0.76f), new Vector2(1f, 1f), TextAnchor.MiddleRight, 16);
    }

    private void ApplyPhaseTheme(int phase)
    {
        Color target = phase switch
        {
            1 => new Color(0.96f, 0.24f, 0.24f, 1f),
            2 => new Color(1f, 0.54f, 0.2f, 1f),
            _ => new Color(1f, 0.86f, 0.26f, 1f)
        };
        _baseFillColor = target;
        if (fillImage) fillImage.color = target;
        if (bossNameText)
        {
            bossNameText.color = Color.Lerp(Color.white, target, 0.4f);
            bossNameText.text = $"Worm Core  -  PHASE {phase}";
        }
        if (phaseFlashImage) StartCoroutine(FlashPhaseBanner(target));
    }

    private IEnumerator FlashPhaseBanner(Color target)
    {
        if (!phaseFlashImage) yield break;
        float duration = 0.5f;
        float end = Time.time + duration;
        while (Time.time < end && phaseFlashImage)
        {
            float t = Mathf.InverseLerp(end - duration, end, Time.time);
            float a = Mathf.Lerp(0.45f, 0f, t);
            phaseFlashImage.color = new Color(target.r, target.g, target.b, a);
            yield return null;
        }
        if (phaseFlashImage) phaseFlashImage.color = new Color(target.r, target.g, target.b, 0f);
    }

    private static GameObject CreateImage(string name, RectTransform parent, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    private static Text CreateText(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, TextAnchor alignment, int fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var txt = go.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontSize;
        txt.color = Color.white;
        txt.alignment = alignment;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.text = string.Empty;
        return txt;
    }

    private static void PlaceMarker(Image marker, float threshold)
    {
        if (!marker) return;
        RectTransform rt = marker.rectTransform;
        float x = Mathf.Clamp01(threshold);
        rt.anchorMin = new Vector2(x, rt.anchorMin.y);
        rt.anchorMax = new Vector2(x + 0.004f, rt.anchorMax.y);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void LateUpdate()
    {
        if (_boss && rootCanvas && !rootCanvas.enabled)
            rootCanvas.enabled = true;
    }
}
