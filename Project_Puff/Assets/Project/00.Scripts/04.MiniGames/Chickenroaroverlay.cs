using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 닭 포효 연출 (미리 만들어 둔 프레임 18장을 재생). 집중선 + 소리 파동 + 의성어 + 먼지 + 번쩍임이 한 장에 합쳐져 있다.
/// 구성: Canvas 아래 전체 화면 Image 하나(Raycast Target 끔) + 이 컴포넌트. frames에 roar_00~17을 순서대로 넣는다.
/// 사용: Play() 호출 → 소리 재생 + 애니메이션 + 화면 흔들림 → Finished 이벤트.
/// 닭의 입 위치가 바뀌면 프레임의 파동 위치도 어긋나므로, 닭을 화면 가운데(기본 배치)에 둘 것.
/// </summary>
[RequireComponent(typeof(Image))]
public class ChickenRoarOverlay : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float fps = 12f;
    [SerializeField] private bool playSfx = true;

    [Header("화면 흔들림 (비우면 Camera.main)")]
    [SerializeField] private Transform shakeTarget;
    [SerializeField] private float shakeTime = 0.5f;
    [SerializeField] private float shakeDistance = 0.25f;

    public event Action Finished;
    public bool IsPlaying { get; private set; }

    private Image image;
    private Coroutine routine;

    // 다른 스크립트의 Awake가 먼저 Stop()/Play()를 불러도 안전하도록, 필요할 때 가져온다
    private Image Img
    {
        get
        {
            if (image == null)
            {
                image = GetComponent<Image>();
                image.raycastTarget = false;
            }
            return image;
        }
    }

    private void Awake()
    {
        Img.enabled = false;
    }

    public void Play()
    {
        if (frames == null || frames.Length == 0) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(PlayRoutine());
    }

    public void Stop()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        IsPlaying = false;
        Img.enabled = false;
    }

    private IEnumerator PlayRoutine()
    {
        IsPlaying = true;
        Img.enabled = true;
        if (playSfx) GameSfx.Play(GameSfxId.ChickenRoar);

        if (shakeTarget == null && Camera.main != null) shakeTarget = Camera.main.transform;
        StartCoroutine(Shake());

        float step = 1f / fps;
        for (int i = 0; i < frames.Length; i++)
        {
            Img.sprite = frames[i];
            // 일시정지(timeScale 0)에도 끝까지 재생되게 실시간 사용. 멈추고 싶으면 unscaled → scaled 로 변경
            yield return new WaitForSecondsRealtime(step);
        }

        Img.enabled = false;
        IsPlaying = false;
        routine = null;
        Finished?.Invoke();
    }

    private IEnumerator Shake()
    {
        if (shakeTarget == null) yield break;
        Vector3 basePos = shakeTarget.position;
        for (float t = 0f; t < shakeTime; t += Time.unscaledDeltaTime)
        {
            float fade = 1f - t / shakeTime;
            shakeTarget.position = basePos + new Vector3(Mathf.Sin(t * 80f), Mathf.Cos(t * 67f), 0f) * shakeDistance * fade;
            yield return null;
        }
        shakeTarget.position = basePos;
    }

    [ContextMenu("Debug/Play")] private void DebugPlay() => Play();
}