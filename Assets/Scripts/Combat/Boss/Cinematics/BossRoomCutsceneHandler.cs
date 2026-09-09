using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Coordinates boss-room arrival, player placement, camera pans, intro performance, and the HUD/combat handoff.
/// Also routes player death in BossGameplay to the configured death scene.
/// </summary>
public sealed class BossRoomCutsceneHandler : MonoBehaviour
{
    [Header("Enable")]
    [SerializeField] private bool playOnStart = true;

    [Header("Run end (matches GameplayHandler)")]
    [SerializeField] private string deathSceneName = "DeathScene";

    [Header("References")]
    [SerializeField] private WormBossController boss;
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
    [Tooltip("Must match CinematicBars.Hide duration so letterbox finishes retracting before the boss bar fades in.")]
    [SerializeField] private float cinematicBarHideDuration = 0.25f;

    [Header("Growl")]
    [SerializeField] private float growlShakeIntensity = 0.34f;
    [SerializeField] private float growlShakeInterval = 0.36f;
    [SerializeField] private float growlShakeStartDelay = 0.12f;

    /// <summary>True from intro start until combat begins; HitstopController refuses freezes while set.</summary>
    public static bool IsIntroActive { get; private set; }

    private Coroutine _routine;
    private Transform _panProxy;

    private IEventBinding<PlayerDiedEvent> _playerDiedBinding;

    /// <summary>
    /// Subscribes this room to player-death events while its handler is enabled.
    /// </summary>
    private void OnEnable()
    {
        _playerDiedBinding = EventBus<PlayerDiedEvent>.Register(OnPlayerDiedDuringBoss);
    }

    /// <summary>
    /// Removes the matching event binding so scene changes do not leave a stale listener.
    /// </summary>
    private void OnDisable()
    {
        EventBus<PlayerDiedEvent>.Unsubscribe(_playerDiedBinding);
    }

    /// <summary>
    /// Loads the death scene only from BossGameplay and clears hitstop or slow motion first.
    /// </summary>
    private void OnPlayerDiedDuringBoss(PlayerDiedEvent evt)
    {
        if (SceneManager.GetActiveScene().name != "BossGameplay")
            return;

        Time.timeScale = 1f;
        SceneManager.LoadScene(deathSceneName);
    }

    /// <summary>
    /// Stops the intro and releases its temporary camera target during scene teardown.
    /// </summary>
    private void OnDestroy()
    {
        IsIntroActive = false;
        if (_routine != null)
            StopCoroutine(_routine);
        if (_panProxy)
            Destroy(_panProxy.gameObject);
    }

    /// <summary>
    /// Starts the configured room intro after scene components have completed Awake.
    /// </summary>
    private void Start()
    {
        if (!playOnStart)
            return;

        if (_routine != null)
            StopCoroutine(_routine);
        _routine = StartCoroutine(RunIntroRoutine());
    }

    /// <summary>
    /// Creates a movable camera target starting at the player, or at the boss if the player is unavailable.
    /// </summary>
    private void EnsurePanProxy(Transform playerTf, Transform bossTf)
    {
        if (_panProxy)
            return;
        GameObject go = new GameObject("CutscenePanProxy");
        _panProxy = go.transform;
        Vector3 p0 = playerTf ? playerTf.position : bossTf.position;
        p0.z = bossTf.position.z;
        _panProxy.position = p0;
    }

    /// <summary>
    /// Stages arrival, rise, roar, return pan, and HUD reveal before releasing intro immunity and starting attacks.
    /// </summary>
    private IEnumerator RunIntroRoutine()
    {
        IsIntroActive = true;
        yield return RunIntroRoutineCore();
        IsIntroActive = false;
    }

