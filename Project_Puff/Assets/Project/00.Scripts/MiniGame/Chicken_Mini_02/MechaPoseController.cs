using UnityEngine;

/// <summary>
/// 메카 닭의 몸짓. 평소엔 숨 쉬듯 살짝 들썩이고, 레이저가 충전되면 눈이 달아오른 포즈(charge), 발사 중에는 눈이 하얗게 타는 포즈(fire)로 바뀐다.
/// (깃털 비 / 전기 바닥 패턴을 만들 때 날개 펼침 포즈 등을 여기에 이어서 추가한다)
/// 닭 SpriteRenderer의 피벗은 Bottom Center 권장. 연출 스크립트가 직접 움직일 때는 Freeze(pose) / Unfreeze() 로 멈추고 푼다.
/// </summary>
public class MechaPoseController : MonoBehaviour
{
    [SerializeField] private SpriteRenderer body;

    [Header("포즈 (mecha_*.png)")]
    [SerializeField] private Sprite idle;           // mecha_idle
    [SerializeField] private Sprite laserCharge;    // mecha_laser_charge
    [SerializeField] private Sprite laserFire;      // mecha_laser_fire
    [SerializeField] private Sprite wingSpread;     // mecha_wing_spread (깃털 비)

    [Header("숨쉬기")]
    [SerializeField] private float breathSpeed = 2.4f;
    [SerializeField] private float breathAmount = 0.01f;

    [Header("발사 중")]
    [Tooltip("발사 중 몸이 부들부들 떨리는 세기")]
    [SerializeField] private float fireShake = 0.04f;

    private int charging, firing;
    private bool spread;
    private Vector3 basePos, baseScale;

    private void Awake()
    {
        basePos = transform.position;
        baseScale = transform.localScale;
        if (body == null) body = GetComponentInChildren<SpriteRenderer>();
        if (body != null && idle != null) body.sprite = idle;
    }

    private void OnEnable() { LaserBeam.PhaseChanged += OnLaser; FeatherRainPattern.VolleyChanged += OnVolley; }
    private void OnDisable() { LaserBeam.PhaseChanged -= OnLaser; FeatherRainPattern.VolleyChanged -= OnVolley; }

    private void OnVolley(bool on) { spread = on; Refresh(); }

    private void OnLaser(LaserBeam.Phase phase, int eye)
    {
        switch (phase)
        {
            case LaserBeam.Phase.Charge: charging++; break;
            case LaserBeam.Phase.Lock: break;
            case LaserBeam.Phase.Fire: charging = Mathf.Max(0, charging - 1); firing++; break;
            case LaserBeam.Phase.End:
                // 발사까지 간 빔이면 firing, 도중에 취소된 빔이면 charging 에서 하나 뺀다
                if (firing > 0) firing--; else charging = Mathf.Max(0, charging - 1);
                break;
        }
        Refresh();
    }

    private void Refresh()
    {
        if (body == null) return;
        Sprite s = spread && wingSpread != null ? wingSpread : firing > 0 ? laserFire : charging > 0 ? laserCharge : idle;
        if (s != null) body.sprite = s;
    }

    private void Update()
    {
        float breath = 1f + Mathf.Sin(Time.time * breathSpeed) * breathAmount;
        float shake = firing > 0 ? Mathf.Sin(Time.time * 80f) * fireShake : 0f;
        transform.position = new Vector3(basePos.x + shake, basePos.y, basePos.z);
        transform.localScale = new Vector3(baseScale.x, baseScale.y * breath, baseScale.z);
    }

    /// <summary>서 있는 기준 위치를 다시 잡는다 (등장 연출 뒤 호출하면 안전)</summary>
    public void RebaseHere()
    {
        basePos = transform.position;
        baseScale = transform.localScale;
    }

    /// <summary>움직임을 멈추고 특정 포즈로 고정 (연출용)</summary>
    public void Freeze(Sprite pose = null)
    {
        charging = firing = 0; spread = false;
        transform.position = basePos;
        transform.localScale = baseScale;
        if (body != null) body.sprite = pose != null ? pose : idle;
        enabled = false;
    }

    public void Unfreeze()
    {
        charging = firing = 0;
        transform.position = basePos;
        transform.localScale = baseScale;
        if (body != null && idle != null) body.sprite = idle;
        enabled = true;
    }
}
