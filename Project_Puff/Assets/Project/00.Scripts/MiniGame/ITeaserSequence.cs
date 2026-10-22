using UnityEngine.Events;

/// <summary>
/// 문에 들어갈 때 보여주는 떡밥 연출. ChickenOutroSequence / MechaOutroSequence 가 구현하고,
/// GoalDoor 가 어느 쪽이든 같은 방식으로 재생한다.
/// </summary>
public interface ITeaserSequence
{
    bool IsTeaserPlaying { get; }
    void PlayTeaser();
    UnityEvent TeaserCompleted { get; }
}
