using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 최종 보스 독수리. 구조: [페이즈 = 값(상태)] + [Behavior Tree = 다음에 뭘 할지 고르는 층] + [AttackPattern = 실제 공격].
///  - 페이즈, HP, 패링 연속 성공 수는 이 컴포넌트가 값으로 들고 있다 (트리 안에 상태를 숨기지 않음).
///  - 페이즈가 바뀔 때마다 그 페이즈의 그룹 목록으로 트리를 새로 만든다.
///  - 트리 모양:  Selector
///        ├ Sequence [ 그로기 상태인가? , 그로기(딜타임) ]
///        └ Random (가중치, 같은 그룹 연속 금지, 뽑는 순간 조건)
///              └ 그룹 = Sequence [ 패턴, 패턴, ..., 휴식 ]
///  - 패링 결과는 ParryEvents 로 받는다. 연속 N회 성공 → 그로기. (피격하면 연속 횟수 초기화, 회피는 변화 없음)
///  - 보스는 IDamageable: 플레이어의 뻥 알갱이가 Enemy 레이어 콜라이더를 맞히면 TakeDamage 가 불린다.
/// 패턴 실행에 PatternRunner 를 "서비스"로만 쓴다 (풀, 위험물 추적/정리). Begin() 은 부르지 않는다.
/// </summary>
public class EagleBoss : MonoBehaviour, IDamageable
{
    public enum GroupGate { Any, PlayerNear, PlayerFar, PlayerAirborne, PlayerGrounded, PlayerLeftSide, PlayerRightSide }

    [Serializable]
    public class PatternGroup
    {
        public string name = "그룹";
        [Tooltip("순서대로 실행할 패턴들. 1개면 단독 패턴, 여러 개면 뭉치")]
        public List<AttackPattern> steps = new List<AttackPattern>();
        public float weight = 1f;
        [Tooltip("뽑는 순간 플레이어 위치 조건. 맞지 않으면 이번엔 후보에서 제외")]
        public GroupGate gate = GroupGate.Any;
    }

    [Serializable]
    public class Phase
    {
        public string name = "1페이즈";
        [Tooltip("패턴 수치에 쓰는 난이도 0~1. 높을수록 패턴이 빠르고 촘촘해짐 (3페이즈 '속도 증가'는 이 값을 올려서)")]
        [Range(0f, 1f)] public float difficulty = 0.2f;
        [Tooltip("이 페이즈에 들어갈 때 HP를 이 값으로 채움. 0 이면 HP 유지")]
        public int refillHp = 30;
        [Tooltip("HP 비율이 이 값 이하가 되면 다음 페이즈. 0 이면 HP가 0이 될 때 (마지막 페이즈는 사망)")]
        [Range(0f, 1f)] public float advanceAtHpRatio = 0f;
        [Tooltip("이전 페이즈의 그룹도 그대로 사용 (3페이즈 = 2페이즈 + 새 그룹)")]
        public bool inheritPreviousGroups = false;
        [Header("시간제 페이즈 (1페이즈: 맞지 않고 버티면 클리어, 보스는 데미지를 받지 않음)")]
        [Tooltip("켜면 HP 대신 시간으로 진행. Refill Hp / Advance At Hp Ratio 는 무시됨")]
        public bool timed = false;
        [Tooltip("이 시간(초)이 지나면 다음 페이즈로")]
        public float duration = 75f;
        [Tooltip("시간제 페이즈에서 끝날 때의 난이도. 시간이 갈수록 Difficulty 에서 이 값으로 올라감 (닭 보스의 분노 게이지와 같은 역할)")]
        [Range(0f, 1f)] public float difficultyAtEnd = 0.8f;
        [Header("체력이 적을 때 (3페이즈: HP 2 이하면 난이도 1)")]
        [Tooltip("HP 가 이 값 이하가 되면 난이도를 아래 값(이상)으로 올림. 0 이면 사용 안 함")]
        public int lowHpThreshold = 0;
        [Range(0f, 1f)] public float lowHpDifficulty = 1f;
        [Header("하늘 그로기 (3페이즈: 그로기 때 독수리가 바닥이 아니라 하늘로 올라감)")]
        [Tooltip("켜면 이 페이즈의 그로기는 추락이 아니라 하늘로 올라가 어지러워함. 플레이어는 올라가서 기본 공격으로 한 대 때려야 함 (뻥은 안 맞음)")]
        public bool skyGroggy = false;
        [Tooltip("하늘 그로기가 유지되는 시간(초). 이 안에 못 때리면 독수리가 복귀하고 패링 카운트는 이미 초기화돼 있음")]
        public float skyGroggyTime = 15f;
        [Tooltip("Sky Anchors 가 없을 때: 바닥 기준 높이 / HP 가 1 줄 때마다 더 올라가는 높이")]
        public float skyHeight = 14f;
        public float skyHeightStep = 3f;
        public List<PatternGroup> groups = new List<PatternGroup>();
    }

