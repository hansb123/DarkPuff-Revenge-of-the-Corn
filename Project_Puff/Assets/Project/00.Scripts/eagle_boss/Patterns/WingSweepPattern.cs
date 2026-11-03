using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 일반 패턴: 날개 후려치기 (리듬 버전). 독수리가 화면 한쪽 끝에 내려앉아 박자에 맞춰 날개를 후려치고,
/// 후려칠 때마다 바닥을 따라 낮은 충격파가 반대편으로 달린다. 점프로 넘어서 피한다.
///
/// 충격파는 "악보(Notes)"대로 묶음으로 나간다. 한 글자 = 한 박자.
///   x = 후려침(충격파 한 발, 점프로 피함)   p = 후려침(금빛 패링 충격파, 쳐내야 함)   - = 쉼
///   예) "xxx-"        → 세 발, 쉬고
///       "xxxxx-"      → 다섯 발, 쉬고
///       "x-x-xx-"     → 한 발 _ 한 발 _ 두 발 _
///       "xxp-"        → 두 발 피하고, 금빛 한 발은 쳐낸다
/// p 바로 앞 박자에 눈이 번쩍여서 "다음은 쳐내야 한다"를 알려 준다. 쳐내서 패링에 성공하면 독수리가 휘청이며 포효하고, 그만큼 악보가 잠깐 멈춘다.
/// 한 번 내려앉으면 악보를 차례로 연주한다. 난이도가 오르면 박자가 빨라지고, 더 어려운 악보가 열린다.
/// 에셋 만들기: Project 창 우클릭 → Create → Darkpuff → Patterns → Wing Sweep
/// </summary>
[CreateAssetMenu(menuName = "Darkpuff/Patterns/Wing Sweep", fileName = "Pattern_WingSweep")]
public class WingSweepPattern : AttackPattern
{
    [System.Serializable]
    public class Phrase
    {
        [Tooltip("x = 한 발, - = 쉼 (한 글자 = 한 박자). 공백은 무시됨")]
        public string notes = "xxx-";
        [Tooltip("난이도가 이 값 이상일 때부터 연주 목록에 들어감 (0 = 처음부터)")]
        [Range(0f, 1f)] public float minDifficulty = 0f;
    }

    [SerializeField] private GroundShockwave wavePrefab;
    [Tooltip("p 음표에 쓰는 패링 충격파 프리팹 (Layer 를 Enemy 로). 악보에 p 가 없으면 비워도 됨")]
    [SerializeField] private GroundShockwave parryWavePrefab;

    [Header("악보 (묶음)")]
    [SerializeField]
    private List<Phrase> phrases = new List<Phrase>
    {
        new Phrase { notes = "xxx-", minDifficulty = 0f },
        new Phrase { notes = "xxxxx-", minDifficulty = 0.3f },
        new Phrase { notes = "x-x-xx-", minDifficulty = 0.5f },
        new Phrase { notes = "xxp-", minDifficulty = 0.4f },
        new Phrase { notes = "x-xxp-xp-", minDifficulty = 0.7f },
    };
    [Tooltip("한 번 내려앉아 연주하는 악보 수 (쉬움 → 어려움)")]
    [SerializeField] private int phrasesEasy = 1;
    [SerializeField] private int phrasesHard = 3;
    [Tooltip("악보를 무작위 순서로 연주")]
    [SerializeField] private bool shuffle = false;
    [Tooltip("악보와 악보 사이 추가로 쉬는 박자 수")]
    [SerializeField] private float phraseRestBeats = 1f;

