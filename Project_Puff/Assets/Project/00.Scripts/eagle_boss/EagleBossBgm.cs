using System.Collections;
using UnityEngine;

/// <summary>
/// 독수리 보스 페이즈별 BGM. 페이즈가 바뀔 때 포효에 맞춰 곡이 꺼졌다가, 포효가 끝날 때쯤 다음 페이즈 곡이 들어온다.
/// 1페이즈 곡은 등장 연출(EagleIntroSequence 의 Boss Bgm)이 이미 틀기 때문에 보통 비워 둔다.
/// 보스 오브젝트(또는 아무 오브젝트)에 붙이고 Phase Clips 에 페이즈 순서대로 곡을 넣는다.
///   Element 0 = 1페이즈 (비움)   Element 1 = 2페이즈 (bgm_boss_eagle_p2)   Element 2 = 3페이즈 (bgm_boss_eagle_p3)
/// </summary>
public class EagleBossBgm : MonoBehaviour
{
    [SerializeField] private EagleBoss boss;
    [SerializeField] private StageBgm stageBgm;

    [Tooltip("페이즈 순서대로 곡. 비워 둔 페이즈는 곡을 바꾸지 않음")]
    [SerializeField] private AudioClip[] phaseClips = new AudioClip[3];
    [Range(0f, 1f)][SerializeField] private float volume = 0.7f;

    [Header("전환 타이밍")]
    [Tooltip("페이즈가 바뀌는 순간 이전 곡이 꺼지는 시간(초)")]
    [SerializeField] private float fadeOut = 0.4f;
    [Tooltip("그 뒤 새 곡이 들어오기까지 기다리는 시간(초). 포효 연출(Transition Time) 길이에 맞추면 됨")]
    [SerializeField] private float newSongDelay = 1.6f;
    [Tooltip("새 곡이 들어오는 시간(초)")]
    [SerializeField] private float fadeIn = 0.6f;
    [Tooltip("보스를 쓰러뜨리면 곡을 서서히 끔")]
    [SerializeField] private bool fadeOutOnDefeat = true;
    [SerializeField] private float defeatFade = 1.2f;

    private Coroutine routine;

    private void Awake()
    {
        if (boss == null) boss = GetComponent<EagleBoss>();
        if (boss == null) boss = FindFirstObjectByType<EagleBoss>();
        if (stageBgm == null) stageBgm = FindFirstObjectByType<StageBgm>();
    }

    private void OnEnable()
    {
        if (boss == null) return;
        boss.PhaseEntered += OnPhase;
        boss.Defeated += OnDefeated;
    }

    private void OnDisable()
    {
        if (boss == null) return;
        boss.PhaseEntered -= OnPhase;
        boss.Defeated -= OnDefeated;
    }

    private void OnPhase(int index)
    {
        if (stageBgm == null || phaseClips == null || index < 0 || index >= phaseClips.Length) return;
        var clip = phaseClips[index];
        if (clip == null) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Change(clip));
    }

    private IEnumerator Change(AudioClip clip)
    {
        stageBgm.Suppress(fadeOut);
        yield return new WaitForSeconds(fadeOut + newSongDelay);
        stageBgm.Release(clip, volume, fadeIn);
        routine = null;
    }

    private void OnDefeated()
    {
        if (!fadeOutOnDefeat || stageBgm == null) return;
        if (routine != null) { StopCoroutine(routine); routine = null; }
        stageBgm.Suppress(defeatFade);
    }
}