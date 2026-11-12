using System;
using UnityEngine;

/// <summary>
/// 해금하는 능력 기록 (PlayerPrefs). StageProgress와 같은 방식이라 나중에 저장 방식을 바꿀 때 이 클래스만 고치면 된다.
/// </summary>
public static class AbilityProgress
{
    private const string GlideKey = "ability_glide";
    private const string HardKey = "ability_hard";
    private const string CheatKey = "cheat_unlock_abilities";

    /// <summary>테스트용 '모든 능력 해금'. 켜 두는 동안 전부 해금된 것으로 취급하고, 실제 해금 기록은 건드리지 않는다 (끄면 원래 상태)</summary>
    public static bool CheatUnlockAll
    {
        get => PlayerPrefs.GetInt(CheatKey, 0) == 1;
        set { PlayerPrefs.SetInt(CheatKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    /// <summary>활공 해금 직후 (UI/튜토리얼 표시 등에서 구독)</summary>
    public static event Action GlideUnlockedEvent;
    /// <summary>단단해지기 해금 직후</summary>
    public static event Action HardUnlockedEvent;

    public static bool GlideUnlocked => CheatUnlockAll || PlayerPrefs.GetInt(GlideKey, 0) == 1;

    public static void UnlockGlide()
    {
        if (PlayerPrefs.GetInt(GlideKey, 0) == 1) return;   // 치트가 켜져 있어도 실제 기록은 따로 저장
        PlayerPrefs.SetInt(GlideKey, 1);
        PlayerPrefs.Save();
        GlideUnlockedEvent?.Invoke();
    }

    public static bool HardUnlocked => CheatUnlockAll || PlayerPrefs.GetInt(HardKey, 0) == 1;

    public static void UnlockHard()
    {
        if (PlayerPrefs.GetInt(HardKey, 0) == 1) return;
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