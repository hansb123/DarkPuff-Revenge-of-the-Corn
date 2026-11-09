using UnityEngine;

/// <summary>만화를 봤는지 기록 (PlayerPrefs). 다시 보기 버튼 해금과 '처음 한 번만 재생'에 쓴다.</summary>
public static class ComicProgress
{
    private const string Prefix = "comic_seen_";

    public static bool Seen(string id) =>
        !string.IsNullOrEmpty(id) && PlayerPrefs.GetInt(Prefix + id, 0) == 1;

    public static void MarkSeen(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        PlayerPrefs.SetInt(Prefix + id, 1);
        PlayerPrefs.Save();
    }

    /// <summary>다시 보기 버튼 해금 여부 (모든 챕터 해금 치트가 켜져 있으면 전부 열림)</summary>
    public static bool Unlocked(string id) => Seen(id) || StageProgress.CheatUnlockAll;
}
