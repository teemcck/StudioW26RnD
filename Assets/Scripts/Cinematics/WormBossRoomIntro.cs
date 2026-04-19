using System.Collections;
using UnityEngine;

[RequireComponent(typeof(WormBossController))]
public sealed class WormBossRoomIntro : MonoBehaviour, IBossRoomIntroSequence
{
    private const string StateIntroRise = "BossWorm_IntroRise";
    private const string StateIntroRoar = "BossWorm_IntroRoar";

    [SerializeField] private BossAudioManager bossAudioManager;

    private WormBossController _boss;

    private void Awake()
    {
        _boss = GetComponent<WormBossController>();
    }

    public IEnumerator RunRiseRoutine()
    {
        _boss.Cutscene_SetUnderground(false);
        _boss.Cutscene_PlayAnimatorState(StateIntroRise);
        float riseLen = _boss.Cutscene_GetAnimatorClipLength(StateIntroRise, 1f);
        yield return new WaitForSeconds(riseLen);
    }

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
