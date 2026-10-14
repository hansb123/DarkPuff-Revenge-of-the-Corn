using UnityEngine;

public enum UISfxId { Move, Submit, Back, Denied, Tab, Pop, Clear, Start, Stamp }

/// <summary>
/// UI 효과음 재생 창구. 어느 씬에서든 UISfx.Play(UISfxId.Submit) 한 줄이면 됨.
/// 처음 부를 때 DontDestroyOnLoad 오브젝트를 만들고, Resources/UISfxBank 에서 클립을 읽는다.
/// (나중에 Addressables로 바꾸려면 Load 부분만 교체)
/// </summary>
public static class UISfx
{
    private static UISfxBank bank;
    private static AudioSource source;
    private static bool warned;

    // 에디터에서 Domain Reload를 꺼 놓아도 값이 남지 않게 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        bank = null;
        source = null;
        warned = false;
    }

    public static void Play(UISfxId id)
    {
        if (!EnsureReady()) return;

        var clip = bank.Get(id);
        if (clip == null) return;

        // 자주 나오는 소리는 음높이를 살짝 흔들어서 기계적인 느낌을 줄임
        bool jitter = id == UISfxId.Move || id == UISfxId.Tab;
        source.pitch = jitter ? 1f + Random.Range(-bank.PitchJitter, bank.PitchJitter) : 1f;
        source.PlayOneShot(clip, bank.Volume);
    }

    private static bool EnsureReady()
    {
        if (source != null && bank != null) return true;

        if (bank == null) bank = Resources.Load<UISfxBank>("UISfxBank");
        if (bank == null)
        {
            if (!warned)
            {
                warned = true;
                Debug.LogWarning("[UISfx] Resources 폴더에 'UISfxBank' 에셋이 없습니다. (Create > Darkpuff > UI Sfx Bank)");
            }
            return false;
        }

        if (source == null)
        {
            var go = new GameObject("[UISfx]");
            Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
        }
        return true;
    }
}
