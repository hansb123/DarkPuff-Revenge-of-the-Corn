using UnityEngine;

/// <summary>
/// 인게임 효과음 재생 창구. 어디서든 한 줄이면 된다.
///   GameSfx.Play(GameSfxId.PlayerJump);                 // 화면 거리와 상관없이 같은 크기 (플레이어, UI성 소리)
///   GameSfx.PlayAt(GameSfxId.EnemyHurt, transform.position);   // 카메라에서 멀수록 작아짐 (적, 함정, 오브젝트)
/// 처음 부를 때 DontDestroyOnLoad 오브젝트를 만들고 Resources/GameSfxBank 에서 클립을 읽는다.
/// 소리는 AudioSource 여러 개를 돌려 쓰므로 음높이 흔들림이 서로 영향을 주지 않는다.
/// (나중에 Addressables로 바꾸려면 Load 부분만 교체)
/// </summary>
public static class GameSfx
{
    private const int VoiceCount = 12;

    private static GameSfxBank bank;
    private static AudioSource[] voices;
    private static int nextVoice;
    private static float[] lastPlayed;
    private static bool warned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        bank = null;
        voices = null;
        lastPlayed = null;
        nextVoice = 0;
        warned = false;
    }

    /// <summary>거리와 상관없이 재생</summary>
    public static void Play(GameSfxId id) => PlayInternal(id, 1f);

    /// <summary>월드 위치에서 난 소리. 카메라에서 멀면 작아지고, 너무 멀면 재생하지 않음</summary>
    public static void PlayAt(GameSfxId id, Vector2 position)
    {
        if (!EnsureReady()) return;
        PlayInternal(id, DistanceScale(position));
    }

    private static float DistanceScale(Vector2 position)
    {
        var cam = Camera.main;
        if (cam == null) return 1f;

        float dist = Vector2.Distance(position, cam.transform.position);
        if (dist <= bank.HearNear) return 1f;
        if (dist >= bank.HearFar) return 0f;
        return 1f - (dist - bank.HearNear) / (bank.HearFar - bank.HearNear);
    }

    private static void PlayInternal(GameSfxId id, float scale)
    {
        if (scale <= 0.001f || !EnsureReady()) return;
        if (!bank.TryGet(id, out var entry)) return;

        // 같은 소리가 너무 촘촘하게 나오면 무시
        int idx = (int)id;
        float now = Time.unscaledTime;
        if (now - lastPlayed[idx] < bank.MinInterval) return;
        lastPlayed[idx] = now;

        var clip = entry.clips[entry.clips.Length == 1 ? 0 : Random.Range(0, entry.clips.Length)];
        if (clip == null) return;

        var voice = NextVoice();
        voice.pitch = 1f + Random.Range(-entry.pitchJitter, entry.pitchJitter);
        voice.volume = bank.MasterVolume * entry.volume * scale;
        voice.clip = clip;
        voice.Play();
    }

    // 놀고 있는 소스를 우선 쓰고, 다 쓰는 중이면 가장 오래된 것부터 덮어씀
    private static AudioSource NextVoice()
    {
        for (int i = 0; i < voices.Length; i++)
        {
            var v = voices[(nextVoice + i) % voices.Length];
            if (!v.isPlaying)
            {
                nextVoice = (nextVoice + i + 1) % voices.Length;
                return v;
            }
        }

        var steal = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;
        return steal;
    }

    private static bool EnsureReady()
    {
        if (voices != null && bank != null) return true;

        if (bank == null) bank = Resources.Load<GameSfxBank>("GameSfxBank");
        if (bank == null)
        {
            if (!warned)
            {
                warned = true;
                Debug.LogWarning("[GameSfx] Resources 폴더에 'GameSfxBank' 에셋이 없습니다. (Create > Darkpuff > Game Sfx Bank)");
            }
            return false;
        }

        if (voices == null)
        {
            var go = new GameObject("[GameSfx]");
            Object.DontDestroyOnLoad(go);

            voices = new AudioSource[VoiceCount];
            for (int i = 0; i < VoiceCount; i++)
            {
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;   // 거리 감쇠는 PlayAt에서 직접 계산 (2D 게임이라 3D 사운드가 필요 없음)
                voices[i] = src;
            }
            lastPlayed = new float[System.Enum.GetValues(typeof(GameSfxId)).Length];
            for (int i = 0; i < lastPlayed.Length; i++) lastPlayed[i] = float.NegativeInfinity;
        }
        return true;
    }
}
