using UnityEngine;

/// <summary>
/// 스테이지 클리어 기록 (PlayerPrefs). 스테이지 ID 형식: "{chapterId}_{NN}" 예) ch1_01
/// 나중에 저장 방식을 바꾸고 싶으면 이 클래스 안쪽만 고치면 됨.
/// </summary>
public static class StageProgress
{
    private const string ClearedPrefix = "cleared_";
    private const string LastPageKey = "chapter_last_page";

    public static string MakeId(string chapterId, int stageNumber) => $"{chapterId}_{stageNumber:00}";

    public static bool IsCleared(string stageId) =>
        !string.IsNullOrEmpty(stageId) && PlayerPrefs.GetInt(ClearedPrefix + stageId, 0) == 1;

    public static void MarkCleared(string stageId)
    {
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

    public static void ResetAll()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }
}
