using UnityEngine;

/// <summary>
/// 카메라 흔들림 (추가 오프셋 방식). 카메라 추적 스크립트와 충돌하지 않도록
/// Update 에서 이전 오프셋을 빼고, LateUpdate(가장 마지막)에서 새 오프셋을 더한다.
/// 사용: EagleCamShake.Add(0.4f, 0.5f);  — 카메라에 자동으로 붙는다.
/// </summary>
[DefaultExecutionOrder(10000)]
public class EagleCamShake : MonoBehaviour
{
    private static EagleCamShake inst;
    private Vector3 applied;
    private float amp, dur, t;
    private bool ignoreTimeScale = true;

    public static void Add(float amplitude, float duration)
    {
        var cam = Camera.main;
        if (cam == null) return;
        if (inst == null || inst.gameObject != cam.gameObject)
        {
            inst = cam.GetComponent<EagleCamShake>();
            if (inst == null) inst = cam.gameObject.AddComponent<EagleCamShake>();
        }
        // 더 강한 흔들림이 우선
        float remain = inst.dur > 0f ? inst.amp * Mathf.Clamp01(1f - inst.t / inst.dur) : 0f;
        if (amplitude >= remain) { inst.amp = amplitude; inst.dur = Mathf.Max(0.01f, duration); inst.t = 0f; }
    }

    private void Update()
    {
        transform.position -= applied;
        applied = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (dur <= 0f) return;
        t += ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
        float k = Mathf.Clamp01(1f - t / dur);
        if (t >= dur) { dur = 0f; return; }
        Vector2 r = Random.insideUnitCircle * (amp * k * k);
        applied = new Vector3(r.x, r.y, 0f);
        transform.position += applied;
    }

    private void OnDisable() { transform.position -= applied; applied = Vector3.zero; }
}