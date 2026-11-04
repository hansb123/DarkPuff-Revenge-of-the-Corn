using UnityEngine;

/// <summary>
/// 낙뢰 1발. 프리팹 루트를 "바닥 한 점"에 두고 ThunderCloud(또는 함정)가 Instantiate + Init 한다.
/// 1) 경고 단계: 바닥 표시(warnVisual)가 깜빡임
/// 2) 낙뢰 단계: 번개(boltVisual)가 번쩍이고 폭 안의 플레이어에게 피해 (한 번)
///
/// 프리팹 구조:
///   CloudLightningStrike (이 스크립트)
///     ├ Warn  (SpriteRenderer, fx_warn_1/2)         ← warnVisual
///     ├ Bolt  (SpriteRenderer, fx_bolt_1~3, 스프라이트 Pivot = Bottom Center) ← boltVisual
///     └ Impact(SpriteRenderer, fx_bolt_hit)         ← impactVisual (선택)
/// </summary>
public class CloudLightningStrike : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private SpriteRenderer warnVisual;
    [SerializeField] private Sprite[] warnFrames;          // fx_warn_1, 2 (번갈아 깜빡)
    [SerializeField] private float warnBlinkInterval = 0.12f;
    [SerializeField] private SpriteRenderer boltVisual;
    [SerializeField] private Sprite[] boltFrames;          // fx_bolt_1~3
    [SerializeField] private float boltFrameInterval = 0.05f;
    [SerializeField] private SpriteRenderer impactVisual;  // 선택

    [Header("Bolt")]
    [SerializeField] private float boltHeight = 14f;       // 번개 기둥 길이 (화면 위까지 닿게)
    [SerializeField] private bool shakeCamera = true;

    private LayerMask playerLayer;
    private int damage;
    private float width, warnTime, strikeTime;
    private float timer;
    private bool striking, cancelled;
    private LightningRod rod;
    private float frameTimer;
    private int frameIndex;

    /// <summary>pos = 바닥 위 번개가 떨어질 점. rod가 있으면 그쪽으로 낙하(연출용 신호)</summary>
    public void Init(int dmg, float w, float warn, float strike, LayerMask players, LightningRod targetRod = null)
    {
        damage = dmg; width = w; warnTime = warn; strikeTime = strike; playerLayer = players; rod = targetRod;
        timer = warnTime;
        striking = false;
        if (boltVisual != null) boltVisual.enabled = false;
        if (impactVisual != null) impactVisual.enabled = false;
        if (warnVisual != null)
        {
            warnVisual.enabled = true;
            // 표시 폭을 낙뢰 폭에 맞춤
            if (warnVisual.sprite != null)
            {
                float sw = warnVisual.sprite.bounds.size.x;
                if (sw > 0.01f) warnVisual.transform.localScale = new Vector3(Mathf.Max(width * 1.4f, 0.5f) / sw, warnVisual.transform.localScale.y, 1f);
            }
        }
    }

    /// <summary>번개를 쏘기 전에 취소 (구름이 죽었을 때)</summary>
    public void Cancel()
    {
        if (striking) return;
        cancelled = true;
        Destroy(gameObject);
    }

    private void Update()
    {
        if (cancelled) return;
        timer -= Time.deltaTime;

        if (!striking)
        {
            // 경고: 시간이 줄수록 깜빡임이 빨라짐
            if (warnVisual != null && warnFrames != null && warnFrames.Length > 0)
            {
                float rate = Mathf.Lerp(warnBlinkInterval, warnBlinkInterval * 0.35f, 1f - Mathf.Clamp01(timer / Mathf.Max(0.01f, warnTime)));
                frameTimer -= Time.deltaTime;
                if (frameTimer <= 0f)
                {
                    frameTimer = rate;
                    frameIndex = (frameIndex + 1) % warnFrames.Length;
                    warnVisual.sprite = warnFrames[frameIndex];
                }
            }
            if (timer <= 0f) BeginStrike();
        }
        else
        {
            if (boltVisual != null && boltFrames != null && boltFrames.Length > 0)
            {
                frameTimer -= Time.deltaTime;
                if (frameTimer <= 0f)
                {
                    frameTimer = boltFrameInterval;
                    frameIndex = (frameIndex + 1) % boltFrames.Length;
                    boltVisual.sprite = boltFrames[frameIndex];
                }
            }
            if (timer <= 0f) Destroy(gameObject);
        }
    }

    private void BeginStrike()
    {
        striking = true;
        timer = strikeTime;
        frameIndex = 0; frameTimer = 0f;

        if (warnVisual != null) warnVisual.enabled = false;

        if (boltVisual != null)
        {
            boltVisual.enabled = true;
            if (boltFrames != null && boltFrames.Length > 0) boltVisual.sprite = boltFrames[0];
            if (boltVisual.sprite != null)
            {
                float h = boltVisual.sprite.bounds.size.y;
                float w = boltVisual.sprite.bounds.size.x;
                // 폭은 원본 비율 유지 + 피격 폭에 약간 맞춤, 길이는 boltHeight
                if (h > 0.01f) boltVisual.transform.localScale = new Vector3(Mathf.Max(width / Mathf.Max(w, 0.01f) * 0.8f, 0.6f), boltHeight / h, 1f);
            }
        }
        if (impactVisual != null) impactVisual.enabled = true;

        GameSfx.PlayAt(GameSfxId.EnemyRangedShoot, transform.position);
        if (shakeCamera) EagleCamShake.Add(0.12f, 0.2f);
        if (rod != null) rod.Struck();

        // 피해: 바닥 점에서 위로 boltHeight, 폭 width 영역 (한 번만)
        if (rod == null)
        {
            Vector2 center = (Vector2)transform.position + Vector2.up * (boltHeight * 0.5f);
            var col = Physics2D.OverlapBox(center, new Vector2(width, boltHeight), 0f, playerLayer);
            if (col != null)
            {
                var target = col.GetComponentInParent<IDamageable>();
                if (target != null) target.TakeDamage(damage, transform.position);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position + Vector3.up * (boltHeight * 0.5f), new Vector3(width <= 0 ? 1.6f : width, boltHeight, 0f));
    }
}