    [Header("난이도별 수치  (왼쪽 = 쉬움 / 오른쪽 = 가장 어려움)")]
    [Tooltip("내려앉는 횟수 (둘째부터는 반대편으로 옮겨 감)")]
    [SerializeField] private int countEasy = 1;
    [SerializeField] private int countHard = 2;
    [Tooltip("한 박자 시간(초). 짧을수록 빠름. 점프 한 번이 약 0.6초")]
    [SerializeField] private float beatEasy = 0.75f;
    [SerializeField] private float beatHard = 0.5f;
    [Tooltip("처음 날개를 젖히는 예고 시간(초)")]
    [SerializeField] private float windupEasy = 0.9f;
    [SerializeField] private float windupHard = 0.55f;
    [Tooltip("충격파 속도 (유닛/초)")]
    [SerializeField] private float speedEasy = 7f;
    [SerializeField] private float speedHard = 11f;
    [Tooltip("충격파 높이. 이 높이보다 높이 뛰면 피함. 낮을수록 쉬움")]
    [SerializeField] private float heightEasy = 0.9f;
    [SerializeField] private float heightHard = 1.2f;

    [Header("패링 음표 (p)")]
    [Tooltip("패링 충격파 속도 = 일반 충격파 속도 × 이 값 (쳐낼 시간을 주려고 조금 느리게)")]
    [SerializeField] private float parrySpeedScale = 0.85f;
    [Tooltip("패링 당했을 때 밀려나는 거리 / 포효하며 멈추는 시간(초)")]
    [SerializeField] private float parryRecoil = 2.0f;
    [SerializeField] private float parryStun = 0.8f;

    [Header("충격파 모양")]
    [Tooltip("충격파 그림의 폭(유닛). 판정 폭은 이 값 기준")]
    [SerializeField] private float visualWidth = 3.2f;
    [Tooltip("후려친 위치에서 얼마나 떨어져서 시작할지")]
    [SerializeField] private float startOffset = 1.2f;

    [Header("독수리 움직임")]
    [SerializeField] private float edgeInset = 1.5f;
    [SerializeField] private float approachTime = 0.7f;
    [Tooltip("내려앉은 뒤 숨 고르는 시간")]
    [SerializeField] private float settle = 0.35f;
    [Tooltip("마지막 후려친 뒤 머무는 시간")]
    [SerializeField] private float hold = 0.45f;
    [SerializeField] private float returnTime = 0.6f;
    [Tooltip("착지 높이 미세조정. 발이 땅에 안 닿거나 박히면 조절")]
    [SerializeField] private float landOffset = 0f;
    [Tooltip("내려앉기와 내려앉기 사이 쉬는 시간(초)")]
    [SerializeField] private float gap = 0.3f;

    private static bool NextIsParry(string notes, int i) => i + 1 < notes.Length && (notes[i + 1] == 'p' || notes[i + 1] == 'P');

