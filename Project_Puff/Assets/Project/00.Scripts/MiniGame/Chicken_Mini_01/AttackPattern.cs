using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 닭의 공격 패턴 한 가지를 담는 데이터 + 실행 코드 (ScriptableObject).
/// 새 패턴 = 이 클래스를 상속해 Run()만 만들면 된다. 수치(개수, 간격, 시간)는 에셋의 Inspector에서 조절.
/// 패턴 자체는 상태를 갖지 않는다 (씬 오브젝트/풀은 PatternContext를 통해 얻는다).
/// </summary>
public abstract class AttackPattern : ScriptableObject
{
    [SerializeField] private string displayName = "패턴";
    [Tooltip("이 패턴이 끝난 뒤 다음 패턴까지 쉬는 시간(초). 난이도가 높을수록 PatternRunner가 줄여 준다")]
    [SerializeField] private float restAfter = 1.4f;

    public string DisplayName => displayName;
    public float RestAfter => restAfter;

    /// <summary>패턴 본체. 끝날 때까지(마지막 공격이 맞거나 사라질 때까지) yield 하며 진행한다</summary>
    public abstract IEnumerator Run(PatternContext ctx);
}

/// <summary>패턴이 실행될 때 필요한 정보 묶음 (무대 범위, 플레이어, 현재 난이도, 풀)</summary>
public class PatternContext
{
    public PatternRunner Runner;
    public Transform Player;
    public float LeftX, RightX, GroundY;
    /// <summary>0(쉬움) ~ 1(가장 어려움). 분노 게이지 진행도</summary>
    public float Difficulty;

    public float Lerp(float easy, float hard) => Mathf.Lerp(easy, hard, Difficulty);
    public int Lerp(int easy, int hard) => Mathf.RoundToInt(Mathf.Lerp(easy, hard, Difficulty));

    public PrefabPool<T> GetPool<T>(T prefab, Action<T, PrefabPool<T>> onCreated = null) where T : Component
        => Runner.GetPool(prefab, onCreated);
}

/// <summary>화면에 남아 있는 공격 오브젝트(알 등). 플레이어가 죽거나 미니게임이 끝날 때 한꺼번에 치우기 위해 Runner가 추적한다</summary>
public interface IPatternHazard
{
    void Cancel();
}
