using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyBase))]
public sealed class EliteModifier : MonoBehaviour
{
    [SerializeField] private float scaleMultiplier = 1.45f;
    [SerializeField] private float healthMultiplier = 2.2f;
    [SerializeField] private float damageMultiplier = 2.5f;
    [SerializeField] private float shieldReserve = 18f;
    [SerializeField] private Color rimColor = default;
    [SerializeField] private float rimPulseHz = 1.4f;

    private EnemyBase _enemy;
    private SpriteRenderer _primarySprite;
    private GameObject _rimChild;
    private float _currentShield;

    public bool IsElite => true;
    public float ShieldNormalized => shieldReserve > 0f ? Mathf.Clamp01(_currentShield / shieldReserve) : 0f;
    public bool HasShield => _currentShield > 0f;
    public float OutgoingDamageMultiplier => damageMultiplier;

    private void Awake()
    {
        _enemy = GetComponent<EnemyBase>();
        if (rimColor == default)
            rimColor = GameColors.EliteAccent;
    }

    private void Start()
    {
        _currentShield = shieldReserve;
        transform.localScale *= scaleMultiplier;

        if (_enemy != null)
            _enemy.ApplyRuntimeScaling(healthMultiplier, 1f, damageMultiplier);

        _primarySprite = GetComponentInChildren<SpriteRenderer>();
        SpawnRimChild();

        AudioManager.Instance?.PlayEliteSpawn();
    }

    private void Update()
    {
        if (_rimChild == null) return;

        float t = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * rimPulseHz * Mathf.PI));
        var color = rimColor;
        color.a = t * (HasShield ? 1f : 0.45f);
        var sr = _rimChild.GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.color = color;
    }

    private void SpawnRimChild()
    {
        if (_primarySprite == null || _primarySprite.sprite == null)
            return;

        var go = new GameObject("EliteRim");
        go.transform.SetParent(_primarySprite.transform, false);
        go.transform.localScale = Vector3.one * 1.08f;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _primarySprite.sprite;
        sr.color = rimColor;
        sr.sortingLayerID = _primarySprite.sortingLayerID;
        sr.sortingOrder = _primarySprite.sortingOrder - 1;
        _rimChild = go;
    }

    public float AbsorbDamage(float incoming)
    {
        if (incoming <= 0f || _currentShield <= 0f)
            return incoming;

        float absorbed = Mathf.Min(_currentShield, incoming);
        _currentShield -= absorbed;
        return incoming - absorbed;
    }
}
