using System;
using UnityEngine;

/// <summary>
/// 배경음악/효과음 음량 설정 (0~1). PlayerPrefs에 저장되어 껐다 켜도 유지된다.
/// 실제 소리 크기 = (각 소리의 원래 크기) × 이 값.  GameSfx / UISfx / StageBgm / SceneBgm 이 읽어서 곱한다.
/// </summary>
public static class GameAudioSettings
{
    private const string BgmKey = "Darkpuff.BgmVolume";
    private const string SfxKey = "Darkpuff.SfxVolume";
    private const float Default = 0.8f;

    private static float bgm = -1f, sfx = -1f;

    public static event Action BgmChanged;
    public static event Action SfxChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { bgm = sfx = -1f; BgmChanged = null; SfxChanged = null; }

    public static float BgmVolume
    {
        get { if (bgm < 0f) bgm = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmKey, Default)); return bgm; }
        set
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(value, BgmVolume)) return;
            bgm = value;
            PlayerPrefs.SetFloat(BgmKey, value);
            BgmChanged?.Invoke();
        }
    }

    public static float SfxVolume
    {
        get { if (sfx < 0f) sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, Default)); return sfx; }
        set
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(value, SfxVolume)) return;
            sfx = value;
            PlayerPrefs.SetFloat(SfxKey, value);
            SfxChanged?.Invoke();
        }
    }

    /// <summary>설정창을 닫을 때 호출해서 디스크에 확실히 저장</summary>
    public static void Save() => PlayerPrefs.Save();
}
