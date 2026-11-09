using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 원하는 순간에 Play() 한 번으로 만화를 재생하고, 끝나면 다음 씬으로 넘어간다.
/// 쿠키(크레딧) 스테이지 끝에서 엔딩 만화를 틀 때 쓴다.
///  - 콜라이더 트리거: Play On Trigger Enter 를 켜고 Is Trigger 콜라이더를 둔다 (플레이어 태그 "Player")
///  - 코드/UnityEvent: Play() 호출
/// 이미 본 만화는 건너뛰고 바로 다음 씬으로 간다(Always Play 를 켜면 매번 재생).
/// </summary>
public class ComicTrigger : MonoBehaviour
{
    [SerializeField] private ComicData comic;
    [Tooltip("만화가 끝난(또는 이미 본) 뒤 이동할 씬 이름. 비우면 이동하지 않음")]
    [SerializeField] private string nextSceneName = "Title";
    [SerializeField] private bool alwaysPlay;
    [SerializeField] private bool playOnTriggerEnter;

    private bool started;

    public void Play()
    {
        if (started) return;
        started = true;
        StartCoroutine(Run());
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (playOnTriggerEnter && other.CompareTag("Player")) Play();
    }

    private IEnumerator Run()
    {
        float prev = Time.timeScale;
        if (comic != null && (alwaysPlay || !ComicProgress.Seen(comic.comicId)))
        {
            Time.timeScale = 0f;
            var mode = FindFirstObjectByType<GameModeController>();
            if (mode != null) mode.EnableInputMap(InputMaps.UI);
            yield return ComicPlayer.Run(comic);
        }
        Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(nextSceneName) && Application.CanStreamedLevelBeLoaded(nextSceneName))
            SceneManager.LoadScene(nextSceneName);
        else if (!string.IsNullOrEmpty(nextSceneName))
            Debug.LogWarning($"[ComicTrigger] 씬 '{nextSceneName}' 을(를) 불러올 수 없습니다. (이름 / Build Settings 확인)", this);
    }
}