    [Header("페이즈")]
    [SerializeField] private List<Phase> phases = new List<Phase>();
    [Tooltip("페이즈가 바뀔 때 포효하는 연출 시간 (이동 중 무적, 공격 없음)")]
    [SerializeField] private float transitionTime = 2.0f;

    [Header("패링 / 그로기")]
    [Tooltip("연속으로 이만큼 패링에 성공하면 그로기")]
    [SerializeField] private int parriesToGroggy = 3;
    [Tooltip("패링당하면 화가 나서 한동안 난이도가 이만큼 올라감 (0이면 없음). 시간이 지나면 서서히 가라앉음")]
    [Range(0f, 0.5f)][SerializeField] private float angerBonus = 0.15f;
    [Tooltip("화가 가라앉는 데 걸리는 시간(초)")]
    [SerializeField] private float angerDuration = 8f;
    [SerializeField] private float groggyTime = 5f;
    [Tooltip("그로기 중 받는 데미지 배율 (딜타임)")]
    [SerializeField] private float groggyDamageMultiplier = 2f;
    [Tooltip("패링 공격에 맞으면 연속 성공 횟수를 0으로 되돌림")]
    [SerializeField] private bool resetStreakOnHit = true;
    [Tooltip("켜면 보스는 그로기일 때만 데미지를 받음 (2페이즈 이후). 끄면 항상 받음")]
    [SerializeField] private bool damageOnlyWhileGroggy = true;

    [Header("시간제 페이즈 게이지 (선택)")]
    [Tooltip("연결하면 시간제 페이즈 동안 이 게이지가 차오름 (닭 미니게임과 같은 HUD). 게이지가 끝날 때가 아니라 이 컴포넌트의 타이머가 진행을 결정함")]
    [SerializeField] private AngerGaugeHud timerGauge;

    [Header("흐름")]
    [SerializeField] private float startDelay = 1.0f;
    [Tooltip("그룹 안에서 패턴과 패턴 사이 쉬는 시간")]
    [SerializeField] private float stepGap = 0.35f;
    [Tooltip("그룹이 끝난 뒤 쉬는 시간 (마지막 패턴의 Rest After 에 곱해짐). 난이도 1이면 아래 값만큼 줄어듦")]
    [Range(0.1f, 1f)][SerializeField] private float restAtMax = 0.5f;
    [SerializeField] private bool restartOnRespawn = true;
    [SerializeField] private float restartDelay = 1.5f;

    [Header("무대 / 연결")]
    [SerializeField] private PatternRunner runner;
    [SerializeField] private Transform leftLimit;
    [SerializeField] private Transform rightLimit;
    [SerializeField] private Transform groundLine;
    [Tooltip("하늘 이동/급강하를 담당 (없으면 보스는 제자리)")]
    [SerializeField] private EagleMotion motion;
    [Tooltip("플레이어가 바닥에서 이 높이 이상이면 '공중'")]
    [SerializeField] private float airborneHeight = 1.5f;
    [Tooltip("이 거리 이내면 '가까움', 넘으면 '멂' (보스 기준 X 거리)")]
    [SerializeField] private float nearDistance = 5f;
    [Tooltip("켜면 어떤 그룹이 뽑혔는지 콘솔에 출력")]
    [SerializeField] private bool logPicks = false;

    // ── 상태 값 ──
    public int PhaseIndex { get; private set; } = -1;
    public int Hp { get; private set; }
    public int PhaseMaxHp { get; private set; }
    public int ParryStreak { get; private set; }
    public bool IsGroggy { get; private set; }
    public bool IsFighting { get; private set; }
    public bool IsTransitioning { get; private set; }
    /// <summary>시간제 페이즈 진행도 0~1 (시간제가 아니면 0)</summary>
    public float TimedProgress { get; private set; }
    public bool IsTimedPhase => PhaseIndex >= 0 && phases[PhaseIndex].timed;

