using UnityEngine;

/// <summary>UI 효과음 묶음. Resources 폴더에 "UISfxBank" 이름으로 한 개만 두면 UISfx가 알아서 불러옴.</summary>
[CreateAssetMenu(menuName = "Darkpuff/UI Sfx Bank", fileName = "UISfxBank")]
public class UISfxBank : ScriptableObject
{
    [field: SerializeField] public AudioClip Move { get; private set; }
    [field: SerializeField] public AudioClip Submit { get; private set; }
    [field: SerializeField] public AudioClip Back { get; private set; }
    [field: SerializeField] public AudioClip Denied { get; private set; }
    [field: SerializeField] public AudioClip Tab { get; private set; }
    [field: SerializeField] public AudioClip Pop { get; private set; }
    [field: SerializeField] public AudioClip Clear { get; private set; }
    [field: SerializeField] public AudioClip Start { get; private set; }
    [field: SerializeField] public AudioClip Stamp { get; private set; }

    [field: Header("믹스")]
    [field: SerializeField, Range(0f, 1f)] public float Volume { get; private set; } = 0.8f;
    [field: Tooltip("Move / Tab처럼 자주 나오는 소리의 음높이 흔들림 (±)")]
    [field: SerializeField, Range(0f, 0.2f)] public float PitchJitter { get; private set; } = 0.06f;

    public AudioClip Get(UISfxId id) => id switch
    {
        UISfxId.Move => Move,
        UISfxId.Submit => Submit,
        UISfxId.Back => Back,
        UISfxId.Denied => Denied,
        UISfxId.Tab => Tab,
        UISfxId.Pop => Pop,
        UISfxId.Clear => Clear,
        UISfxId.Start => Start,
        UISfxId.Stamp => Stamp,
        _ => null
    };
}
