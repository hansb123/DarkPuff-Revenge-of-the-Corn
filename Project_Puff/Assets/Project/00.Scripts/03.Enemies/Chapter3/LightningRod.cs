using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 피뢰침(deco_rod). 이 근처에서 낙뢰가 조준되면 번개가 플레이어 대신 이쪽으로 떨어진다.
/// "피뢰침 옆에 서 있으면 안전" 용도. 번개구름/낙뢰 함정이 함께 사용.
/// 오브젝트의 발밑(Pivot)이 바닥에 닿게 두면 번개가 바닥 위치로 떨어진다.
/// </summary>
public class LightningRod : MonoBehaviour
{
    private static readonly List<LightningRod> all = new List<LightningRod>();

    [Tooltip("번개를 맞았을 때 (반짝임, 소리 등 연결)")]
    public UnityEvent OnStruck;

    private void OnEnable() => all.Add(this);
    private void OnDisable() => all.Remove(this);

    /// <summary>x 근처 range 안에서 가장 가까운 피뢰침. 없으면 null</summary>
    public static LightningRod FindNear(float x, float range)
    {
        LightningRod best = null;
        float bestD = range;
        foreach (var r in all)
        {
            float d = Mathf.Abs(r.transform.position.x - x);
            if (d <= bestD) { bestD = d; best = r; }
        }
        return best;
    }

    public void Struck() => OnStruck?.Invoke();
}
