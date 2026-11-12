using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 수집품 진행 상태.
///  - 스테이지에서 먹으면 '임시 보유(pending)'. 아직 저장되지 않는다.
///  - 스테이지를 클리어하면(StageProgress.MarkCleared) 임시 보유분이 영구 저장(PlayerPrefs)된다.
///  - 클리어 전에 나가거나 씬이 다시 불려오면 임시 보유분은 사라진다 (그 수집품은 다시 먹어야 함).
///  - 이미 영구 저장된 수집품은 다시 먹어도 아무 일도 일어나지 않는다 (TryPickUp이 false).
/// </summary>
public static class CollectibleProgress
{
    private const string Prefix = "collect_";
    private static readonly HashSet<string> pending = new HashSet<string>();

    /// <summary>임시 보유에 새로 추가됐을 때 (HUD 표시용)</summary>
    public static event Action<string> PendingAdded;
    /// <summary>영구 저장됐을 때 (새로 얻은 id 목록)</summary>
    public static event Action<IReadOnlyList<string>> Committed;

    private const string CheatKey = "cheat_unlock_collectibles";

    /// <summary>테스트용 '모든 수집품 해금'. 켜 두는 동안 도감에 전부 있는 것으로 취급하고, 실제 기록은 건드리지 않는다 (끄면 원래 상태)</summary>
    public static bool CheatUnlockAll
    {
        get => PlayerPrefs.GetInt(CheatKey, 0) == 1;
        set { PlayerPrefs.SetInt(CheatKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static bool IsOwned(string id) =>
        !string.IsNullOrEmpty(id) && (CheatUnlockAll || PlayerPrefs.GetInt(Prefix + id, 0) == 1);

    // 실제 저장 기록만 (치트 무시)
    private static bool IsSaved(string id) => PlayerPrefs.GetInt(Prefix + id, 0) == 1;

    public static bool IsPending(string id) => !string.IsNullOrEmpty(id) && pending.Contains(id);
    public static int PendingCount => pending.Count;

    /// <summary>
    /// 먹기 시도. 이미 가지고 있거나(저장됨) 이번 판에 먹은 것이면 아무 일도 없이 false.
    /// 새로 먹으면 true.
    /// </summary>
    public static bool TryPickUp(string id)
    {
        if (string.IsNullOrEmpty(id) || IsSaved(id) || pending.Contains(id)) return false;   // 치트와 상관없이 실제 기록 기준
        pending.Add(id);
        PendingAdded?.Invoke(id);
        return true;
    }

    /// <summary>스테이지 클리어 시 호출 (StageProgress.MarkCleared가 부른다). 새로 저장된 개수를 돌려준다</summary>
    public static int CommitPending()
    {
        if (pending.Count == 0) return 0;
        var added = new List<string>(pending);
        foreach (var id in added) PlayerPrefs.SetInt(Prefix + id, 1);
        PlayerPrefs.Save();
        pending.Clear();
        Committed?.Invoke(added);
        return added.Count;
    }

    public static void ClearPending() => pending.Clear();

    public static int OwnedCount(IEnumerable<CollectibleData> all)
    {
        int n = 0;
        if (all == null) return 0;
        foreach (var c in all) if (c != null && IsOwned(c.id)) n++;
        return n;
    }

    /// <summary>테스트용</summary>
    public static void Lock(string id)
    {
        PlayerPrefs.DeleteKey(Prefix + id);
        PlayerPrefs.Save();
    }

    // 새 씬이 불려오면 아직 저장 안 된 임시 보유분은 버린다.
    // (클리어한 경우는 이미 CommitPending으로 비워진 뒤)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        pending.Clear();
        SceneManager.sceneLoaded += (_, __) => pending.Clear();
    }
}