    // ── 연출/HUD 연결용 이벤트 ──
    public event Action<int, int> HpChanged;          // (현재, 이 페이즈 최대)
    public event Action<int> PhaseEntered;            // 페이즈 인덱스 (포효 연출 시작)
    public event Action<int, int> ParryStreakChanged; // (연속 성공, 필요 횟수)
    [Header("테스트용")]
    [Tooltip("전투를 이 페이즈부터 시작 (0 = 1페이즈, 2 = 3페이즈). 테스트가 끝나면 반드시 0 으로 되돌릴 것")]
    [SerializeField] private int debugStartPhase = 0;

    [Header("하늘 그로기")]
    [Tooltip("하늘 그로기 때 독수리가 올라가 머무를 위치. HP 가 5, 4, 3... 일 때 순서대로 (Element 0 = 처음 그로기). 비우면 현재 X 에서 위로 올라감")]
    [SerializeField] private Transform[] skyAnchors;
    [Tooltip("1단계: 솟구쳐 올라가는 시간(초). 카메라가 독수리를 따라 올라감")]
    [SerializeField] private float skyRiseTime = 1.6f;
    [Tooltip("2단계: 제자리에서 날갯짓(바람)으로 플랫폼을 만드는 시간(초)")]
    [SerializeField] private float skySummonTime = 1.6f;
    [Tooltip("하늘 그로기가 끝난(맞았거나 시간 초과) 뒤 보스가 다음 공격을 시작하기까지 기다리는 시간(초). 플레이어가 발판에서 내려올 시간")]
    [SerializeField] private float skyAfterDelay = 3f;
    [Tooltip("3단계: 어지러운 포즈로 멈춰 있는 시간(초). 이 사이에 카메라가 내려가기 시작")]
    [SerializeField] private float skyDazeTime = 0.6f;
    [Tooltip("카메라가 독수리에서 플레이어로 내려오는 시간(초). 끝나면 플랫포밍 시작 (제한 시간도 이때부터)")]
    [SerializeField] private float skyPanDownTime = 1.4f;

    public enum SkyStage { None, Rising, Summoning, Dazed, Playable }
    /// <summary>하늘 그로기 연출 단계. Playable 일 때만 맞출 수 있고, 제한 시간도 이때부터 흐른다</summary>
    public SkyStage Stage { get; private set; } = SkyStage.None;
    public float SkySummonTime => skySummonTime;
    public float SkyPanDownTime => skyPanDownTime;
    public float SkyDazeTime => skyDazeTime;
    /// <summary>연출 단계가 바뀜 (단계, 몇 번째 그로기인지 0부터). 카메라/플랫폼/바람 이펙트가 구독</summary>
    public event Action<SkyStage, int> SkyStageChanged;
    private Coroutine skyRoutine;

    /// <summary>하늘 그로기 중 (3페이즈). 이때는 기본 공격만 맞고, 한 대 맞으면 끝남</summary>
    public bool IsSkyGroggy { get; private set; }
    /// <summary>하늘 그로기가 시작됨 (인자 = 몇 번째 그로기인지 0부터. 플랫폼/카메라가 구독)</summary>
    public event Action<int> SkyGroggyStarted;
    /// <summary>하늘 그로기가 끝남 (맞았거나 시간 초과, 페이즈 변경)</summary>
    public event Action SkyGroggyEnded;
    private int skyLevel;

    public event Action GroggyStarted;
    public event Action GroggyEnded;
    public event Action Defeated;

    private BtNode root;
    private Transform player;
    private Coroutine patternRoutine;
    private Coroutine transitionRoutine;
    private Coroutine restartRoutine;
    private float groggyEndAt;
    private float fightStartAt;
    private float phaseStartAt;

    private void Start()
    {
        var health = FindFirstObjectByType<PlayerHealth>();
        if (health != null) player = health.transform;
        else Debug.LogWarning("[EagleBoss] 플레이어(PlayerHealth)를 찾지 못했습니다.", this);

        if (motion == null) motion = GetComponent<EagleMotion>();
        if (runner == null) runner = FindFirstObjectByType<PatternRunner>();
        if (runner == null) Debug.LogWarning("[EagleBoss] PatternRunner 가 필요합니다 (풀/위험물 정리용, Begin 은 부르지 않음).", this);

        if (RespawnManager.Instance != null) RespawnManager.Instance.RespawnApplied += OnRespawn;
    }

