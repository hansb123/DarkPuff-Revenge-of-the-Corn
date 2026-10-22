using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 플레이어가 이 구역(미니게임 중앙)에 들어오면 등장 연출(ChickenIntroSequence 또는 MechaIntroSequence)을 시작한다.
/// 구성: 중앙에 빈 오브젝트 + Collider2D(Is Trigger 켬) + 이 컴포넌트. Sequence 칸에 등장 연출 컴포넌트(IMiniGameIntro)를 연결. 비워 두면 씬에서 자동으로 찾음.
/// 한 번만 발동한다. 죽어서 처음부터 다시 시작해도 연출은 다시 나오지 않고,
/// 이미 시작된 패턴은 PatternRunner가 알아서 다시 시작한다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class MiniGameStartTrigger : MonoBehaviour
{
    [Tooltip("등장 연출 컴포넌트 (ChickenIntroSequence / MechaIntroSequence). IMiniGameIntro를 구현한 것만 동작")]
    [SerializeField] private MonoBehaviour sequence;
    private IMiniGameIntro intro;
    [Tooltip("구역에 들어오고 나서 연출을 시작하기까지 기다리는 시간")]
    [SerializeField] private float delay = 0.2f;
    [Tooltip("켜면 연출이 끝난 뒤에도 구역을 다시 밟아도 발동하지 않음 (기본)")]
    [SerializeField] private bool once = true;
    [Tooltip("연출 시작과 함께 호출 (예: 문을 닫는 오브젝트 켜기)")]
    [SerializeField] private UnityEvent onTriggered;

    private bool triggered;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Awake()
    {
        intro = sequence as IMiniGameIntro;
        if (intro == null)
        {
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (mb is IMiniGameIntro found) { intro = found; break; }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered && once) return;
        if (other.GetComponentInParent<PlayerHealth>() == null) return;   // 플레이어만

        triggered = true;
        StartCoroutine(Begin());
    }

    private IEnumerator Begin()
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        onTriggered?.Invoke();
        if (intro != null) intro.Play();
        else Debug.LogWarning("[MiniGameStartTrigger] 등장 연출(IMiniGameIntro)을 찾지 못했습니다.", this);
    }

    private void OnValidate()
    {
        if (sequence != null && !(sequence is IMiniGameIntro))
        {
            Debug.LogWarning("[MiniGameStartTrigger] Sequence에는 ChickenIntroSequence/MechaIntroSequence처럼 IMiniGameIntro를 구현한 컴포넌트를 넣어 주세요.", this);
            sequence = null;
        }
    }

    private void OnDrawGizmos()
    {
        var col = GetComponent<Collider2D>();
        if (col == null) return;
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.35f);
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);
    }
}
