using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>버튼에 붙이면 키보드/패드로 포커스가 옮겨올 때 '톡' 소리가 남.</summary>
public class UISelectSound : MonoBehaviour, ISelectHandler
{
    private static float ignoreUntil;

    /// <summary>씬 시작할 때 첫 버튼을 코드로 선택해도 소리가 나지 않게 잠깐 막음 (unscaled 시간)</summary>
    public static void IgnoreFor(float seconds) => ignoreUntil = Time.unscaledTime + seconds;

    public void OnSelect(BaseEventData eventData)
    {
        if (Time.unscaledTime < ignoreUntil) return;
        UISfx.Play(UISfxId.Move);
    }
}