    public override IEnumerator Run(PatternContext ctx)
    {
        var motion = ctx.Boss != null ? ctx.Boss.GetComponent<EagleMotion>() : null;
        if (motion == null || wavePrefab == null)
        {
            Debug.LogWarning("[WingSweepPattern] 독수리에 EagleMotion 과 Wave Prefab 이 필요합니다.", this);
            yield break;
        }

        // 지금 난이도에서 열려 있는 악보
        var open = new List<string>();
        foreach (var ph in phrases)
        {
            if (ph == null || string.IsNullOrWhiteSpace(ph.notes) || ph.minDifficulty > ctx.Difficulty) continue;
            open.Add(ph.notes.Replace(" ", ""));
        }
        if (open.Count == 0) open.Add("xxx-");

        var pool = ctx.GetPool(wavePrefab, (w, pl) => w.SetReleaser(x => pl.Release(x)));
        PrefabPool<GroundShockwave> parryPool = parryWavePrefab != null
            ? ctx.GetPool(parryWavePrefab, (w, pl) => w.SetReleaser(x => pl.Release(x))) : null;
        int count = ctx.Lerp(countEasy, countHard);
        int phraseCount = Mathf.Max(1, ctx.Lerp(phrasesEasy, phrasesHard));
        float beat = ctx.Lerp(beatEasy, beatHard);
        float speed = ctx.Lerp(speedEasy, speedHard);
        float height = ctx.Lerp(heightEasy, heightHard);

        // 첫 방향: 플레이어에서 먼 쪽 끝에 내려앉아 플레이어 쪽으로 후려침
        float mid = (ctx.LeftX + ctx.RightX) * 0.5f;
        float px = ctx.Player != null ? ctx.Player.position.x : mid;
        float dirX = px < mid ? -1f : 1f;       // 플레이어가 왼쪽이면 오른쪽 끝에서 왼쪽으로
        int cursor = 0;

        for (int i = 0; i < count; i++)
        {
            var s = new EagleMotion.SweepSettings
            {
                edgeInset = edgeInset,
                approachTime = approachTime,
                settle = settle,
                windup = ctx.Lerp(windupEasy, windupHard),
                hold = hold,
                returnTime = returnTime,
                landOffset = landOffset,
            };

            float d = dirX;
            bool staggerPending = false;
            int parryAlive = 0;

            // 첫 음표가 p 이면 날개를 젖히는 동안 눈 번쩍
            bool firstIsParry = false;
            if (open.Count > 0)
            {
                string first = shuffle ? null : open[cursor % open.Count];
                firstIsParry = first != null && first.Length > 0 && (first[0] == 'p' || first[0] == 'P');
            }
            yield return motion.SweepLand(s, d, firstIsParry && parryPool != null);

            for (int p = 0; p < phraseCount; p++)
            {
                string notes = shuffle ? open[Random.Range(0, open.Count)] : open[cursor++ % open.Count];
                for (int ni = 0; ni < notes.Length; ni++)
                {
                    char c = notes[ni];
                    if (staggerPending)   // 앞에서 쳐낸 충격파에 맞아 휘청이며 포효 (악보가 잠깐 멈춤)
                    {
                        staggerPending = false;
                        yield return motion.SweepStagger(-d, parryRecoil, parryStun);
                        motion.SweepReady();
                    }

                    bool isParry = (c == 'p' || c == 'P') && parryPool != null;
                    bool isShot = c == 'x' || c == 'X' || isParry;
                    if (isShot)
                    {
                        motion.SweepStrike();
                        float endX = d > 0f ? ctx.RightX + visualWidth : ctx.LeftX - visualWidth;
                        float sx = motion.transform.position.x + d * startOffset;
                        if (isParry)
                        {
                            EaglePose.RaiseEyeFlash(false);
                            parryAlive++;
                            var pw = parryPool.Get();
                            pw.LaunchParry(ctx.Runner, ctx.Boss, sx, d, ctx.GroundY, speed * parrySpeedScale, height, visualWidth, endX,
                                           r => { parryAlive--; if (r == GroundShockwave.Result.Parried) staggerPending = true; });
                        }
                        else
                        {
                            var w = pool.Get();
                            w.Launch(ctx.Runner, sx, d, ctx.GroundY, speed, height, visualWidth, endX);
                        }
                        yield return new WaitForSeconds(beat * 0.4f);   // 후려친 직후 잠깐
                        motion.SweepReady();                             // 날개 다시 젖힘 = 다음 박자 준비
                        if (NextIsParry(notes, ni) && parryPool != null) EaglePose.RaiseEyeFlash(true);   // 다음은 패링: 미리 눈 번쩍
                        yield return new WaitForSeconds(beat * 0.6f);
                    }
                    else   // '-' 쉼
                    {
                        motion.SweepReady();
                        if (NextIsParry(notes, ni) && parryPool != null) EaglePose.RaiseEyeFlash(true);
                        yield return new WaitForSeconds(beat);
                    }
                }
                if (p < phraseCount - 1 && phraseRestBeats > 0f) yield return new WaitForSeconds(beat * phraseRestBeats);
            }

            // 쳐낸 충격파 결과가 아직 안 끝났으면 기다렸다가 반응
            while (parryAlive > 0) yield return null;
            if (staggerPending) { staggerPending = false; yield return motion.SweepStagger(-d, parryRecoil, parryStun); }
            EaglePose.RaiseEyeFlash(false);

            yield return motion.SweepLeave(s);

            dirX = -dirX;                       // 다음엔 반대편에서
            if (i < count - 1) yield return new WaitForSeconds(gap);
        }

        yield return new WaitForSeconds(0.2f);
    }
}