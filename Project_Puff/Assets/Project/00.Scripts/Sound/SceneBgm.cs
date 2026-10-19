using System.Collections;
using UnityEngine;

/// <summary>
/// 타이틀/챕터 선택처럼 한 곡만 계속 틀면 되는 화면용 BGM. 씬에 하나 두고 곡만 넣으면 된다.
/// 시작할 때 서서히 켜지고, 장면을 떠날 때(Destroy) 서서히 꺼지지는 않으므로
/// 장면이 바뀔 때 끊기는 게 싫으면 Keep Between Scenes를 켜고 같은 곡이면 이어서 재생한다.
/// </summary>
public class SceneBgm : MonoBehaviour
{
    private static SceneBgm instance;

    [SerializeField] private AudioClip clip;
    [Range(0f, 1f)] [SerializeField] private float volume = 0.6f;
    [SerializeField] private float fadeInTime = 1f;
    [Tooltip("타이틀 → 챕터 선택처럼 같은 곡이 이어지는 장면 사이에서 끊기지 않게 함. 다른 곡이 있는 장면에서는 그 곡으로 교체됨")]
    [SerializeField] private bool keepBetweenScenes = true;

    private AudioSource source;

    private void Awake()
    {
        if (keepBetweenScenes && instance != null)
        {
            // 이미 같은 곡이 재생 중이면 이 오브젝트는 필요 없음. 다른 곡이면 기존 것을 치우고 이쪽을 씀
            if (instance.clip == clip) { Destroy(gameObject); return; }
            Destroy(instance.gameObject);
        }

        if (keepBetweenScenes) { instance = this; DontDestroyOnLoad(gameObject); }

        source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.Play();
        StartCoroutine(FadeIn());
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private IEnumerator FadeIn()
    {
        for (float t = 0f; t < fadeInTime; t += Time.unscaledDeltaTime)
        {
            source.volume = Mathf.Lerp(0f, volume, t / fadeInTime);
            yield return null;
        }
        source.volume = volume;
    }

    /// <summary>스테이지에 들어갈 때 호출하면 메뉴 BGM을 멈추고 지운다 (예: 시작 버튼에서)</summary>
    public static void StopMenuBgm()
    {
        if (instance != null) Destroy(instance.gameObject);
    }
}
