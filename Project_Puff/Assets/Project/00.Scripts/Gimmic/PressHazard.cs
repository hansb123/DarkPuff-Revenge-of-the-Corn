using System.Collections;
using UnityEngine;

/// <summary>
/// 프레스. 천장에 매달린 기계가 주기적으로 예고 → 내려찍기 → 잠시 누름 → 천천히 올라감을 반복한다.
/// 구성 (빈 오브젝트 Press 아래):
///   Press (이 스크립트, 위치 = 천장 마운트 아래 끝)
///   ├ Mount  (press_mount 스프라이트, 장식)
///   ├ Shaft  (SpriteRenderer press_shaft, Draw Mode = Tiled)  ← Shaft 칸에 연결, 길이는 자동
///   ├ Head   (press_head, BoxCollider2D Is Trigger + Obstacle)  ← Head 칸에 연결
///   └ Light  (SpriteRenderer, 선택)  ← 경고등
/// 머리는 내려와 있는 동안만 위험하다 (Obstacle.DamageEnabled 을 이 스크립트가 켜고 끈다).
/// 소리는 GameSfxBank 의 PressSlam / PressRise (press_slam.wav, press_rise.wav) 를 쓴다.
/// 죽고 다시 시작하면 TrapBase 가 처음 상태로 되돌린다. Initial Delay 로 여러 대의 박자를 엇갈리게 한다.
/// </summary>
public class PressHazard : TrapBase
{
    [Header("부품")]
    [SerializeField] private Transform head;
    [Tooltip("Draw Mode = Tiled 인 SpriteRenderer. 마운트와 머리 사이를 늘려서 채운다")]
    [SerializeField] private SpriteRenderer shaft;
    [SerializeField] private float shaftWidth = 0.5f;
    [Tooltip("머리 스프라이트 높이 (유닛). press_head 는 1")]
    [SerializeField] private float headHeight = 1f;
    [SerializeField] private Obstacle obstacle;

    [Header("박자 (초)")]
    [Tooltip("처음 한 번만 기다리는 시간. 여러 대를 엇갈리게 할 때 사용")]
    [SerializeField] private float initialDelay;
    [Tooltip("위에서 대기 (안전)")]
    [SerializeField] private float idleTime = 1.5f;
    [Tooltip("내려오기 전에 흔들리는 예고")]
    [SerializeField] private float warnTime = 0.6f;
    [Tooltip("내려찍는 시간 (짧을수록 무섭다)")]
    [SerializeField] private float slamTime = 0.12f;
    [Tooltip("내려와서 누르고 있는 시간 (이때까지 위험)")]
    [SerializeField] private float downTime = 0.5f;
    [Tooltip("올라가는 시간")]
    [SerializeField] private float riseTime = 0.9f;

    [Header("거리")]
    [Tooltip("머리가 내려가는 거리 (유닛). 바닥에 딱 닿도록 맞춘다")]
    [SerializeField] private float dropDistance = 4f;
    [SerializeField] private float shakeAmount = 0.05f;

    [Header("연출 (선택)")]
    [SerializeField] private SpriteRenderer warnLight;
    [SerializeField] private Sprite lightOff;
    [SerializeField] private Sprite lightOn;
    [Tooltip("내려찍을 때 바닥에 터지는 먼지 이펙트 프리펩 (OneShotEffect)")]
    [SerializeField] private OneShotEffect slamEffect;
    [SerializeField] private float effectWidth = 2f;
    [SerializeField] private Vector2 effectOffset = new Vector2(0f, -0.4f);

