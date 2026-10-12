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

    /// <summary>어떤 입력으로 상호작용하는가 (E 키 / 위쪽 방향키)</summary>
    InteractionInput Input { get; }

    void Interact(PlayerInteractor interactor);
}

public enum InteractionPromptKind
{
    Key,     // 키 안내 (E)
    KeyUp,   // 위쪽 방향키 안내 (문에 들어갈 때)
    Alert    // 느낌표 (키 없이 알림만)
}

public enum InteractionInput
{
    Interact,   // Interact 액션 (기본 E 키)
    Up          // 위쪽 방향키 (Move 액션의 위쪽)
}
