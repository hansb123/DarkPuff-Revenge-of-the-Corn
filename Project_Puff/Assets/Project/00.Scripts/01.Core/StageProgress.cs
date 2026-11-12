using UnityEngine;

/// <summary>
/// 스테이지 클리어 기록 (PlayerPrefs). 스테이지 ID 형식: "{chapterId}_{NN}" 예) ch1_01
/// 나중에 저장 방식을 바꾸고 싶으면 이 클래스 안쪽만 고치면 됨.
/// </summary>
public static class StageProgress
{
    private const string ClearedPrefix = "cleared_";
    private const string LastPageKey = "chapter_last_page";
    private const string CheatUnlockKey = "cheat_unlock_all";

    public static string MakeId(string chapterId, int stageNumber) => $"{chapterId}_{stageNumber:00}";

    public static bool IsCleared(string stageId) =>
        !string.IsNullOrEmpty(stageId) && PlayerPrefs.GetInt(ClearedPrefix + stageId, 0) == 1;

    public static void MarkCleared(string stageId)
    {
        CollectibleProgress.CommitPending();

        if (string.IsNullOrEmpty(stageId)) return;
        PlayerPrefs.SetInt(ClearedPrefix + stageId, 1);
        PlayerPrefs.Save();
    }

    /// <summary>챕터 선택 화면에서 마지막으로 보던 페이지 (돌아왔을 때 그 페이지부터 보여줌)</summary>
    public static int LastPage
    {
        get => PlayerPrefs.GetInt(LastPageKey, 0);
        set => PlayerPrefs.SetInt(LastPageKey, value);
    }

    /// <summary>타이틀의 숨겨진 버튼으로 켜는 '모든 챕터 해금'. 클리어 기록은 건드리지 않고 잠금만 무시한다</summary>
    public static bool CheatUnlockAll
    {
        get => PlayerPrefs.GetInt(CheatUnlockKey, 0) == 1;
        set { PlayerPrefs.SetInt(CheatUnlockKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static void ResetAll()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }
}
