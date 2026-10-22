using UnityEngine;

/// <summary>
/// 메카 닭의 눈(레이저가 나가는 위치) 표시용. 메카 닭 오브젝트에 붙이고 왼쪽/오른쪽 눈 위치에 빈 자식 오브젝트를 만들어 연결한다.
/// 닭 스프라이트의 자식으로 두면 닭이 흔들릴 때 같이 움직인다.
/// </summary>
public class MechaEyes : MonoBehaviour
{
    [SerializeField] private Transform leftEye;
    [SerializeField] private Transform rightEye;

    public Transform Get(int index) => index == 0 ? leftEye : rightEye;
    public bool IsValid => leftEye != null && rightEye != null;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        if (leftEye != null) Gizmos.DrawWireSphere(leftEye.position, 0.15f);
        if (rightEye != null) Gizmos.DrawWireSphere(rightEye.position, 0.15f);
    }
}
