using System;
using UnityEngine;

public enum Difficulty
{
    Easy = 0,     // 무한 체크포인트 부활
    Normal = 1    // 스테이지(씬)당 목숨 제한, 다 잃으면 스테이지 처음부터
}

/// <summary>
/// 난이도 설정. PlayerPrefs에 저장되어 게임을 껐다 켜도 유지된다. 기본은 Easy.
/// 난이도 선택 UI(DifficultySelectUi)가 바꾸고, RespawnManager가 읽는다.
/// </summary>
public static class GameDifficulty
{
    private const string Key = "Darkpuff.Difficulty";
    private static Difficulty? cached;

    public static event Action<Difficulty> Changed;

    public static Difficulty Current
    {
        get
        {
            if (!cached.HasValue)
            {
                int v = PlayerPrefs.GetInt(Key, (int)Difficulty.Easy);
                cached = Enum.IsDefined(typeof(Difficulty), v) ? (Difficulty)v : Difficulty.Easy;
            }
            return cached.Value;
        }
        set
        {
            if (cached.HasValue && cached.Value == value) return;
            cached = value;
            PlayerPrefs.SetInt(Key, (int)value);
            PlayerPrefs.Save();
            Changed?.Invoke(value);
        }
    }

    public static bool IsNormal => Current == Difficulty.Normal;
    public static bool IsEasy => Current == Difficulty.Easy;
}