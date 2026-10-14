using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 플레이어가 구역에 들어오면 연결된 함정들을 발동시키는 보이지 않는 스위치.
/// "저기 지나가면 천장 블록이 떨어진다" 같은 고양이마리오식 함정의 시작점.
/// Is Trigger BoxCollider2D와 함께 쓴다. 한 번 발동하면 플레이어가 죽어 리셋될 때까지 다시 발동하지 않는다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TrapTrigger : TrapBase
{
    [Header("Targets")]
    [SerializeField] private TrapBase[] targets;
    [Tooltip("밟은 뒤 이 시간(초)이 지나서 함정을 발동. 0이면 즉시")]
    [SerializeField] private float delay;

    [Header("Feedback")]
    [Tooltip("발동 순간 코믹한 '뿅!' 효과음. 깜짝 함정에 어울림 (경고음이 있는 함정에는 끄는 게 나음)")]
    [SerializeField] private bool playSurpriseSound;

    [Header("Custom")]
    [Tooltip("발동할 때 추가로 하고 싶은 것 (오브젝트 켜기 등)")]
    [SerializeField] private UnityEvent onFired;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerHealth>() == null) return;
        Activate();
    }

    protected override void OnActivate()
    {
        StartCoroutine(FireRoutine());
    }

    protected override void OnReset() { }

    private IEnumerator FireRoutine()
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (playSurpriseSound) GameSfx.PlayAt(GameSfxId.TrapSurprise, transform.position);

        foreach (var target in targets)
            if (target != null) target.Activate();

        onFired?.Invoke();
    }

    private void OnDrawGizmos()
    {
        // 어떤 함정을 건드리는지 선으로 보여줌
        if (targets == null) return;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f);
        foreach (var target in targets)
            if (target != null) Gizmos.DrawLine(transform.position, target.transform.position);
    }
}
