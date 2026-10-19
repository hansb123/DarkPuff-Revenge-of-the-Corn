using UnityEngine;

/// <summary>톱날 등 제자리에서 계속 회전시키는 스크립트. MovingObstacle과 같이 붙이세요.</summary>
public class Spinner : MonoBehaviour
{
    [SerializeField] private float degreesPerSecond = -360f;
    [SerializeField] private Transform target; // 비우면 자기 자신

    private void Update()
    {
        (target != null ? target : transform).Rotate(0f, 0f, degreesPerSecond * Time.deltaTime);
    }
}
