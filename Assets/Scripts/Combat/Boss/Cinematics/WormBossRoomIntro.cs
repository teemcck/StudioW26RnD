using System.Collections;
using UnityEngine;

/// <summary>
/// Worm-specific rise and roar presentation used by the room cutscene handler.
/// The controller keeps animation access centralized while the room handler owns camera travel and combat handoff.
/// </summary>
[RequireComponent(typeof(WormBossController))]
public sealed class WormBossRoomIntro : MonoBehaviour
{
    private const string StateIntroRise = "BossWorm_IntroRise";
    private const string StateIntroRoar = "BossWorm_IntroRoar";

    [SerializeField] private BossAudioManager bossAudioManager;

    private WormBossController _boss;

    /// <summary>
    /// Caches the required boss controller before the room handler requests an intro sequence.
    /// </summary>
    private void Awake()
    {
        _boss = GetComponent<WormBossController>();
    }

    /// <summary>
    /// Surfaces the boss and waits for the authored intro-rise clip, with a one-second timing fallback.
    /// </summary>
    public IEnumerator RunRiseRoutine()
    {
        _boss.Cutscene_SetUnderground(false);
        _boss.Cutscene_PlayAnimatorState(StateIntroRise);
        float riseLen = _boss.Cutscene_GetAnimatorClipLength(StateIntroRise, 1f);
        yield return new WaitForSeconds(riseLen);
    }

    /// <summary>
    /// Plays the intro roar and synchronized audio, adding repeated camera rumble after the configured lead.
    /// </summary>
    public IEnumerator RunGrowlRoutine(CameraController camera, float shakeIntensity, float shakeInterval, float shakeLeadAfterGrowlStarts)
    {
        _boss.Cutscene_PlayAnimatorState(StateIntroRoar);
        float growlLen = _boss.Cutscene_GetAnimatorClipLength(StateIntroRoar, 1.65f);
        float growlStart = Time.time;
        float shakeBegin = growlStart + Mathf.Max(0f, shakeLeadAfterGrowlStarts);
        float nextShake = shakeBegin;

        if (!bossAudioManager)
            bossAudioManager = FindFirstObjectByType<BossAudioManager>();
        if (bossAudioManager)
            bossAudioManager.NotifyIntroGrowlStarted(growlLen);

        while (Time.time - growlStart < growlLen)
        {
            float now = Time.time;
            if (camera && now >= shakeBegin && now >= nextShake)
            {
                camera.ShakeRumble(shakeIntensity);
                nextShake = now + shakeInterval;
            }

            yield return null;
        }
    }
}
