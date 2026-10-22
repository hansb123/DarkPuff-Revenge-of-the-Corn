using System;
using UnityEngine;

/// <summary>
/// 해금하는 능력 기록 (PlayerPrefs). StageProgress와 같은 방식이라 나중에 저장 방식을 바꿀 때 이 클래스만 고치면 된다.
/// </summary>
public static class AbilityProgress
{
    private const string GlideKey = "ability_glide";
    private const string HardKey = "ability_hard";

    /// <summary>활공 해금 직후 (UI/튜토리얼 표시 등에서 구독)</summary>
    public static event Action GlideUnlockedEvent;
    /// <summary>단단해지기 해금 직후</summary>
    public static event Action HardUnlockedEvent;

    public static bool GlideUnlocked => PlayerPrefs.GetInt(GlideKey, 0) == 1;

    public static void UnlockGlide()
    {
        if (GlideUnlocked) return;
        PlayerPrefs.SetInt(GlideKey, 1);
        PlayerPrefs.Save();
        GlideUnlockedEvent?.Invoke();
    }

    public static bool HardUnlocked => PlayerPrefs.GetInt(HardKey, 0) == 1;

    public static void UnlockHard()
    {
        if (HardUnlocked) return;
        PlayerPrefs.SetInt(HardKey, 1);
        PlayerPrefs.Save();
        HardUnlockedEvent?.Invoke();
    }

    public static void LockHard()
    {
        PlayerPrefs.DeleteKey(HardKey);
        PlayerPrefs.Save();
    }

    /// <summary>테스트용: 다시 잠그기</summary>
    public static void LockGlide()
    {
        PlayerPrefs.DeleteKey(GlideKey);
        PlayerPrefs.Save();
    }
}
