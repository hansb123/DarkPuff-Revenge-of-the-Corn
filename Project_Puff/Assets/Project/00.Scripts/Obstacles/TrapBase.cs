using UnityEngine;

/// <summary>
/// 함정 공통 뼈대. "발동(Activate) → 동작 → 플레이어가 죽으면 원래대로(ResetTrap)" 흐름을 맡는다.
/// 함정이 많은 게임이라 죽고 다시 시작하면 함정이 전부 처음 상태로 돌아와야 한다.
/// 리셋은 RespawnManager.RespawnApplied(화면이 까만 동안)에 일어나서 되돌아가는 모습이 보이지 않는다.
/// 씬에 RespawnManager가 없으면 리셋 없이 한 번만 동작한다.
/// </summary>
public abstract class TrapBase : MonoBehaviour
{
    private RespawnManager manager;

    protected Vector3 startPosition;

    /// <summary>이번 생에서 이미 발동했는가 (한 번 발동하면 리셋될 때까지 다시 발동하지 않음)</summary>
    public bool Triggered { get; private set; }

    protected virtual void Awake()
    {
        startPosition = transform.position;
    }

    protected virtual void Start()
    {
        manager = RespawnManager.Instance;
        if (manager != null) manager.RespawnApplied += ResetTrap;
    }

    protected virtual void OnDestroy()
    {
        if (manager != null) manager.RespawnApplied -= ResetTrap;
    }

    /// <summary>함정을 발동. TrapTrigger가 부르거나, 코드/UnityEvent에서 직접 불러도 됨</summary>
    public void Activate()
    {
        if (Triggered) return;
        Triggered = true;
        OnActivate();
    }

    /// <summary>처음 상태로 되돌림 (진행 중인 동작도 멈춤)</summary>
    public void ResetTrap()
    {
        StopAllCoroutines();
        Triggered = false;
        transform.position = startPosition;
        OnReset();
    }

    protected abstract void OnActivate();

    /// <summary>위치는 이미 시작 위치로 돌아와 있음. 나머지(켜고 끄기, 데미지 상태 등)를 되돌린다</summary>
    protected abstract void OnReset();
}
