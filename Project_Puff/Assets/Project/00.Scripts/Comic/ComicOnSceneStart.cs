using System.Collections;
using UnityEngine;

/// <summary>
/// 이 씬에 들어올 때 아직 안 본 만화면 재생하고 시작한다. (처음 한 번만)
/// 오프닝  → 1챕터 1스테이지 씬에 놓고 opening 만화 연결
/// 챕터2   → 2챕터 1스테이지 씬에 놓고 chapter2 만화 연결
/// 타이틀 시작 / 챕터 선택 / 결과창의 '다음' 어느 경로로 들어와도 동작한다.
///
/// 재생 중에는 시간을 멈추고 플레이어 입력 맵을 UI로 돌려 놓았다가, 끝나면 원래대로 되돌린다.
/// </summary>
public class ComicOnSceneStart : MonoBehaviour
{
    [SerializeField] private ComicData comic;
    [Tooltip("켜면 이미 본 만화라도 매번 재생 (테스트용)")]
    [SerializeField] private bool alwaysPlay;
    [Tooltip("재생 전 기다릴 프레임 수 (GameModeController가 입력 맵을 켠 뒤에 끄기 위함)")]
    [SerializeField] private int waitFrames = 2;

    private IEnumerator Start()
    {
        if (comic == null) yield break;
        if (!alwaysPlay && ComicProgress.Seen(comic.comicId)) yield break;

        // 만화 위에서는 스테이지가 보이지 않도록 즉시 검은 화면을 깔고 시작
        float prevScale = Time.timeScale;
        Time.timeScale = 0f;

        for (int i = 0; i < waitFrames; i++) yield return null;

        var mode = FindFirstObjectByType<GameModeController>();
        if (mode != null) mode.EnableInputMap(InputMaps.UI);

        yield return ComicPlayer.Run(comic);

        Time.timeScale = prevScale <= 0f ? 1f : prevScale;
        if (mode != null) mode.EnableInputMap(InputMaps.Platformer);
    }
}
