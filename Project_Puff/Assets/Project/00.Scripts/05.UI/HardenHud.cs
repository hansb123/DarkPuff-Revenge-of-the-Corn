using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 단단해지기 HUD. 슬롯 아이콘(준비/발동/쿨타임) + 쿨타임 시계방향 오버레이 + 지속 바 + 준비 번쩍.
/// 구성 예 (이 스크립트는 항상 켜져 있는 부모에 붙이고, Visual 은 그 자식 컨테이너):
///   HardenHud(이 스크립트)
///   └ Visual (해금 전에는 꺼짐)  ← 'Visual Root' 에 연결
///       ├ Slot        (Image: 상태에 따라 hud_hard_ready / active / cooldown 으로 교체)
///       ├ CooldownMask(Image: hud_hard_cooldown_mask, Image Type = Filled / Radial 360 / Clockwise)
///       ├ Keycap      (Image: hud_hard_keycap) ─ KeyLabel (Text 'C')
///       ├ Bar         (= Bar Root) ─ BarFrame(hud_hard_bar_frame) ─ BarFill(hud_hard_bar_fill, Filled / Horizontal)
///       └ ReadyFlash  (Image: hud_hard_ready_flash_1~3, 처음엔 비활성)
/// </summary>
public class HardenHud : MonoBehaviour
{
    [SerializeField] private HardenAbility ability;
    [Tooltip("해금 전에는 숨길 컨테이너 (이 스크립트가 붙은 오브젝트의 자식으로)")]
    [SerializeField] private GameObject visualRoot;

    [Header("슬롯")]
    [SerializeField] private Image slot;
    [SerializeField] private Sprite readySprite;      // hud_hard_ready
    [SerializeField] private Sprite activeSprite;     // hud_hard_active
    [SerializeField] private Sprite cooldownSprite;   // hud_hard_cooldown

    [Header("쿨타임 / 지속 바")]
    [Tooltip("Image Type = Filled, Radial 360, Clockwise. 쿨타임이 줄어들수록 시계 방향으로 걷힌다")]
    [SerializeField] private Image cooldownMask;
    [Tooltip("Image Type = Filled, Horizontal. 발동 중 줄어든다")]
    [SerializeField] private Image barFill;
    [Tooltip("바 틀 + 채움을 묶은 오브젝트. 발동 중에만 보임")]
    [SerializeField] private GameObject barRoot;

    [Header("준비 번쩍")]
    [SerializeField] private Image readyFlash;
    [SerializeField] private Sprite[] flashFrames;    // hud_hard_ready_flash_1~3
    [SerializeField] private float flashTime = 0.35f;

    private void Awake()
    {
        if (ability == null) ability = FindFirstObjectByType<HardenAbility>();
        if (readyFlash != null) readyFlash.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (ability != null) ability.BecameReady += OnReady;
    }

    private void OnDisable()
    {
        if (ability != null) ability.BecameReady -= OnReady;
    }

    private void Update()
    {
        if (ability == null) return;
        var st = ability.Current;

        if (visualRoot != null && visualRoot != gameObject)
        {
            bool show = st != HardenAbility.State.Locked;
            if (visualRoot.activeSelf != show) visualRoot.SetActive(show);
        }

        if (slot != null)
        {
            var s = st == HardenAbility.State.Active ? activeSprite
                  : st == HardenAbility.State.Cooldown ? cooldownSprite
                  : readySprite;
            if (s != null) slot.sprite = s;
        }

        if (cooldownMask != null)
        {
            cooldownMask.fillAmount = ability.CooldownRemaining01;
            cooldownMask.enabled = st == HardenAbility.State.Cooldown;
        }
        if (barRoot != null) barRoot.SetActive(st == HardenAbility.State.Active);
        if (barFill != null) barFill.fillAmount = ability.ActiveRemaining01;
    }

    private void OnReady()
    {
        if (readyFlash == null || flashFrames == null || flashFrames.Length == 0) return;
        StopAllCoroutines();
        StartCoroutine(Flash());
    }

    private IEnumerator Flash()
    {
        readyFlash.gameObject.SetActive(true);
        for (float t = 0f; t < flashTime; t += Time.unscaledDeltaTime)
        {
            readyFlash.sprite = flashFrames[Mathf.Min(flashFrames.Length - 1, Mathf.FloorToInt(t / flashTime * flashFrames.Length))];
            var c = readyFlash.color; c.a = 1f - t / flashTime; readyFlash.color = c;
            yield return null;
        }
        readyFlash.gameObject.SetActive(false);
    }
}
