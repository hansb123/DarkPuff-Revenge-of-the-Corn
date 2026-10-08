using UnityEngine;

/// <summary>
/// 플레이어가 상호작용할 수 있는 오브젝트(클리어 문, 미니게임 입구, 표지판 등)가 구현.
/// 트리거 콜라이더가 있는 오브젝트에 붙이고, 플레이어가 범위에 들어오면 말풍선이 뜬다.
/// </summary>
public interface IInteractable
{
    /// <summary>지금 상호작용 가능한가 (예: 보스를 잡기 전의 문은 false)</summary>
    bool CanInteract { get; }

    /// <summary>말풍선을 띄울 때 쓸 스프라이트 종류. 기본 키 안내 외에 느낌표 등을 쓰고 싶을 때</summary>
    InteractionPromptKind PromptKind { get; }

    void Interact(PlayerInteractor interactor);
}

public enum InteractionPromptKind
{
    Key,     // 키 안내 (E)
    Alert    // 느낌표 (키 없이 알림만)
}