    private IEnumerator RunIntroRoutineCore()
    {
        yield return null;

        if (!boss)
            boss = FindFirstObjectByType<WormBossController>();

        if (!boss)
        {
            Debug.LogWarning("BossRoomCutsceneHandler: no WormBossController in scene.", this);
            yield break;
        }

        // WormBossController requires WormBossRoomIntro, so the boss prefab always carries its intro.
        WormBossRoomIntro sequence = boss.GetComponent<WormBossRoomIntro>();

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
            if (CinematicBars.Instance != null)
            {
                CinematicBars.Instance.Hide(cinematicBarHideDuration);
                yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, cinematicBarHideDuration));
            }
            yield return CoFadeBossBarAndPlayerHud(healthBarFadeSeconds);
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
        yield return BossTeleporterReveal.PanProxyLerp(_panProxy, fromPos, toPos, panToBossSeconds);

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

        yield return BossTeleporterReveal.PanProxyLerp(_panProxy, fromPos, toPos, panBackSeconds);

        if (cameraController)
            cameraController.LockToPlayer();

        if (_panProxy)
        {
            Destroy(_panProxy.gameObject);
            _panProxy = null;
        }

        boss.BindHealthBarForFightStart();
        if (CinematicBars.Instance != null)
        {
            CinematicBars.Instance.Hide(cinematicBarHideDuration);
            yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, cinematicBarHideDuration));
        }
        yield return CoFadeBossBarAndPlayerHud(healthBarFadeSeconds);

        yield return WaitBossIdleThenStartCombat();
        _routine = null;
    }

    /// <summary>
    /// Finds the current player HUD, including inactive child HUD objects used during transitions.
    /// </summary>
    private PlayerHudUI ResolvePlayerHud()
    {
        if (player != null)
            return player.GetComponent<PlayerHudUI>() ?? player.GetComponentInChildren<PlayerHudUI>(true);
        return FindFirstObjectByType<PlayerHudUI>(FindObjectsInactive.Include);
    }

    /// <summary>
    /// Prefers the persistent upgrade strip, then falls back to the current player or scene.
    /// </summary>
    private AppliedUpgradeStripUI ResolveUpgradeStrip()
    {
        if (UpgradeManager.Instance != null && UpgradeManager.Instance.PersistentAppliedUpgradeStrip != null)
            return UpgradeManager.Instance.PersistentAppliedUpgradeStrip;

        if (player != null)
            return player.GetComponent<AppliedUpgradeStripUI>() ?? player.GetComponentInChildren<AppliedUpgradeStripUI>(true);
        return FindFirstObjectByType<AppliedUpgradeStripUI>(FindObjectsInactive.Include);
    }

    /// <summary>
    /// Removes cinematic suppression while holding the player HUD at zero intro alpha.
    /// </summary>
    private void PreparePlayerHudAndStripForCoordinatedFade()
    {
        ResolvePlayerHud()?.SetBossCutsceneHudSuppressed(false);
        ResolveUpgradeStrip()?.SetBossCutsceneStripSuppressed(false);
        ResolvePlayerHud()?.SetBossHudIntroFade(0f);
    }

    /// <summary>
    /// Drives boss and player HUD alpha from one progress value, then returns visibility to their normal owners.
    /// </summary>
    private IEnumerator CoFadeBossBarAndPlayerHud(float duration)
    {
        float dur = Mathf.Max(0.01f, duration);
        PreparePlayerHudAndStripForCoordinatedFade();

        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            if (healthBar)
                healthBar.SetIntroFadeAlpha(u);
            ResolvePlayerHud()?.SetBossHudIntroFade(u);
            yield return null;
        }

        if (healthBar)
            healthBar.ShowBar();
        ResolvePlayerHud()?.ClearBossHudIntroFade();
        SetPlayerHudVisible(true);
    }

    /// <summary>
    /// Delegates cinematic suppression to the HUD components so their own visibility rules remain authoritative.
    /// </summary>
    private void SetPlayerHudVisible(bool visible)
    {
        // Drive visibility via PlayerHudUI / AppliedUpgradeStripUI so we do not stack CanvasGroups on
        // the player root (that hid the HUD canvas even when gameplay LateUpdate forced alpha to 1).
        ResolvePlayerHud()?.SetBossCutsceneHudSuppressed(!visible);
        ResolveUpgradeStrip()?.SetBossCutsceneStripSuppressed(!visible);
    }

    /// <summary>
    /// Provides a final idle grace period before clearing boss intro immunity and starting the behavior loop.
    /// </summary>
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

    /// <summary>
    /// Finds an existing player, including inactive objects carried into the boss scene.
    /// </summary>
    private void ResolvePlayerReference()
    {
        if (player)
            return;

        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (players.Length > 0)
            player = players[0];
    }

    /// <summary>
    /// Activates and positions the player, clears residual motion, and restores physics simulation for arrival.
    /// </summary>
    private void PreparePlayerForIntro()
    {
        if (!player)
            return;

        if (!player.gameObject.activeSelf)
            player.gameObject.SetActive(true);

        Transform spawn = playerSpawnPoint ? playerSpawnPoint : player.transform;
        player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);

        if (player.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = true;
        }
    }
}

