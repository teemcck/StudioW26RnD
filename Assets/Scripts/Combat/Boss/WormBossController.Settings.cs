using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Inspector configuration for every worm-boss feature module.
/// Keep these field names and types stable: WormBoss.prefab and BossGameplay override them directly.
/// Phase arrays use indices 0, 1, and 2 for phases one, two, and three; time values are seconds unless stated otherwise.
/// </summary>
public sealed partial class WormBossController
{
    [Header("References")]
    [Tooltip("BossWorm Animator; falls back to the Animator on this object.")]
    [SerializeField] private Animator animator;

    [Tooltip("Boss visual sprite; may be a child so stun wobble does not move the root.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Tooltip("Primary occupied floor tiles used for walkability, attack cells, and arena edges.")]
    [SerializeField] private Tilemap baseTilemap;

    [Tooltip("Fallback floor tilemap when Base is unavailable; not an obstacle mask.")]
    [SerializeField] private Tilemap decorationTilemap;

    [Tooltip("Collider layers queried for melee, emergence, and laser-tip damage.")]
    [SerializeField] private LayerMask playerLayerMask;

    [Tooltip("Encounter HUD; can be found even when inactive for the intro.")]
    [SerializeField] private BossHealthBarUI healthBarUI;

    [Tooltip("Exit object activated and faded in by the death cinematic.")]
    [SerializeField] private GameObject postBossTeleporter;

    [Tooltip("Camera framing marker for the exit reveal; absent markers use world origin.")]
    [SerializeField] private Transform postBossTeleporterPan;

    [Tooltip("Encounter music and SFX owner; uses a cached scene lookup when unassigned.")]
    [SerializeField] private BossAudioManager bossAudioManager;

    [Header("Telegraph")]
    [Tooltip("Warning sprite; warnings are omitted when this is unassigned.")]
    [SerializeField] private Sprite indicatorBaseSprite;

    [Tooltip("Imminent warning sprite; indicators fall back to the base sprite.")]
    [SerializeField] private Sprite indicatorImminentSprite;

    [Tooltip("Tint and base opacity during the warning phase.")]
    [SerializeField] private Color indicatorBaseColor = new Color(1f, 1f, 1f, 0.82f);

    [Tooltip("Tint and base opacity during the imminent phase.")]
    [SerializeField] private Color indicatorImminentColor = new Color(1f, 1f, 1f, 0.95f);

    [Tooltip("Sorting order for all attack warnings.")]
    [SerializeField] private int indicatorSortingOrder = 42;

    [Tooltip("Optional warning layer; empty uses the boss sprite sorting layer.")]
    [SerializeField] private string indicatorSortingLayerName = "";

    [Header("Phase & shield")]
    [Tooltip("Inclusive remaining-HP fraction that enters phase two.")]
    [SerializeField] private float phaseTwoHp = 0.64f;

    [Tooltip("Inclusive remaining-HP fraction that enters phase three; evaluated before phase two.")]
    [SerializeField] private float phaseThreeHp = 0.31f;

    [Tooltip("Shield capacity for phases one, two, and three. Breaking hits do not spill into HP.")]
    [SerializeField] private float[] maxShieldByPhase = { 50f, 80f, 120f };

    [Tooltip("Scaled seconds after shield damage before an unbroken shield starts recovering.")]
    [SerializeField] private float shieldRegenIdleSeconds = 5f;

    [Tooltip("Scaled seconds to regenerate an entire shield from empty at a constant rate.")]
    [SerializeField] private float shieldRegenFillSeconds = 5f;

    [Tooltip("Sprite tint at the peak of a shield-hit pulse.")]
    [SerializeField] private Color shieldPingColor = new Color(0.42f, 0.78f, 1f, 1f);

    [Tooltip("Scaled seconds for the shield-hit tint pulse.")]
    [SerializeField] private float shieldPingDuration = 0.18f;

    [Tooltip("Scaled seconds for an HP hit reaction when no attack is active.")]
    [SerializeField] private float damageStunDuration = 0.32f;

    [Header("Shield break stun")]
    [Tooltip("Scaled stun duration after the brief shield-break hitstop.")]
    [SerializeField] private float shieldBreakStunDuration = 2f;

    [Tooltip("Impulse applied when the shield breaks.")]
    [SerializeField] private float shieldBreakKnockbackForce = 4.05f;

    [Tooltip("Camera shake strength at shield break.")]
    [SerializeField] private float shieldBreakCameraShake = 0.24f;

    [Tooltip("Root Z-rotation wobble when the sprite is on the boss root.")]
    [SerializeField] private float shieldBreakWobbleDegrees = 4.6f;

    [Tooltip("Local horizontal wobble amplitude when the sprite is a child.")]
    [SerializeField] private float shieldBreakChildWobbleX = 0.058f;

    [Tooltip("Local vertical wobble amplitude when the sprite is a child.")]
    [SerializeField] private float shieldBreakChildWobbleY = 0.042f;

    [Tooltip("Child sprite local Z-rotation wobble amplitude in degrees.")]
    [SerializeField] private float shieldBreakChildWobbleDegrees = 4.5f;

    [Header("Phase transition")]
    [Tooltip("Camera used for phase zooms, shield-break shake, and death pans.")]
    [SerializeField] private CameraController phaseTransitionCamera;

    [Tooltip("Scaled seconds for phase-two shield refill after the transition animation.")]
    [SerializeField] private float phaseTransitionShieldFillPhase2 = 0.95f;

    [Tooltip("Scaled seconds for phase-three shield refill after the transition animation.")]
    [SerializeField] private float phaseTransitionShieldFillPhase3 = 1.35f;

    [Tooltip("Legacy field name: extra phase-change pose hold after phase-two shield refill.")]
    [SerializeField] private float phaseTransitionLaserHoldAfterShieldPhase2 = 0.4f;

    [Tooltip("Legacy field name: extra phase-change pose hold after phase-three shield refill.")]
    [SerializeField] private float phaseTransitionLaserHoldAfterShieldPhase3 = 0.65f;

    [Tooltip("Camera shake strength on entry to phase two.")]
    [SerializeField] private float phaseTransitionShakeEnterPhase2 = 0.38f;

    [Tooltip("Camera shake strength on entry to phase three.")]
    [SerializeField] private float phaseTransitionShakeEnterPhase3 = 0.62f;

    [Tooltip("Values above zero enable the camera's medium shake after a transition.")]
    [SerializeField] private float phaseTransitionShakeResume = 0.14f;

    [Header("Cutscene")]
    [Tooltip("Waits for BossRoomCutsceneHandler to release intro immunity and start combat.")]
    [SerializeField] private bool waitForExternalCutscene;

    [Header("Death cutscene")]
    [Tooltip("Scaled camera travel time from player to boss before the dying animation.")]
    [SerializeField] private float deathPanPlayerToBossSeconds = 1.25f;

    [Tooltip("Scaled duration of the health-bar fade after the dying animation.")]
    [SerializeField] private float deathHealthBarFadeSeconds = 1f;

    [Tooltip("Scaled duration of the final visual HP drain before the camera impact.")]
    [SerializeField] private float deathHealthDrainSeconds = 0.45f;

    [Tooltip("Scaled travel time from the boss to the exit framing marker.")]
    [SerializeField] private float deathPanBossToTeleporterSeconds = 1.25f;

    [Tooltip("Scaled time to restore the exit's authored sprite and tilemap opacity.")]
    [SerializeField] private float deathTeleporterFadeSeconds = 1f;

    [Tooltip("Scaled travel time from the exit framing marker back to the player.")]
    [SerializeField] private float deathPanTeleporterToPlayerSeconds = 1.25f;

    [Tooltip("Starts the reveal rumble and post-boss escape enemies after the death sequence.")]
    [SerializeField] private BossEscapeSequenceManager escapeSequenceManager;

    [Header("Movement")]
    [Tooltip("World units per scaled second for tactical repositioning; tracking dig uses its own duration.")]
    [SerializeField] private float undergroundSpeed = 3.2f;

    [Tooltip("Scaled seconds between underground rubble bursts.")]
    [SerializeField] private float rubbleTrailInterval = 0.08f;

    [Tooltip("Tint and starting opacity of underground rubble.")]
    [SerializeField] private Color rubbleColor = new Color(0.48f, 0.32f, 0.18f, 0.82f);

    [Tooltip("Sorting order for procedural rubble on the boss sprite layer.")]
    [SerializeField] private int rubbleSortingOrder = 50;

    [Tooltip("Scaled recovery seconds for each phase after the attack or chain completes.")]
    [SerializeField] private float[] idleTimeAfterAttackByPhase = { 2.0f, 1.4f, 0.9f };

    [Tooltip("Probability of repositioning before an attack in each phase.")]
    [SerializeField] private float[] repositionChanceByPhase = { 0.25f, 0.45f, 0.7f };

    [Tooltip("Minimum accepted world distance from the current boss position for sampled destinations.")]
    [SerializeField] private float minRepositionDistance = 2.2f;

    [Tooltip("Maximum accepted world distance from the current boss position for sampled destinations.")]
    [SerializeField] private float maxRepositionDistance = 6f;

    [Tooltip("Preferred distance to the player when the boss is not retreating to recover shield.")]
    [SerializeField] private float repositionIdealPlayerDistance = 1.85f;

    [Tooltip("Chance of a second attack after the first during phase three.")]
    [SerializeField] private float phaseThreeChainChance = 0.35f;

    [Tooltip("Multiplier on the recovery delay after a phase-three attack chain.")]
    [SerializeField] private float phaseThreeChainCooldownMultiplier = 1.35f;

    [Range(0f, 1f)]
    [Tooltip("Below this shield fraction, an unbroken shield favors repositioning away from the player.")]
    [SerializeField] private float shieldRegenSeekThreshold = 0.2f;

    [Tooltip("Damage dealt when tactical repositioning surfaces.")]
    [SerializeField] private float repositionEmergeDamage = 10f;

    [Tooltip("World-space damage circle around the tactical emergence cell.")]
    [SerializeField] private float repositionEmergeRadius = 0.85f;

    [Tooltip("Extra world-space padding around cached sprite/collider extents for destination sampling.")]
    [SerializeField] private float baseArenaEdgePadding = 0.52f;

    [Tooltip("Whether the unflipped artwork faces right; also controls mouth-offset mirroring.")]
    [SerializeField] private bool spriteFacesRightByDefault = false;

    [Tooltip("Minimum scaled seconds between horizontal facing changes.")]
    [SerializeField] private float minFlipInterval = 0.25f;

    [Tooltip("Horizontal aim dead zone that prevents flips for nearly vertical targets.")]
    [SerializeField] private float flipThreshold = 0.25f;

    [Header("Combat")]
    [Tooltip("World-space distance used to select cell centers in the melee cone.")]
    [SerializeField] private float meleeRange = 2.2f;

    [Tooltip("Full angle of the melee targeting cone in degrees.")]
    [SerializeField] private float meleeArcDegrees = 70f;

    [Tooltip("Damage dealt once per target across the melee cells.")]
    [SerializeField] private float meleeDamage = 18f;

    [Tooltip("Legacy field name: scaled seconds before the melee charge pause.")]
    [SerializeField] private float meleeFramesBeforeWait = 0.42f;

    [Tooltip("Additional scaled warning seconds before melee damage.")]
    [SerializeField] private float meleeChargeWait = 0.33f;

    [Tooltip("Scaled time spent on the strike after applying damage.")]
    [SerializeField] private float meleeStrikeDuration = 0.12f;

    [Tooltip("Additional scaled recovery after the strike.")]
    [SerializeField] private float meleeRecoveryDuration = 0.13f;

    [Tooltip("Final fraction of the melee warning shown as imminent.")]
    [SerializeField] private float meleeImminentFraction = 0.3f;

    [Tooltip("Projectile prefab with SimpleProjectile; its own settings supply speed and damage.")]
    [SerializeField] private GameObject bossProjectilePrefab;

    [Tooltip("Legacy field name: scaled seconds before the ranged charge pause.")]
    [SerializeField] private float shootFramesBeforeWait = 0.42f;

    [Tooltip("Additional scaled warning seconds before the first volley.")]
    [SerializeField] private float shootChargeWait = 0.33f;

    [Tooltip("Scaled recovery after the final volley.")]
    [SerializeField] private float shootRecoveryDuration = 0.25f;

    [Tooltip("Maximum angular offset on either side of aim, in degrees, for phase two.")]
    [SerializeField] private float shootSpreadPhase2 = 18f;

    [Tooltip("Maximum angular offset on either side of aim, in degrees, for phase three.")]
    [SerializeField] private float shootSpreadPhase3 = 22f;

    [Tooltip("Scaled seconds before the second, freshly aimed phase-three volley.")]
    [SerializeField] private float shootBurstIntervalPhase3 = 0.18f;

    [Tooltip("Number of projectiles distributed across the phase-two spread.")]
    [SerializeField] private int shootBulletsPhase2 = 3;

    [Tooltip("Number of projectiles in each phase-three volley.")]
    [SerializeField] private int shootBulletsPhase3 = 3;

    [Tooltip("Scaled seconds during which the underground strike follows the player's cell.")]
    [SerializeField] private float digTrackDuration = 0.9f;

    [Tooltip("Scaled warning seconds after locking the emergence cell.")]
    [SerializeField] private float digLockDuration = 0.35f;

    [Tooltip("World-space damage radius around the locked emergence cell.")]
    [SerializeField] private float digStrikeRadius = 1.05f;

    [Tooltip("Damage applied when the tracking dig emerges.")]
    [SerializeField] private float digDamage = 24f;

    [Tooltip("Fraction of the rising clip to wait before tracking-dig damage.")]
    [SerializeField] private float digRiseDamageNormalizedTime = 0.22f;

    [Tooltip("Beam visual prefab; the controller owns tip damage independently of its renderer.")]
    [SerializeField] private GameObject bossLaserBeamPrefab;

    [Tooltip("Scaled seconds for the full-length laser charge warning.")]
    [SerializeField] private float laserChargeDuration = 0.66f;

    [Tooltip("Scaled seconds for the beam to extend from minimum to maximum length.")]
    [SerializeField] private float laserFireDuration = 0.72f;

    [Tooltip("Scaled recovery after all laser sweeps complete.")]
    [SerializeField] private float laserRecoveryDuration = 0.35f;

    [Tooltip("Damage per target per sweep, applied only by the advancing tip.")]
    [SerializeField] private float laserDamage = 22f;

    [Tooltip("Maximum beam length in world units.")]
    [SerializeField] private float laserBeamLength = 7.5f;

    [Tooltip("Starting beam length in world units before extension.")]
    [SerializeField] private float laserMinBeamLength = 0.35f;

    [Tooltip("Warning row count along the beam; runtime uses at least four.")]
    [SerializeField] private int laserStretchSegmentCount = 18;

    [Tooltip("World-space width of each extending-beam warning row.")]
    [SerializeField] private float laserSegmentStripeWidth = 0.42f;

    [Tooltip("World-space collision query radius at the advancing beam tip.")]
    [SerializeField] private float laserTipHitRadius = 0.48f;

    [Tooltip("World-space width of the full-length charge warning.")]
    [SerializeField] private float laserChargeStripeWidth = 0.55f;

    [Tooltip("Fraction of one row length before its end at which the row becomes imminent.")]
    [SerializeField] private float laserSegmentImminentLead = 0.35f;

    [Tooltip("Scaled fade seconds for rows after the tip has crossed them.")]
    [SerializeField] private float laserRowFadeDuration = 0.14f;

    [Tooltip("Tint and starting opacity for all beam visual pieces.")]
    [SerializeField] private Color laserBeamColor = new Color(1f, 0.3f, 0.2f, 0.95f);

    [Tooltip("World-space mouth offset, mirrored horizontally according to sprite facing.")]
    [SerializeField] private Vector2 laserMouthOffset = new Vector2(0.4f, 0.05f);

    [Tooltip("Scaled pause between the two phase-three laser sweeps.")]
    [SerializeField] private float laserBetweenSweepPause = 0.22f;

    [Tooltip("Tint and starting opacity of emergence rubble bursts.")]
    [SerializeField] private Color riseRubbleColor = new Color(0.55f, 0.38f, 0.22f, 0.92f);
}
