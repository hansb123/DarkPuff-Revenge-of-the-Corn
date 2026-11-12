using UnityEngine;

/// <summary>
/// 테스트용 치트 스위치 모음 (PlayerPrefs에 저장). 타이틀의 숨은 버튼으로 켜고 끈다.
/// 정식 출시 빌드에서는 해당 버튼 오브젝트를 빼 두면 켤 방법이 없다.
/// </summary>
public static class DebugCheats
{
    private const string InfiniteHealthKey = "cheat_infinite_health";

    /// <summary>켜 두면 플레이어가 적/장애물에게 받는 피해를 전부 무시한다 (낙사는 그대로 죽음)</summary>
    public static bool InfiniteHealth
    {
        get => PlayerPrefs.GetInt(InfiniteHealthKey, 0) == 1;
        set { PlayerPrefs.SetInt(InfiniteHealthKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }
}