    private void OnEnable()
    {
        ParryEvents.Succeeded += OnParrySucceeded;
        ParryEvents.Failed += OnParryFailed;
    }

    private void OnDisable()
    {
        ParryEvents.Succeeded -= OnParrySucceeded;
        ParryEvents.Failed -= OnParryFailed;
    }

    private void OnDestroy()
    {
        if (RespawnManager.Instance != null) RespawnManager.Instance.RespawnApplied -= OnRespawn;
    }

    // ───────────── 외부 호출 ─────────────

    /// <summary>전투 시작 (인트로 연출이 끝나면 호출)</summary>
    public void Begin()
    {
        if (IsFighting || phases.Count == 0) return;
        IsFighting = true;
        fightStartAt = Time.time + startDelay;
        PhaseIndex = -1;
        ParryStreak = 0;
        IsGroggy = false; ClearSky();
        EnterPhase(Mathf.Clamp(debugStartPhase, 0, phases.Count - 1), withTransition: false);
    }

    /// <summary>전투 중단 + 남은 공격 정리</summary>
    public void Stop()
    {
        IsFighting = false;
        IsTransitioning = false;
        IsGroggy = false; ClearSky();
        if (motion != null) motion.Recover();
        Interrupt();
        if (transitionRoutine != null) { StopCoroutine(transitionRoutine); transitionRoutine = null; }
        if (restartRoutine != null) { StopCoroutine(restartRoutine); restartRoutine = null; }
        root = null;
    }

    // ───────────── 매 프레임: 트리 한 번 Tick ─────────────

    private void Update()
    {
        if (!IsFighting || IsTransitioning || root == null) return;
        if (Time.time < fightStartAt) return;

        if (IsTimedPhase)
        {
            var ph = phases[PhaseIndex];
            TimedProgress = ph.duration > 0f ? Mathf.Clamp01((Time.time - phaseStartAt) / ph.duration) : 1f;
            if (TimedProgress >= 1f)
            {
                if (PhaseIndex >= phases.Count - 1) Die();
                else EnterPhase(PhaseIndex + 1, withTransition: true);
                return;
            }
        }
        root.Tick();
    }

    // ───────────── 트리 만들기 ─────────────

    private void BuildTree(int phaseIndex)
    {
        var groups = new List<PatternGroup>();
        CollectGroups(phaseIndex, groups);

        var random = new BtRandom($"{phases[phaseIndex].name} 패턴 선택") { Verbose = logPicks };
        foreach (var g in groups)
        {
            if (g.steps == null || g.steps.Count == 0) continue;
            random.Add(BuildGroup(g), g.weight, MakeGate(g.gate));
        }

        var groggy = new BtSequence("그로기")
            .Add(new BtCondition(() => IsGroggy, "그로기인가?"))
            .Add(new BtGroggy(this));

        root = new BtSelector("보스 루트").Add(groggy).Add(random);

        phaseStartAt = Mathf.Max(Time.time, fightStartAt);   // 시작 대기(startDelay) 동안은 시간을 세지 않음
        if (phases[phaseIndex].timed && timerGauge != null) timerGauge.Run(phases[phaseIndex].duration);
    }

    private void CollectGroups(int phaseIndex, List<PatternGroup> into)
    {
        if (phaseIndex > 0 && phases[phaseIndex].inheritPreviousGroups)
            CollectGroups(phaseIndex - 1, into);
        into.AddRange(phases[phaseIndex].groups);
    }

    private BtNode BuildGroup(PatternGroup g)
    {
        var seq = new BtSequence(g.name);
        for (int i = 0; i < g.steps.Count; i++)
        {
            seq.Add(new BtPatternAction(this, g.steps[i]));
            if (i < g.steps.Count - 1)
                seq.Add(new BtWait(() => stepGap, "간격"));
        }

        var last = g.steps[g.steps.Count - 1];
        float baseRest = last != null ? last.RestAfter : 1f;
        seq.Add(new BtWait(() => baseRest * Mathf.Lerp(1f, restAtMax, CurrentDifficulty), "휴식"));
        return seq;
    }

