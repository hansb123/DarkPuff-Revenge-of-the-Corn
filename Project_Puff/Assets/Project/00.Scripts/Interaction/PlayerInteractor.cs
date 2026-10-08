using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어에 붙이는 상호작용 담당. 범위 안의 IInteractable 중 가장 가까운 것을 골라
/// 말풍선을 띄우고, 상호작용 키를 누르면 Interact()를 호출한다.
/// 플레이어에 Rigidbody2D가 있어야 트리거 감지가 된다(PlayerController가 이미 요구함).
/// 상호작용 대상은 Is Trigger 콜라이더를 가져야 한다.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;
    [Tooltip("Platformer 맵(= InputMaps.Platformer) 안의 액션 이름. 기본 InputSystem_Actions는 Interact")]
    [SerializeField] private string interactActionName = "Interact";

    [Header("Prompt")]
    [SerializeField] private InteractionPrompt prompt;

    private readonly List<IInteractable> inRange = new List<IInteractable>();
    private InputAction interactAction;
    private IInteractable current;

    private void Awake()
    {
        if (inputActions != null)
        {
            interactAction = inputActions.FindAction($"{InputMaps.Platformer}/{interactActionName}", false);
            if (interactAction == null)
                Debug.LogWarning($"[PlayerInteractor] '{InputMaps.Platformer}/{interactActionName}' 액션을 찾지 못했습니다.", this);
        }
        else
        {
            Debug.LogError("[PlayerInteractor] Input Actions 에셋이 연결되지 않았습니다.", this);
        }
    }

    private void OnEnable()
    {
        // started: 키를 누르는 순간. (기본 에셋의 Interact는 Hold가 걸려 있어 performed는 길게 눌러야 해서 started를 씀)
        if (interactAction != null) interactAction.started += OnInteractStarted;
    }

    private void OnDisable()
    {
        if (interactAction != null) interactAction.started -= OnInteractStarted;
        inRange.Clear();
        current = null;
        if (prompt != null) prompt.Hide();
    }

    private void Update()
    {
        // 가장 가까운 상호작용 가능 대상 선택
        IInteractable best = null;
        float bestDist = float.MaxValue;

        for (int i = inRange.Count - 1; i >= 0; i--)
        {
            var it = inRange[i];
            var mb = it as MonoBehaviour;
            if (mb == null) { inRange.RemoveAt(i); continue; }   // 파괴된 대상 정리
            if (!it.CanInteract) continue;

            float dist = ((Vector2)mb.transform.position - (Vector2)transform.position).sqrMagnitude;
            if (dist < bestDist) { bestDist = dist; best = it; }
        }

        if (best != current)
        {
            current = best;
            if (prompt != null)
            {
                if (current != null) prompt.Show(current.PromptKind);
                else prompt.Hide();
            }
        }
    }

    private void OnInteractStarted(InputAction.CallbackContext ctx)
    {
        if (current == null || !current.CanInteract) return;

        if (prompt != null) prompt.PlayPressed();
        current.Interact(this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var it = other.GetComponentInParent<IInteractable>();
        if (it != null && !inRange.Contains(it)) inRange.Add(it);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var it = other.GetComponentInParent<IInteractable>();
        if (it != null) inRange.Remove(it);
    }
}
