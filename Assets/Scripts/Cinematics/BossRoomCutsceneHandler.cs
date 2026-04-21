using System.Collections;
using UnityEngine;

public sealed class BossRoomCutsceneHandler : MonoBehaviour
{
    [Header("Enable")]
    [SerializeField] private bool playOnStart = true;

    [Header("References")]
    [SerializeField] private WormBossController boss;
    [SerializeField] private MonoBehaviour introSequenceBehaviour;
    [SerializeField] private CameraController cameraController;
    [SerializeField] private PlayerController player;
    [SerializeField] private Transform playerSpawnPoint;
    [SerializeField] private BossHealthBarUI healthBar;

    [Header("Timing")]
    [SerializeField] private float playerLockDurationSeconds = 8.5f;
    [SerializeField] private float holdOnPlayerSeconds = 0.5f;
    [SerializeField] private float panToBossSeconds = 1.25f;
    [SerializeField] private float panBackSeconds = 1.25f;
    [SerializeField] private float healthBarFadeSeconds = 1f;
    [SerializeField] private float bossIdleBeforeFightSeconds = 0.5f;

    [Header("Growl")]
    [SerializeField] private float growlShakeIntensity = 0.34f;
    [SerializeField] private float growlShakeInterval = 0.36f;
    [SerializeField] private float growlShakeStartDelay = 0.12f;

    private Coroutine _routine;
    private Transform _panProxy;

    private void OnDestroy()
    {
        if (_routine != null)
            StopCoroutine(_routine);
        if (_panProxy)
            Destroy(_panProxy.gameObject);
    }

    private void Start()
    {
        if (!playOnStart)
            return;

        if (_routine != null)
            StopCoroutine(_routine);
        _routine = StartCoroutine(RunIntroRoutine());
    }

    private IBossRoomIntroSequence ResolveSequence()
    {
        if (introSequenceBehaviour is IBossRoomIntroSequence s)
            return s;
        if (boss)
            return boss.GetComponent<WormBossRoomIntro>();
        return null;
    }

    private void EnsurePanProxy(Transform playerTf, Transform bossTf)
    {
        if (_panProxy)
            return;
        var go = new GameObject("CutscenePanProxy");
        _panProxy = go.transform;
        Vector3 p0 = playerTf ? playerTf.position : bossTf.position;
        p0.z = bossTf.position.z;
        _panProxy.position = p0;
    }

    private static IEnumerator PanProxyLerp(Transform proxy, Vector3 from, Vector3 to, float duration)
    {
        if (!proxy)
            yield break;
        if (duration <= 0.0001f)
        {
            proxy.position = to;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            proxy.position = Vector3.Lerp(from, to, u);
            yield return null;
        }
        proxy.position = to;
    }

