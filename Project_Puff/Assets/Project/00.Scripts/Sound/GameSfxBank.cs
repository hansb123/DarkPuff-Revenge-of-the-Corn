using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인게임 효과음 목록. 이름이 곧 파일 이름 규칙이다:
///   PlayerSwing  →  player_swing.wav  (또는 player_swing_1, player_swing_2 ... 처럼 번호가 붙은 여러 개 = 랜덤 재생)
/// </summary>
public enum GameSfxId
{
    // 플레이어
    PlayerJump, PlayerLand, PlayerStep, PlayerSwing, PlayerHitEnemy,
    PlayerHurt, PlayerDead, PlayerRespawn,
    // 특수공격 뻥이요
    SpecialShot, SpecialRocketJump, PelletHit, PelletPop,
    // 오브젝트
    ItemPpeongGet, CheckpointOn, DoorOpen,
    // 적
    EnemyAlert, EnemyMeleeAttack, EnemyRangedShoot, EnemyProjectileHit, EnemyHurt, EnemyDead,
    // 장애물 / 함정
    TrapWarn, TrapFallHit, TrapSpikePop, TrapSurprise,
    // 닭 미니게임
    ChickenCluck, ChickenBawk, ChickenRoar, ChickenWingFlap, ChickenPeckWarn, ChickenPeckHit,
    EggFall, EggSplat, ChickenWindGust, AngerTick, AngerFull,
    // 닭 미니게임 클리어 연출
    ChickenDizzy, ChickenCollapse, CraneMotor, CraneClank, SteamHiss, MechaStomp, TeaserSting,
    // 회복템
    HealDrop, HealLand, HealGet,
    // 활공
    FeatherGet, GlideStart,
    // 메카 닭 미니게임
    MechaIntro, MechaServo, MechaWingSpread, MechaAlarm,
    LaserCharge, LaserLock, LaserFire, LaserBeamLoop, LaserHit, LaserEnd,
    MechaFeatherLaunch, MechaFeatherFall, MechaFeatherStick,
    FloorWarn, FloorShock, FloorShockEnd,
    HardGet, HardEnd,
    // 메카 닭 클리어 연출
    MechaOverload, SparkZap, MechaPowerDown, MechaCoreOpen, HardEggPop, HardEggLand, ShadowPass,
}

/// <summary>
/// 인게임 효과음 묶음. Resources 폴더에 "GameSfxBank" 이름으로 한 개만 두면 GameSfx가 알아서 불러온다.
/// 에셋을 만든 뒤 우클릭(또는 ⋮ 메뉴) → "Auto Fill From Folder..." 로 sfx_game 폴더를 고르면 클립이 한 번에 채워진다.
/// </summary>
[CreateAssetMenu(menuName = "Darkpuff/Game Sfx Bank", fileName = "GameSfxBank")]
public class GameSfxBank : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public GameSfxId id;
        [Tooltip("여러 개면 재생할 때마다 랜덤으로 하나를 고름")]
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("재생할 때마다 음높이를 ± 만큼 흔듦. 자주 나오는 소리일수록 크게")]
        [Range(0f, 0.2f)] public float pitchJitter = 0.03f;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    [field: Header("믹스")]
    [field: SerializeField, Range(0f, 1f)] public float MasterVolume { get; private set; } = 0.8f;
    [field: Tooltip("같은 소리가 이 시간(초) 안에 또 나오면 무시. 적 여러 마리가 동시에 소리 내도 겹쳐 뭉개지지 않게")]
    [field: SerializeField, Range(0f, 0.2f)] public float MinInterval { get; private set; } = 0.05f;

    [field: Header("거리 감쇠 (PlayAt용, 월드 단위)")]
    [field: Tooltip("이 거리 안에서는 원래 크기")]
    [field: SerializeField] public float HearNear { get; private set; } = 6f;
    [field: Tooltip("이 거리 밖에서는 안 들림")]
    [field: SerializeField] public float HearFar { get; private set; } = 18f;

    private Dictionary<GameSfxId, Entry> map;

    public bool TryGet(GameSfxId id, out Entry entry)
    {
        if (map == null)
        {
            map = new Dictionary<GameSfxId, Entry>();
            foreach (var e in entries)
                if (e != null && e.clips != null && e.clips.Length > 0) map[e.id] = e;
        }
        return map.TryGetValue(id, out entry);
    }

    private void OnValidate() => map = null;   // 인스펙터에서 바꾸면 다시 만들기

