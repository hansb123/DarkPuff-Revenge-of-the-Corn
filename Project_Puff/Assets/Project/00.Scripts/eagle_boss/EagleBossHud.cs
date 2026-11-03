using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 독수리 보스 체력게이지 (2·3페이즈용). 1페이즈(시간제)에서는 숨고, 그 자리에는 분노 게이지(AngerGaugeHud)가 나온다.
///  - 체력이 깎이면 검은 막대가 줄고, 방금 깎인 부분이 회색 잔상으로 남았다가 따라 줄어든다
///  - 맞을 때 게이지가 살짝 흔들린다
///  - 아이콘: 2페이즈 화남 / 3페이즈 폭발 직전 / 그로기일 땐 어질어질
///  - 페이즈가 시작될 때 체력이 가득 차오르는 연출
/// UI 구성 (Canvas 아래, 이 스크립트가 붙은 Root)
///  Root ─ Back(hp_back) ─ Ghost(hp_ghost, Filled/Horizontal) ─ Fill(hp_fill, Filled/Horizontal) ─ Frame(hp_frame)
///       ├ Icon(icon_angry)  ├ NamePlate(hp_nameplate)
/// 각 Image 의 Image Type 은 Filled, Fill Method 는 Horizontal, Fill Origin 은 Left.
/// </summary>
public class EagleBossHud : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private EagleBoss boss;
    [SerializeField] private Image fill;
    [Tooltip("방금 깎인 만큼 회색으로 남는 잔상 (Fill 보다 뒤에)")]
    [SerializeField] private Image ghost;
    [SerializeField] private Image icon;
    [Tooltip("(선택) 보스 이름 글자")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private string bossName = "폭풍의 독수리";

    [Header("아이콘")]
    [SerializeField] private Sprite phase2Icon;     // icon_angry
    [SerializeField] private Sprite phase3Icon;     // icon_furious
    [SerializeField] private Sprite groggyIcon;     // icon_groggy
    [Tooltip("이 페이즈 번호 이상이면 3페이즈 아이콘 (0 = 1페이즈, 2 = 3페이즈)")]
    [SerializeField] private int furiousFromPhase = 2;

    [Header("연출")]
    [Tooltip("체력이 가득 차오르는 시간 (페이즈 시작)")]
    [SerializeField] private float fillInTime = 0.8f;
    [Tooltip("깎인 뒤 잔상이 따라 줄기 시작하기까지 / 줄어드는 속도")]
    [SerializeField] private float ghostDelay = 0.35f;
    [SerializeField] private float ghostSpeed = 1.2f;
    [SerializeField] private float hitShakeTime = 0.18f;
    [SerializeField] private float hitShakeDistance = 8f;
    [Tooltip("그로기 동안 막대가 이 색으로 깜빡임 (끄려면 알파 0)")]
    [SerializeField] private Color groggyTint = new Color(1f, 0.85f, 0.2f, 1f);

    private float target = 1f;   // 실제 비율
    private float shown = 1f;    // 막대에 보이는 비율
    private float ghostShown = 1f;
    private float ghostHoldUntil;
    private bool groggy;
    private Vector2 rootBase;
    private Coroutine shakeRoutine, fillInRoutine;
    private RectTransform Rt => (RectTransform)transform;

    private void Awake()
    {
        rootBase = Rt.anchoredPosition;
        if (boss == null) boss = FindFirstObjectByType<EagleBoss>();
        if (nameText != null) nameText.text = bossName;
        Show(false);
    }

    private void Start() => rootBase = Rt.anchoredPosition;

    private void OnEnable()
    {
        if (boss == null) return;
        boss.HpChanged += OnHp;
        boss.PhaseEntered += OnPhase;
        boss.GroggyStarted += OnGroggyStart;
        boss.GroggyEnded += OnGroggyEnd;
        boss.Defeated += OnDefeated;
    }

    private void OnDisable()
    {
        if (boss == null) return;
        boss.HpChanged -= OnHp;
        boss.PhaseEntered -= OnPhase;
        boss.GroggyStarted -= OnGroggyStart;
        boss.GroggyEnded -= OnGroggyEnd;
        boss.Defeated -= OnDefeated;
    }

    // ───────────── 이벤트 ─────────────

    private void OnPhase(int index)
    {
        groggy = false;
        if (boss.IsTimedPhase) { Show(false); return; }   // 1페이즈는 분노 게이지가 담당

        Show(true);
        SetIcon();
        if (fillInRoutine != null) StopCoroutine(fillInRoutine);
        fillInRoutine = StartCoroutine(FillIn());
    }

    private void OnHp(int cur, int max)
    {
        if (boss.IsTimedPhase) return;
        float r = max > 0 ? Mathf.Clamp01((float)cur / max) : 0f;
        bool damaged = r < target - 0.0001f;
        target = r;
        if (fillInRoutine != null) return;          // 차오르는 중에는 FillIn 이 처리
        shown = r;
        Apply();
        if (damaged)
        {
            ghostHoldUntil = Time.unscaledTime + ghostDelay;
            Shake();
        }
    }

    private void OnGroggyStart() { groggy = true; SetIcon(); }
    private void OnGroggyEnd() { groggy = false; SetIcon(); }
    private void OnDefeated() => Show(false);

    // ───────────── 표시 ─────────────

    private IEnumerator FillIn()
    {
        target = boss.PhaseMaxHp > 0 ? Mathf.Clamp01((float)boss.Hp / boss.PhaseMaxHp) : 1f;
        shown = ghostShown = 0f;
        Apply();
        for (float t = 0f; t < fillInTime; t += Time.unscaledDeltaTime)
        {
            shown = ghostShown = Mathf.Lerp(0f, target, Mathf.SmoothStep(0f, 1f, t / fillInTime));
            Apply();
            yield return null;
        }
        shown = ghostShown = target;
        Apply();
        fillInRoutine = null;
    }

    private void Update()
    {
        // 잔상: 잠깐 머문 뒤 실제 값까지 따라 내려옴
        if (ghostShown > shown && Time.unscaledTime >= ghostHoldUntil)
        {
            ghostShown = Mathf.MoveTowards(ghostShown, shown, ghostSpeed * Time.unscaledDeltaTime);
            Apply();
        }
        else if (ghostShown < shown) { ghostShown = shown; Apply(); }

        // 그로기 동안 막대 색 깜빡임
        if (fill != null)
        {
            if (groggy && groggyTint.a > 0f)
                fill.color = Color.Lerp(Color.white, groggyTint, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 12f));
            else fill.color = Color.white;
        }
    }

    private void Apply()
    {
        if (fill != null) fill.fillAmount = shown;
        if (ghost != null) ghost.fillAmount = ghostShown;
    }

    private void SetIcon()
    {
        if (icon == null) return;
        Sprite s = groggy && groggyIcon != null ? groggyIcon
                 : boss.PhaseIndex >= furiousFromPhase && phase3Icon != null ? phase3Icon
                 : phase2Icon;
        if (s != null) icon.sprite = s;
    }

    private void Show(bool on)
    {
        gameObject.SetActive(true);   // 컴포넌트는 항상 살려 두고 자식만 보이게 한다 (이벤트 구독 유지)
        for (int i = 0; i < transform.childCount; i++) transform.GetChild(i).gameObject.SetActive(on);
    }

    private void Shake()
    {
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    private IEnumerator ShakeRoutine()
    {
        for (float t = 0f; t < hitShakeTime; t += Time.unscaledDeltaTime)
        {
            float k = 1f - t / hitShakeTime;
            Rt.anchoredPosition = rootBase + new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * hitShakeDistance * k;
            yield return null;
        }
        Rt.anchoredPosition = rootBase;
        shakeRoutine = null;
    }
}