    private IEnumerator RunIntroRoutine()
    {
        if (!boss)
            boss = FindFirstObjectByType<WormBossController>();

        if (!boss)
        {
            Debug.LogWarning("BossRoomCutsceneHandler: no WormBossController in scene.", this);
            yield break;
        }

        IBossRoomIntroSequence sequence = ResolveSequence();
        if (sequence == null)
        {
            Debug.LogWarning("BossRoomCutsceneHandler: add WormBossRoomIntro (or assign introSequenceBehaviour).", this);
            yield break;
        }

        ResolvePlayerReference();
        PreparePlayerForIntro();

        if (!healthBar)
            healthBar = FindFirstObjectByType<BossHealthBarUI>(FindObjectsInactive.Include);

        if (!cameraController)
            cameraController = FindFirstObjectByType<CameraController>();

        Transform playerTf = player ? player.transform : null;
        Transform bossTf = boss.transform;

        if (player)
            player.LockControlsForSeconds(playerLockDurationSeconds);

        if (healthBar)
            healthBar.HideForIntroFade();

        if (CinematicBars.Instance != null)
            CinematicBars.Instance.Show(0.3f);
        SetPlayerHudVisible(false);

        boss.Cutscene_PrepareBuriedFacingPlayer();

        if (!playerTf)
        {
            Debug.LogWarning("BossRoomCutsceneHandler: no Player — skipping pan, framing boss.", this);
            if (cameraController)
                cameraController.LockToTransform(bossTf);
            yield return sequence.RunRiseRoutine();
            yield return sequence.RunGrowlRoutine(cameraController, growlShakeIntensity, growlShakeInterval, growlShakeStartDelay);
            if (cameraController)
                cameraController.LockToPlayer();
            yield return new WaitForSeconds(panBackSeconds);
            boss.BindHealthBarForFightStart();
            if (healthBar)
                yield return healthBar.FadeInFromIntro(healthBarFadeSeconds);
            if (CinematicBars.Instance != null)
                CinematicBars.Instance.Hide(0.25f);
            SetPlayerHudVisible(true);
            yield return WaitBossIdleThenStartCombat();
            _routine = null;
            yield break;
        }

        EnsurePanProxy(playerTf, bossTf);
        Vector3 fromPos = playerTf.position;
        fromPos.z = bossTf.position.z;
        Vector3 toPos = bossTf.position;
        _panProxy.position = fromPos;

        if (cameraController)
            cameraController.LockToTransform(_panProxy);

        float t0 = Time.time;
        while (Time.time - t0 < holdOnPlayerSeconds)
        {
            if (boss.IsDead) yield break;
            yield return null;
        }

        if (cameraController)
            cameraController.LockToTransform(_panProxy);
        yield return PanProxyLerp(_panProxy, fromPos, toPos, panToBossSeconds);

        if (boss.IsDead)
            yield break;

        if (cameraController)
            cameraController.LockToTransform(bossTf);

        yield return sequence.RunRiseRoutine();

        if (boss.IsDead)
            yield break;

        yield return sequence.RunGrowlRoutine(cameraController, growlShakeIntensity, growlShakeInterval, growlShakeStartDelay);

        if (boss.IsDead)
            yield break;

        fromPos = bossTf.position;
        fromPos.z = playerTf.position.z;
        toPos = playerTf.position;
        _panProxy.position = fromPos;

        if (cameraController)
            cameraController.LockToTransform(_panProxy);

        yield return PanProxyLerp(_panProxy, fromPos, toPos, panBackSeconds);

        if (cameraController)
            cameraController.LockToPlayer();

        if (_panProxy)
        {
            Destroy(_panProxy.gameObject);
            _panProxy = null;
        }

        boss.BindHealthBarForFightStart();
        if (healthBar)
            yield return healthBar.FadeInFromIntro(healthBarFadeSeconds);

        if (CinematicBars.Instance != null)
            CinematicBars.Instance.Hide(0.25f);
        SetPlayerHudVisible(true);

        yield return WaitBossIdleThenStartCombat();
        _routine = null;
    }

    private void SetPlayerHudVisible(bool visible)
    {
        var hud = FindFirstObjectByType<PlayerHudUI>(FindObjectsInactive.Include);
        if (hud != null)
        {
            var cg = hud.GetComponent<CanvasGroup>() ?? hud.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = visible ? 1f : 0f;
            cg.blocksRaycasts = visible;
        }

        var strip = FindFirstObjectByType<AppliedUpgradeStripUI>(FindObjectsInactive.Include);
        if (strip != null)
        {
            var cg = strip.GetComponent<CanvasGroup>() ?? strip.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = visible ? 1f : 0f;
            cg.blocksRaycasts = visible;
        }
    }

    private IEnumerator WaitBossIdleThenStartCombat()
    {
        float wait = Mathf.Max(0f, bossIdleBeforeFightSeconds);
        if (wait > 0f && player)
            player.LockControlsForSeconds(wait);

        float t0 = Time.time;
        while (Time.time - t0 < wait)
        {
            if (boss.IsDead)
                yield break;
            yield return null;
        }

        boss.SetIntroCutsceneActive(false);
        boss.StartCombatAfterCutscene();
    }

    private void ResolvePlayerReference()
    {
        if (player)
            return;

        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (players.Length > 0)
            player = players[0];
    }

    private void PreparePlayerForIntro()
    {
        if (!player)
            return;

        if (!player.gameObject.activeSelf)
            player.gameObject.SetActive(true);

        Transform spawn = playerSpawnPoint ? playerSpawnPoint : player.transform;
        player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);

        if (player.TryGetComponent<Rigidbody2D>(out var rb))
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = true;
        }
    }
}

public interface IBossRoomIntroSequence
{
    IEnumerator RunRiseRoutine();
    IEnumerator RunGrowlRoutine(CameraController camera, float shakeIntensity, float shakeInterval, float shakeLeadAfterGrowlStarts);
}
