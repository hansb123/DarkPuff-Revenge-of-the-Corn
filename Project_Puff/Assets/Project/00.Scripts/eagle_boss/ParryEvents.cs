using System;

/// <summary>
/// 패링 공격이 결과를 알리는 통로. 패링 공격(ParryProjectile 등)이 쏘고, 보스가 구독해서 센다.
/// 공격과 보스가 서로를 몰라도 되게 하려는 것 (공격은 "나는 이렇게 끝났다"만 알림).
/// </summary>
public static class ParryEvents
{
    /// <summary>뻥 특수공격으로 막아냄</summary>
    public static event Action Succeeded;
    /// <summary>맞음 (체력 감소)</summary>
    public static event Action Failed;
    /// <summary>피함 (이득도 손해도 없음)</summary>
    public static event Action Dodged;

    public static void RaiseSucceeded() => Succeeded?.Invoke();
    public static void RaiseFailed() => Failed?.Invoke();
    public static void RaiseDodged() => Dodged?.Invoke();
}