#if UNITY_EDITOR
    [ContextMenu("Auto Fill From Folder...")]
    private void AutoFill()
    {
        string abs = UnityEditor.EditorUtility.OpenFolderPanel("효과음(wav)이 들어있는 폴더 선택", Application.dataPath, "");
        if (string.IsNullOrEmpty(abs)) return;

        if (!abs.Replace('\\', '/').StartsWith(Application.dataPath))
        {
            Debug.LogWarning("[GameSfxBank] 프로젝트의 Assets 폴더 안에 있는 폴더를 골라주세요.");
            return;
        }

        string rel = "Assets" + abs.Replace('\\', '/').Substring(Application.dataPath.Length);
        var clips = new List<AudioClip>();
        foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new[] { rel }))
        {
            var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (clip != null) clips.Add(clip);
        }
        clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        UnityEditor.Undo.RecordObject(this, "Auto Fill Game Sfx");

        // 기존에 손본 볼륨/지터는 유지
        var old = new Dictionary<GameSfxId, Entry>();
        foreach (var e in entries) if (e != null) old[e.id] = e;

        entries = new List<Entry>();
        var missing = new List<string>();

        foreach (GameSfxId id in Enum.GetValues(typeof(GameSfxId)))
        {
            string key = ToSnake(id.ToString());
            var matched = clips.FindAll(c => IsMatch(c.name, key));

            if (matched.Count == 0) missing.Add(key);

            var entry = old.TryGetValue(id, out var prev) ? prev : new Entry { id = id, pitchJitter = DefaultJitter(id) };
            entry.id = id;
            entry.clips = matched.ToArray();
            entries.Add(entry);
        }

        map = null;
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[GameSfxBank] 채우기 완료: {entries.Count - missing.Count}/{entries.Count}개 연결" +
                  (missing.Count > 0 ? $"\n파일을 못 찾은 항목: {string.Join(", ", missing)}" : ""), this);
    }

    // player_swing 은 "player_swing" 이거나 "player_swing_숫자" 일 때만 일치
    private static bool IsMatch(string clipName, string key)
    {
        if (clipName == key) return true;
        if (!clipName.StartsWith(key + "_")) return false;
        string rest = clipName.Substring(key.Length + 1);
        foreach (char ch in rest) if (!char.IsDigit(ch)) return false;
        return rest.Length > 0;
    }

    private static string ToSnake(string pascal)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < pascal.Length; i++)
        {
            char ch = pascal[i];
            if (char.IsUpper(ch) && i > 0) sb.Append('_');
            sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    private static float DefaultJitter(GameSfxId id) => id switch
    {
        GameSfxId.PlayerStep => 0.08f,
        GameSfxId.PlayerSwing => 0.05f,
        GameSfxId.PlayerHitEnemy => 0.05f,
        GameSfxId.PelletHit => 0.06f,
        GameSfxId.PelletPop => 0.08f,
        GameSfxId.EnemyHurt => 0.06f,
        GameSfxId.ChickenCluck => 0.07f,
        GameSfxId.EggSplat => 0.08f,
        GameSfxId.EggFall => 0.05f,
        GameSfxId.AngerTick => 0.03f,
        GameSfxId.MechaStomp => 0.04f,
        GameSfxId.MechaServo => 0.05f,
        GameSfxId.LaserLock => 0.02f,
        GameSfxId.LaserHit => 0.06f,
        GameSfxId.MechaFeatherLaunch => 0.05f,
        GameSfxId.MechaFeatherStick => 0.07f,
        GameSfxId.SparkZap => 0.06f,
        _ => 0.02f,
    };
#endif
}
