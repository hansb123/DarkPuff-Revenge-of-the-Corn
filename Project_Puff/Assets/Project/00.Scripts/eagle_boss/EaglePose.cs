using System;

/// <summary>
/// 독수리 포즈 신호. 패턴이 Raise 하면 EaglePoseController 가 정면샷 스프라이트를 바꾼다.
/// (패턴이 보스 오브젝트를 몰라도 되도록 정적 이벤트로 연결)
/// </summary>
public static class EaglePose
{
    public enum Id { Idle, Windup, Strike, Roar, Groggy, Dive, Hurt, Land, SkyGroggy }

    public static event Action<Id> Changed;

    /// <summary>true 인 동안 독수리는 플레이어 쪽으로 몸을 돌리지 않는다 (돌진 중 지나친 뒤에 뒤집히지 않게)</summary>
    public static bool FacingLocked;

    /// <summary>날개짓 속도 배율 (1 = 평소). 하늘 그로기 연출에서 힘차게 퍼덕일 때 키운다</summary>
    public static float FlapSpeed = 1f;

    /// <summary>눈이 번쩍이는 연출 켜기/끄기 (패링 가능한 공격의 신호)</summary>
    public static event Action<bool> EyeFlash;
    public static void RaiseEyeFlash(bool on) => EyeFlash?.Invoke(on);
    public static void Raise(Id id) => Changed?.Invoke(id);
}