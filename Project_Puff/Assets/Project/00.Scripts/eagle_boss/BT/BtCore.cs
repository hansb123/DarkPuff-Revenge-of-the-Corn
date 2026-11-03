using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 가벼운 Behavior Tree 핵심. 노드는 매 Tick 마다 Success / Failure / Running 중 하나를 돌려준다.
/// 이 프로젝트의 규칙: 실행 중인 패턴은 매 프레임 다시 판단하지 않는다 (Running 이면 그 자식만 계속 Tick).
/// 중단(페이즈 전환, 그로기, 사망)은 EagleBoss 가 root.Reset() 으로 한 번에 처리한다.
/// </summary>
public enum BtStatus { Success, Failure, Running }

public abstract class BtNode
{
    public string Name;
    public abstract BtStatus Tick();
    /// <summary>진행 상태를 처음으로 되돌림. 실행 중이던 것은 정리(중단)한다</summary>
    public virtual void Reset() { }
}

/// <summary>자식을 순서대로 실행. 하나라도 실패하면 즉시 실패, 전부 성공하면 성공</summary>
public class BtSequence : BtNode
{
    private readonly List<BtNode> children = new List<BtNode>();
    private int index;

    public BtSequence(string name = "Sequence") { Name = name; }
    public BtSequence Add(BtNode n) { children.Add(n); return this; }

    public override BtStatus Tick()
    {
        while (index < children.Count)
        {
            var s = children[index].Tick();
            if (s == BtStatus.Running) return s;
            if (s == BtStatus.Failure) { Reset(); return BtStatus.Failure; }
            index++;
        }
        Reset();
        return BtStatus.Success;
    }

    public override void Reset()
    {
        index = 0;
        foreach (var c in children) c.Reset();
    }
}

/// <summary>자식을 순서대로 시도. 처음으로 성공/실행 중인 자식에서 멈춘다 (실행 중인 자식은 기억)</summary>
public class BtSelector : BtNode
{
    private readonly List<BtNode> children = new List<BtNode>();
    private int running = -1;

    public BtSelector(string name = "Selector") { Name = name; }
    public BtSelector Add(BtNode n) { children.Add(n); return this; }

    public override BtStatus Tick()
    {
        int start = running >= 0 ? running : 0;
        for (int i = start; i < children.Count; i++)
        {
            var s = children[i].Tick();
            if (s == BtStatus.Running) { running = i; return s; }
            if (s == BtStatus.Success) { running = -1; return s; }
        }
        running = -1;
        return BtStatus.Failure;
    }

    public override void Reset()
    {
        running = -1;
        foreach (var c in children) c.Reset();
    }
}

/// <summary>조건이 참이면 Success, 거짓이면 Failure</summary>
public class BtCondition : BtNode
{
    private readonly Func<bool> test;
    public BtCondition(Func<bool> test, string name = "Condition") { this.test = test; Name = name; }
    public override BtStatus Tick() => test() ? BtStatus.Success : BtStatus.Failure;
}

/// <summary>
/// 가중치 랜덤 선택. 뽑는 순간(= 이전 선택이 끝나고 새로 시작할 때)에만 고른다.
/// - gate: 뽑는 순간의 조건 (예: 플레이어가 멀리 있을 때만). 매 프레임 검사하지 않는다.
/// - 같은 자식이 연달아 나오지 않음 (후보가 둘 이상일 때)
/// 고른 자식이 끝나면 그 결과를 돌려준다.
/// </summary>
public class BtRandom : BtNode
{
    private class Item { public BtNode node; public float weight; public Func<bool> gate; }
    private readonly List<Item> items = new List<Item>();
    private Item picked, last;
    public bool Verbose;

    public BtRandom(string name = "Random") { Name = name; }

    public BtRandom Add(BtNode n, float weight = 1f, Func<bool> gate = null)
    {
        items.Add(new Item { node = n, weight = weight, gate = gate });
        return this;
    }

    public override BtStatus Tick()
    {
        if (picked == null)
        {
            picked = Pick();
            if (picked == null) return BtStatus.Failure;
            if (Verbose) Debug.Log($"[BT] {Name} → {picked.node.Name}");
        }

        var s = picked.node.Tick();
        if (s == BtStatus.Running) return s;

        picked.node.Reset();
        last = picked;
        picked = null;
        return s;
    }

    private Item Pick()
    {
        var cand = new List<Item>();
        foreach (var it in items)
            if (it.weight > 0f && (it.gate == null || it.gate())) cand.Add(it);
        if (cand.Count == 0) return null;
        if (cand.Count > 1) cand.RemoveAll(i => i == last);

        float total = 0f;
        foreach (var it in cand) total += it.weight;
        float r = UnityEngine.Random.value * total;
        foreach (var it in cand)
        {
            r -= it.weight;
            if (r <= 0f) return it;
        }
        return cand[cand.Count - 1];
    }

    public override void Reset()
    {
        if (picked != null) picked.node.Reset();
        picked = null;
        foreach (var it in items) it.node.Reset();
    }
}

/// <summary>일정 시간 기다림 (Running → Success). 시간 값은 시작할 때 한 번 읽는다</summary>
public class BtWait : BtNode
{
    private readonly Func<float> seconds;
    private float endAt = -1f;

    public BtWait(Func<float> seconds, string name = "Wait") { this.seconds = seconds; Name = name; }

    public override BtStatus Tick()
    {
        if (endAt < 0f) endAt = Time.time + Mathf.Max(0f, seconds());
        if (Time.time < endAt) return BtStatus.Running;
        endAt = -1f;
        return BtStatus.Success;
    }

    public override void Reset() { endAt = -1f; }
}

/// <summary>
/// 기존 AttackPattern(코루틴)을 한 번 실행하는 잎 노드. 끝날 때까지 Running.
/// 패링 결과는 여기서 다루지 않는다 — 패링 공격이 이벤트로 알리고 보스가 센다. 그래서 항상 Success 로 끝나 시퀀스가 이어진다.
/// </summary>
public class BtPatternAction : BtNode
{
    private readonly EagleBoss boss;
    private readonly AttackPattern pattern;
    private bool started, finished;

    public BtPatternAction(EagleBoss boss, AttackPattern pattern)
    {
        this.boss = boss;
        this.pattern = pattern;
        Name = pattern != null ? pattern.DisplayName : "(빈 패턴)";
    }

    public override BtStatus Tick()
    {
        if (pattern == null) return BtStatus.Success;
        if (!started)
        {
            started = true;
            finished = false;
            boss.RunPattern(pattern, () => finished = true);
        }
        if (!finished) return BtStatus.Running;
        started = false;
        return BtStatus.Success;
    }

    public override void Reset()
    {
        if (started && !finished) boss.StopPattern();
        started = false;
        finished = false;
    }
}

/// <summary>그로기: 정해진 시간 동안 Running, 끝나면 Success</summary>
public class BtGroggy : BtNode
{
    private readonly EagleBoss boss;
    public BtGroggy(EagleBoss boss) { this.boss = boss; Name = "Groggy"; }

    public override BtStatus Tick() => boss.TickGroggy() ? BtStatus.Running : BtStatus.Success;
}