    private Func<bool> MakeGate(GroupGate gate)
    {
        switch (gate)
        {
            case GroupGate.PlayerNear: return () => PlayerDistanceX() <= nearDistance;
            case GroupGate.PlayerFar: return () => PlayerDistanceX() > nearDistance;
            case GroupGate.PlayerAirborne: return () => PlayerHeight() >= airborneHeight;
            case GroupGate.PlayerGrounded: return () => PlayerHeight() < airborneHeight;
            case GroupGate.PlayerLeftSide: return () => player != null && player.position.x < transform.position.x;
            case GroupGate.PlayerRightSide: return () => player != null && player.position.x >= transform.position.x;
            default: return null;
        }
    }

    private float PlayerDistanceX() => player != null ? Mathf.Abs(player.position.x - transform.position.x) : 0f;
    private float PlayerHeight() => player != null ? player.position.y - GroundY : 0f;
    private float GroundY => groundLine != null ? groundLine.position.y : 0f;
    private float angerUntil;
    private float CurrentDifficulty
    {
        get
        {
            if (PhaseIndex < 0) return 0f;
            var ph = phases[PhaseIndex];
            float d = ph.timed ? Mathf.Lerp(ph.difficulty, ph.difficultyAtEnd, TimedProgress) : ph.difficulty;
            if (ph.lowHpThreshold > 0 && Hp <= ph.lowHpThreshold) d = Mathf.Max(d, ph.lowHpDifficulty);   // 체력이 얼마 안 남으면 최고 난이도
            if (angerBonus > 0f && Time.time < angerUntil)   // 패링당한 직후엔 화가 나서 더 빠르고 거칠어짐
                d += angerBonus * Mathf.Clamp01((angerUntil - Time.time) / Mathf.Max(0.1f, angerDuration));
            return Mathf.Clamp01(d);
        }
    }

    // ───────────── 패턴 실행 (BtPatternAction 이 호출) ─────────────

    public void RunPattern(AttackPattern pattern, Action onDone)
    {
        if (patternRoutine != null) { StopCoroutine(patternRoutine); patternRoutine = null; }   // 위험물은 건드리지 않음 (앞 패턴의 잔여물 유지)
        patternRoutine = StartCoroutine(RunPatternRoutine(pattern, onDone));
    }

    private IEnumerator RunPatternRoutine(AttackPattern pattern, Action onDone)
    {
        yield return pattern.Run(MakeContext());
        patternRoutine = null;
        onDone?.Invoke();
    }

    /// <summary>실행 중인 패턴을 끊고 남은 공격 오브젝트를 치운다</summary>
    public void StopPattern()
    {
        if (patternRoutine != null) { StopCoroutine(patternRoutine); patternRoutine = null; }
        if (motion != null) motion.CancelAll();
        if (runner != null) runner.Stop();   // 위험물(알, 투사체 등) 일괄 Cancel. Begin 을 안 쓰므로 부작용 없음
    }

    private PatternContext MakeContext()
    {
        return new PatternContext
        {
            Runner = runner,
            Player = player,
            Boss = transform,
            LeftX = leftLimit != null ? leftLimit.position.x : -8f,
            RightX = rightLimit != null ? rightLimit.position.x : 8f,
            GroundY = GroundY,
            Difficulty = CurrentDifficulty,
        };
    }

    /// <summary>진행 중인 모든 것을 끊고 트리를 처음 상태로 (페이즈 전환 / 그로기 / 사망 때)</summary>
    private void Interrupt()
    {
        root?.Reset();
        StopPattern();
    }

    // ───────────── 페이즈 ─────────────

    private void EnterPhase(int index, bool withTransition)
    {
        PhaseIndex = index;
        var p = phases[index];
        TimedProgress = 0f;
        if (motion != null) motion.SetFrontLocked(p.timed);
        if (timerGauge != null) timerGauge.gameObject.SetActive(p.timed);   // 분노 게이지는 시간제 페이즈(1페이즈)에서만 보임
        if (p.timed)
        {
            // 시간제: HP 대신 시간. 전환 연출이 끝난 뒤부터 시간을 센다 (아래 BuildTree 시점에 phaseStartAt 갱신)
            if (timerGauge != null) timerGauge.ResetGauge();
        }
        else
        {
            if (p.refillHp > 0) { PhaseMaxHp = p.refillHp; Hp = p.refillHp; }
            HpChanged?.Invoke(Hp, PhaseMaxHp);
        }

        ParryStreak = 0;
        ParryStreakChanged?.Invoke(0, parriesToGroggy);
        IsGroggy = false; ClearSky();
        if (motion != null) motion.Recover();   // 그로기 중 페이즈가 넘어가도 하늘로 복귀

        Interrupt();
        PhaseEntered?.Invoke(index);

        if (withTransition)
        {
            if (transitionRoutine != null) StopCoroutine(transitionRoutine);
            transitionRoutine = StartCoroutine(TransitionRoutine(index));
        }
        else
        {
            BuildTree(index);
        }
    }

