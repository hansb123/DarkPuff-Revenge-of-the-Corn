using UnityEngine;

/// <summary>
/// 화면 비율을 16:9로 고정한다. 모니터가 16:10, 21:9 등이어도 남는 부분은 검은 띠(레터박스)로 채워서
/// 어떤 노트북/모니터에서도 같은 화면 구성과 같은 시야를 보장한다.
///
/// 사용법: 메인 카메라에 붙인다 (씬마다 1개).
///  - 검은 띠는 자동으로 만든 배경 카메라가 그린다.
///  - HUD/UI가 16:9 영역 안에 들어오게 하려면 Canvas를 Screen Space - Camera 로 두고 Render Camera에 이 카메라를 연결.
///    (Screen Space - Overlay 캔버스는 창 전체를 쓰므로 띠 위까지 늘어난다)
/// </summary>
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class AspectRatioLock : MonoBehaviour
{
    [SerializeField] private float targetWidth = 16f;
    [SerializeField] private float targetHeight = 9f;
    [Tooltip("검은 띠 색")]
    [SerializeField] private Color barColor = Color.black;

    private Camera cam;
    private Camera bgCam;
    private int lastW, lastH;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        BuildBackgroundCamera();
        Apply();
    }

    private void Update()
    {
        if (Screen.width != lastW || Screen.height != lastH) Apply();
    }

    private void BuildBackgroundCamera()
    {
        var go = new GameObject("LetterboxBackground");
        go.transform.SetParent(transform, false);
        bgCam = go.AddComponent<Camera>();
        bgCam.clearFlags = CameraClearFlags.SolidColor;
        bgCam.backgroundColor = barColor;
        bgCam.cullingMask = 0;
        bgCam.depth = cam.depth - 10f;
        bgCam.allowHDR = false; bgCam.allowMSAA = false;
        var l = go.GetComponent<AudioListener>(); if (l != null) Destroy(l);
    }

    private void Apply()
    {
        lastW = Screen.width; lastH = Screen.height;
        float target = targetWidth / targetHeight;
        float window = lastW / (float)Mathf.Max(1, lastH);
        Rect r;

        if (window > target)         // 창이 더 넓음 → 좌우 띠
        {
            float w = target / window;
            r = new Rect((1f - w) * 0.5f, 0f, w, 1f);
        }
        else                          // 창이 더 높음(16:10 등) → 위아래 띠
        {
            float h = window / target;
            r = new Rect(0f, (1f - h) * 0.5f, 1f, h);
        }
        cam.rect = r;
    }
}
