using System.Collections;
using UnityEngine;

/// <summary>
/// Self-contained lifetime animation for a runtime-created boss rubble puff.
/// Kept in its own file so Unity can associate this MonoBehaviour with its script asset.
/// </summary>
public sealed class BossRubblePuffFx : MonoBehaviour
{
    /// <summary>
    /// Starts this puff's one-shot animation; it owns and destroys the supplied renderer's object at completion.
    /// </summary>
    public void Run(SpriteRenderer sr, Color baseColor, float lifetime, Vector2 outwardVelocity)
    {
        StartCoroutine(RunRoutine(sr, baseColor, lifetime, outwardVelocity));
    }

    /// <summary>
    /// Expands, spins, damps velocity, and fades the puff over scaled time before releasing its object.
    /// </summary>
    private IEnumerator RunRoutine(SpriteRenderer sr, Color baseColor, float lifetime, Vector2 velocity)
    {
        if (!sr) yield break;
        float t = 0f;
        Vector3 startScale = transform.localScale;
        Vector3 endScale = startScale * Random.Range(1.12f, 1.42f);
        float spin = Random.Range(-180f, 180f);
        float ang = Random.Range(0f, 360f);
        while (t < lifetime && sr)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / lifetime);
            Color c = baseColor;
            c.a = Mathf.Lerp(baseColor.a, 0f, u * u);
            sr.color = c;
            transform.localScale = Vector3.Lerp(startScale, endScale, Mathf.SmoothStep(0f, 1f, u));
            velocity *= Mathf.Exp(-8f * Time.deltaTime);
            transform.position += (Vector3)(velocity * Time.deltaTime);
            ang += spin * Time.deltaTime;
            transform.rotation = Quaternion.Euler(0f, 0f, ang);
            yield return null;
        }
        if (sr) Destroy(sr.gameObject);
    }
}