    private IEnumerator TransitionRoutine(int index)
    {
        IsTransitioning = true;
        root = null;
        yield return new WaitForSeconds(transitionTime);
        BuildTree(index);
        IsTransitioning = false;
        transitionRoutine = null;
    }

    private void CheckPhaseAdvance()
    {
        var p = phases[PhaseIndex];
        bool last = PhaseIndex >= phases.Count - 1;
        int threshold = Mathf.FloorToInt(PhaseMaxHp * p.advanceAtHpRatio);

        if (Hp > threshold) return;

        if (last) Die();
        else EnterPhase(PhaseIndex + 1, withTransition: true);
    }

    private void Die()
    {
        Stop();
        Defeated?.Invoke();
    }

    // ───────────── 피격 (플레이어의 뻥 알갱이) ─────────────

    public void TakeDamage(int amount, Vector2 hitFrom)
    {
        if (!IsFighting || IsTransitioning || amount <= 0) return;
        if (motion != null && motion.NotifyParried()) return;   // 패링 가능한 돌진 중에 몸을 맞힘 = 패링 성공
        if (damageOnlyWhileGroggy && !IsGroggy) return;   // 그로기 때만 때릴 수 있음
        if (IsTimedPhase) return;   // 시간제 페이즈(1페이즈)에서는 보스가 데미지를 받지 않는다

        if (IsSkyGroggy)
        {
            // 하늘 그로기: 기본 공격으로만, 딱 한 대(체력 1) 맞는다. 뻥(여러 알갱이)은 무시해서 한 번에 다 깎이는 걸 막음
            if (Stage != SkyStage.Playable) return;   // 연출 중에는 맞지 않음
            if (AttackSource.Current == AttackSource.Kind.Special) return;
            amount = 1;
            Hp = Mathf.Max(0, Hp - amount);
            EaglePose.Raise(EaglePose.Id.Hurt);
            HpChanged?.Invoke(Hp, PhaseMaxHp);
            if (Hp > Mathf.FloorToInt(PhaseMaxHp * phases[PhaseIndex].advanceAtHpRatio)) EndSkyGroggyByHit();
            else CheckPhaseAdvance();
            return;
        }

        if (IsGroggy) amount = Mathf.CeilToInt(amount * groggyDamageMultiplier);
        Hp = Mathf.Max(0, Hp - amount);
        EaglePose.Raise(EaglePose.Id.Hurt);
        HpChanged?.Invoke(Hp, PhaseMaxHp);
        CheckPhaseAdvance();
    }

    // ───────────── 패링 / 그로기 ─────────────

    private void OnParrySucceeded()
    {
        if (!IsFighting || IsTransitioning || IsGroggy) return;

        ParryStreak++;
        angerUntil = Time.time + angerDuration;
        ParryStreakChanged?.Invoke(ParryStreak, parriesToGroggy);
        if (ParryStreak >= parriesToGroggy) StartGroggy();
    }

    private void OnParryFailed()
    {
        if (!IsFighting || IsTransitioning || IsGroggy) return;
        if (!resetStreakOnHit || ParryStreak == 0) return;

        ParryStreak = 0;
        ParryStreakChanged?.Invoke(0, parriesToGroggy);
    }

