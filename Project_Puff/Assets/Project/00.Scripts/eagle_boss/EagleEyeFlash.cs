using UnityEngine;

/// <summary>
/// 독수리 눈이 번쩍이는 연출 (패링 가능한 돌진의 신호). EaglePose.EyeFlash 신호로 켜지고 꺼진다.
/// 보스 오브젝트의 자식에 SpriteRenderer 를 만들고 이 컴포넌트를 붙인다.
/// 스프라이트는 eagle_stage/fx/fx_twinkle_1~6 을 Frames 에 넣으면 반짝반짝 재생된다 (비우면 노란 사각형이 깜빡임).
/// 눈 위치: 보스 자식에 빈 오브젝트(EyePoint)를 만들어 Hierarchy 에서 눈 위로 끌어다 놓고, 이 컴포넌트의 Eye Anchor 칸에 연결한다.
///   - 그림이 왼쪽을 향한 상태(기본)에서 눈 위에 놓으면 된다. 오른쪽을 볼 때는 자동으로 좌우 대칭 위치가 된다.
///   - Eye Anchor 를 비우면 아래 Eye Offset 숫자를 쓴다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class EagleEyeFlash : MonoBehaviour
{
    [SerializeField] private SpriteRenderer bossRenderer;
    [Tooltip("연결하면 포즈마다 눈 위치를 자동으로 따라감 (Eye Anchor 보다 우선). 보스 오브젝트의 EaglePoseController")]
    [SerializeField] private EaglePoseController poseController;
    [Tooltip("fx_twinkle_1~6")]
    [SerializeField] private Sprite[] frames;
    [Tooltip("눈 위치를 표시한 빈 오브젝트 (보스의 자식). 왼쪽을 향한 그림의 눈 위에 놓는다")]
    [SerializeField] private Transform eyeAnchor;
    [Tooltip("(Eye Anchor 가 없을 때) 보스 중심에서 눈까지의 위치. X: 바라보는 방향이 +")]
    [SerializeField] private Vector2 eyeOffset = new Vector2(1.1f, 0.6f);
    [SerializeField] private float size = 1.2f;
    [SerializeField] private float fps = 16f;
    [SerializeField] private float pulse = 0.25f;
    [SerializeField] private Color color = new Color(1f, 0.95f, 0.5f, 1f);

    private SpriteRenderer sr;
    private bool on;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (bossRenderer == null && transform.parent != null) bossRenderer = transform.parent.GetComponent<SpriteRenderer>();
        sr.enabled = false;
    }

    private void OnEnable() => EaglePose.EyeFlash += OnFlash;
    private void OnDisable() { EaglePose.EyeFlash -= OnFlash; if (sr != null) sr.enabled = false; }

    private void OnFlash(bool value)
    {
        on = value;
        sr.enabled = on;
    }

    private void LateUpdate()
    {
        if (!on) return;

        bool faceRight = bossRenderer != null && bossRenderer.flipX;   // 그림은 왼쪽을 향하므로 flipX 면 오른쪽
        Transform p = transform.parent != null ? transform.parent : transform;
        if (poseController != null && poseController.HasEye)
        {
            transform.position = p.TransformPoint(new Vector3(poseController.EyeLocal.x, poseController.EyeLocal.y, 0f));
        }
        else if (eyeAnchor != null)
        {
            Vector3 local = p.InverseTransformPoint(eyeAnchor.position);   // 보스가 회전해도 맞도록 보스 기준 좌표로
            if (faceRight) local.x = -local.x;                               // 오른쪽을 볼 때는 좌우 대칭
            transform.position = p.TransformPoint(local);
        }
        else
        {
            float x = (faceRight ? eyeOffset.x : -eyeOffset.x);
            transform.position = p.position + new Vector3(x, eyeOffset.y, 0f);
        }

        if (frames != null && frames.Length > 0)
            sr.sprite = frames[Mathf.FloorToInt(Time.time * fps) % frames.Length];
        sr.color = color;
        sr.sortingOrder = bossRenderer != null ? bossRenderer.sortingOrder + 1 : 10;
        float k = size * (1f + Mathf.Sin(Time.time * 30f) * pulse);
        transform.localScale = new Vector3(k, k, 1f);
    }
}