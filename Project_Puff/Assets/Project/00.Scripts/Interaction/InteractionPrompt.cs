using UnityEngine;

/// <summary>
/// 플레이어 머리 위에 뜨는 상호작용 말풍선(월드 스페이스 스프라이트).
/// 플레이어의 자식 오브젝트에 SpriteRenderer와 함께 붙인다.
/// 위아래로 살짝 흔들리고, 키를 누르는 순간 눌린 모양으로 바뀐다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class InteractionPrompt : MonoBehaviour
{
    [Header("Sprites")]
    [SerializeField] private Sprite keySprite;          // prompt_key_E
    [SerializeField] private Sprite keyPressedSprite;   // prompt_key_E_pressed
    [SerializeField] private Sprite alertSprite;        // prompt_alert

    [Header("Motion")]
    [SerializeField] private float bobAmplitude = 0.06f;
    [SerializeField] private float bobSpeed = 4f;
    [SerializeField] private float pressedDuration = 0.12f;

    private SpriteRenderer sr;
    private Vector3 basePosition;
    private float pressedUntil;
    private InteractionPromptKind kind;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        basePosition = transform.localPosition;
        Hide();
    }

    private void Update()
    {
        if (!sr.enabled) return;

        transform.localPosition = basePosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobAmplitude);

        if (kind == InteractionPromptKind.Key)
        {
            sr.sprite = Time.time < pressedUntil ? keyPressedSprite : keySprite;
        }
    }

    public void Show(InteractionPromptKind promptKind)
    {
        kind = promptKind;
        sr.sprite = promptKind == InteractionPromptKind.Alert ? alertSprite : keySprite;
        sr.enabled = true;
    }

    public void Hide()
    {
        sr.enabled = false;
    }

    /// <summary>키를 누른 순간 호출 → 잠깐 눌린 모양으로</summary>
    public void PlayPressed()
    {
        pressedUntil = Time.time + pressedDuration;
    }
}