    private void StartGroggy()
    {
        ParryStreak = 0;
        ParryStreakChanged?.Invoke(0, parriesToGroggy);

        Interrupt();                       // 진행 중이던 패턴을 즉시 끊는다
        IsGroggy = true;
        var gp = phases[PhaseIndex];
        if (gp.skyGroggy)
        {
            // 하늘 그로기: 추락하지 않고 하늘로 올라가 어지러워한다
            int level = Mathf.Clamp(PhaseMaxHp - Hp, 0, 99);   // 지금까지 맞은 횟수 = 몇 번째 플랫포밍인지
            skyLevel = level;
            Vector3 target;
            if (skyAnchors != null && skyAnchors.Length > 0 && skyAnchors[Mathf.Min(level, skyAnchors.Length - 1)] != null)
                target = skyAnchors[Mathf.Min(level, skyAnchors.Length - 1)].position;
            else
                target = new Vector3(transform.position.x, GroundY + gp.skyHeight + gp.skyHeightStep * level, transform.position.z);
            IsSkyGroggy = true;
            groggyEndAt = float.MaxValue;   // 연출이 끝나(Playable) 플랫포밍이 시작될 때부터 제한 시간이 흐른다
            if (skyRoutine != null) StopCoroutine(skyRoutine);
            skyRoutine = StartCoroutine(SkySequence(target, gp.skyGroggyTime, level));
            GroggyStarted?.Invoke();
            SkyGroggyStarted?.Invoke(level);
            return;
        }
        groggyEndAt = Time.time + groggyTime;
        if (motion != null) motion.Fall();       // 하늘에서 바닥으로 추락
        GroggyStarted?.Invoke();
    }

    /// <summary>BtGroggy 가 매 Tick 부름. 아직 그로기면 true</summary>
    public bool TickGroggy()
    {
        if (!IsGroggy) return false;   // 하늘 그로기에서 한 대 맞아 일찍 끝난 경우
        if (Time.time < groggyEndAt) return true;
        IsGroggy = false;
        if (IsSkyGroggy) fightStartAt = Time.time + skyAfterDelay;   // 시간 초과: 플레이어가 내려올 때까지 공격 보류
        ClearSky();
        if (motion != null) motion.Recover();    // 다시 하늘로 / 제자리로 복귀
        GroggyEnded?.Invoke();
        return false;
    }

    /// <summary>상승 → 바람(플랫폼 생성) → 어지러움 → (카메라 하강) → 플랫포밍 시작</summary>
    private System.Collections.IEnumerator SkySequence(Vector3 target, float playTime, int level)
    {
        SetSkyStage(SkyStage.Rising, level);
        if (motion != null) motion.RiseGroggy(target, skyRiseTime);
        yield return new WaitForSeconds(skyRiseTime);

        SetSkyStage(SkyStage.Summoning, level);
        if (motion != null) motion.SkySummon();
        yield return new WaitForSeconds(skySummonTime);

        SetSkyStage(SkyStage.Dazed, level);
        if (motion != null) motion.SkyDaze();
        yield return new WaitForSeconds(skyDazeTime + skyPanDownTime);

        groggyEndAt = Time.time + playTime;
        SetSkyStage(SkyStage.Playable, level);
        skyRoutine = null;
    }

    private void SetSkyStage(SkyStage st, int level)
    {
        Stage = st;
        SkyStageChanged?.Invoke(st, level);
    }

    private void ClearSky()
    {
        if (skyRoutine != null) { StopCoroutine(skyRoutine); skyRoutine = null; }
        if (!IsSkyGroggy) return;
        IsSkyGroggy = false;
        Stage = SkyStage.None;
        SkyGroggyEnded?.Invoke();
    }

    /// <summary>하늘 그로기에서 한 대 맞음 → 그로기를 바로 끝내고 독수리가 복귀한다</summary>
    private void EndSkyGroggyByHit()
    {
        IsGroggy = false;
        fightStartAt = Time.time + skyAfterDelay;   // 플레이어가 아래로 떨어질 시간을 준다
        ClearSky();
        if (motion != null) motion.Recover();
        GroggyEnded?.Invoke();
    }

    // ───────────── 플레이어가 죽어 처음부터 ─────────────

    private void OnRespawn()
    {
        if (!restartOnRespawn || !IsFighting) return;
        if (RespawnManager.Instance != null && RespawnManager.Instance.UseCheckpoints) return;

        Stop();
        restartRoutine = StartCoroutine(RestartAfterDelay());
    }

    private IEnumerator RestartAfterDelay()
    {
        yield return new WaitForSeconds(restartDelay);
        restartRoutine = null;
        Begin();
    }

    [ContextMenu("Debug/Begin")] private void DebugBegin() => Begin();
    [ContextMenu("Debug/Stop")] private void DebugStop() => Stop();
    [ContextMenu("Debug/Force Groggy")] private void DebugGroggy() { if (IsFighting) StartGroggy(); }
    [ContextMenu("Debug/Next Phase")] private void DebugNext() { if (IsFighting && PhaseIndex < phases.Count - 1) EnterPhase(PhaseIndex + 1, true); }
}