using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 활공 능력 (닭 깃털을 먹으면 해금). 플레이어 오브젝트에 붙인다.
/// 조작: 공중에서 점프 버튼을 (다시) 눌러서 누르고 있는 동안, 내려갈 때 천천히 떨어진다. 버튼을 떼거나 땅에 닿으면 끝.
/// 실제 물리 적용은 PlayerController가 한다 (이 컴포넌트는 수치와 해금 여부, 표시를 담당).
/// </summary>
public class GlideAbility : MonoBehaviour
{
    [Header("수치")]
    [Tooltip("활공 중 최대 낙하 속도 (양수). 작을수록 둥실둥실")]
    [SerializeField] private float fallSpeed = 2.2f;
    [Tooltip("활공 중 중력 배율 (1 = 원래, 작을수록 천천히 가속)")]
    [SerializeField] private float gravityMultiplier = 0.35f;

    [Header("표시")]
    [Tooltip("활공 중에만 켜지는 오브젝트 (등에서 펼쳐지는 깃털/날개 스프라이트 등). 비워도 됨")]
    [SerializeField] private GameObject glideVisual;
    [SerializeField] private UnityEvent glideStarted;
    [SerializeField] private UnityEvent glideEnded;

    [Header("테스트")]
    [Tooltip("켜면 해금 여부와 상관없이 활공 가능 (에디터 테스트용). 출시 전에 끄기")]
    [SerializeField] private bool forceUnlocked = false;

    public float FallSpeed => fallSpeed;
    public float GravityMultiplier => gravityMultiplier;
    public bool CanGlide => forceUnlocked || AbilityProgress.GlideUnlocked;
    public bool IsGliding { get; private set; }

    private void Awake()
    {
        if (glideVisual != null) glideVisual.SetActive(false);
    }

    public void SetGliding(bool on)
    {
        if (on == IsGliding) return;
        IsGliding = on;
        if (glideVisual != null) glideVisual.SetActive(on);
        if (on) { GameSfx.Play(GameSfxId.GlideStart); glideStarted?.Invoke(); }
        else glideEnded?.Invoke();
    }

    [ContextMenu("Debug/Unlock Glide")] private void DebugUnlock() => AbilityProgress.UnlockGlide();
    [ContextMenu("Debug/Lock Glide")] private void DebugLock() => AbilityProgress.LockGlide();
}
