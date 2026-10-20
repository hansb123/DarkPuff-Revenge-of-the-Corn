using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;

/// <summary>
/// 어드레서블 동작 확인용 테스트 스크립트 (확인이 끝나면 지워도 됨).
/// 빈 오브젝트에 붙이고, Prefab Ref 칸에 어드레서블로 만든 프리팹을 드래그한다.
/// 플레이 중  F1 = 불러와서 생성,  F2 = 지우고 메모리 해제
/// </summary>
public class AddressableTest : MonoBehaviour
{
    [Tooltip("Addressable로 체크한 프리팹을 드래그 (체크 안 된 프리팹은 목록에 안 보임)")]
    [SerializeField] private AssetReferenceGameObject prefabRef;

    private GameObject instance;
    private bool loading;

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.f1Key.wasPressedThisFrame) Spawn();
        if (kb.f2Key.wasPressedThisFrame) Despawn();
    }

    private async void Spawn()
    {
        if (instance != null || loading) return;
        if (prefabRef == null || !prefabRef.RuntimeKeyIsValid())
        {
            Debug.LogWarning("[AddressableTest] Prefab Ref가 비어 있거나 어드레서블이 아닙니다.", this);
            return;
        }

        loading = true;
        var handle = prefabRef.InstantiateAsync(transform.position, Quaternion.identity);
        instance = await handle.Task;
        loading = false;

        Debug.Log(instance != null
            ? $"[AddressableTest] 로드 성공: {instance.name}"
            : "[AddressableTest] 로드 실패");
    }

    private void Despawn()
    {
        if (instance == null) return;

        prefabRef.ReleaseInstance(instance);   // Destroy + 메모리 해제를 한 번에
        instance = null;
        Debug.Log("[AddressableTest] 해제 완료");
    }
}