    [Header("시작")]
    [Tooltip("켜면 씬 시작과 리셋 때 스스로 반복. 끄면 TrapTrigger 로 한 번 발동")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private bool loop = true;

    private Vector3 headRestLocal;

    protected override void Awake()
    {
        base.Awake();
        if (head != null) headRestLocal = head.localPosition;
        if (obstacle == null && head != null) obstacle = head.GetComponent<Obstacle>();
        if (obstacle == null)
            Debug.LogWarning($"[PressHazard] '{name}'의 Head에 Obstacle 컴포넌트가 없어서 닿아도 다치지 않습니다.", this);
    }

    protected override void Start()
    {
        base.Start();
        ApplyIdle();
        if (autoStart) Activate();
    }

    protected override void OnActivate() => StartCoroutine(Cycle());

    protected override void OnReset()
    {
        if (head != null) head.localPosition = headRestLocal;
        ApplyIdle();
        if (autoStart) Activate();
    }

    private void ApplyIdle()
    {
        if (obstacle != null) obstacle.DamageEnabled = false;
        SetLight(false);
    }

    private IEnumerator Cycle()
    {
        if (head == null) yield break;
        if (initialDelay > 0f) yield return new WaitForSeconds(initialDelay);

        Vector3 up = headRestLocal;
        Vector3 down = headRestLocal + Vector3.down * dropDistance;

        do
        {
            head.localPosition = up;
            yield return new WaitForSeconds(idleTime);

            // 예고: 흔들림 + 경고등 깜빡임
            GameSfx.PlayAt(GameSfxId.TrapWarn, head.position);
            float t = 0f;
            while (t < warnTime)
            {
                t += Time.deltaTime;
                head.localPosition = up + new Vector3(Mathf.Sin(t * 70f) * shakeAmount, 0f, 0f);
                SetLight(Mathf.FloorToInt(t * 12f) % 2 == 0);
                yield return null;
            }
            head.localPosition = up;
            SetLight(true);

            // 내려찍기
            if (obstacle != null) obstacle.DamageEnabled = true;
            t = 0f;
            while (t < slamTime)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / Mathf.Max(0.01f, slamTime));
                head.localPosition = Vector3.Lerp(up, down, u * u);   // 가속
                yield return null;
            }
            head.localPosition = down;
            GameSfx.PlayAt(GameSfxId.PressSlam, head.position);
            SpawnDust();

            yield return new WaitForSeconds(downTime);

            // 올라가기 (올라갈 때는 안전)
            if (obstacle != null) obstacle.DamageEnabled = false;
            SetLight(false);
            GameSfx.PlayAt(GameSfxId.PressRise, head.position);
            t = 0f;
            while (t < riseTime)
            {
                t += Time.deltaTime;
                head.localPosition = Vector3.Lerp(down, up, Mathf.Clamp01(t / Mathf.Max(0.01f, riseTime)));
                yield return null;
            }
            head.localPosition = up;
        }
        while (loop);
    }

    private void LateUpdate()
    {
        if (shaft == null || head == null) return;
        // 마운트(이 오브젝트 원점)에서 머리 윗면까지 축을 늘린다
        float headTop = transform.InverseTransformPoint(head.position).y + headHeight * 0.5f;
        float len = Mathf.Max(0f, -headTop);
        shaft.drawMode = SpriteDrawMode.Tiled;
        shaft.tileMode = SpriteTileMode.Continuous;
        shaft.size = new Vector2(shaftWidth, len);
        shaft.transform.localPosition = new Vector3(0f, -len * 0.5f, 0f);
    }

    private void SetLight(bool on)
    {
        if (warnLight == null) return;
        var s = on ? lightOn : lightOff;
        if (s != null) warnLight.sprite = s;
    }

    private void SpawnDust()
    {
        if (slamEffect == null || head == null) return;
        var fx = Instantiate(slamEffect);
        fx.Play((Vector2)head.position + effectOffset, false, effectWidth);
    }

    private void OnDrawGizmosSelected()
    {
        if (head == null) return;
        Vector3 rest = Application.isPlaying ? transform.TransformPoint(headRestLocal) : head.position;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(rest, rest + Vector3.down * dropDistance);
        Gizmos.DrawWireCube(rest + Vector3.down * dropDistance, new Vector3(1.5f, headHeight, 0f));
    }
}