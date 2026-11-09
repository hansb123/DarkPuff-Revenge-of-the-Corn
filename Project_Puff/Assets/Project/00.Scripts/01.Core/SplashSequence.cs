using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임 시작 시 팀 로고 → AI 고지 화면을 차례로 보여주고 타이틀 씬으로 넘어간다.
/// 각 화면은 CanvasGroup을 가진 전체화면 오브젝트(Image 등)이며, 알파 0 → 1 → 유지 → 0 으로 페이드한다.
/// 아무 키/클릭/패드 버튼으로 현재 화면을 건너뛴다 (너무 빨리 넘어가지 않게 skipLockTime 동안은 무시).
/// 시간은 unscaledTime 기준이라 timeScale에 영향받지 않는다.
/// </summary>
public class SplashSequence : MonoBehaviour
{
    [Serializable]
    public class Screen
    {
        [Tooltip("이 화면의 CanvasGroup (전체화면 Image 오브젝트에 붙임)")]
        public CanvasGroup group;
        public float fadeIn = 0.5f;
        public float hold = 2f;
        public float fadeOut = 0.5f;
    }

    [SerializeField] private Screen[] screens;
    [Tooltip("모든 화면이 끝난 뒤 불러올 씬 (Build Settings에 등록되어 있어야 함)")]
    [SerializeField] private string nextSceneName = "Title";
    [Tooltip("화면이 뜬 뒤 이 시간(초) 동안은 건너뛰기 입력을 무시")]
    [SerializeField] private float skipLockTime = 0.6f;
    [Tooltip("첫 화면 전에 잠깐 기다리는 시간 (초)")]
    [SerializeField] private float startDelay = 0.3f;

    private bool skipRequested;

    private void Awake()
    {
        // 처음엔 모든 화면을 투명하게
        if (screens == null) return;
        foreach (var s in screens)
            if (s != null && s.group != null) { s.group.alpha = 0f; s.group.gameObject.SetActive(true); }
    }

    private IEnumerator Start()
    {
        Time.timeScale = 1f;
        yield return new WaitForSecondsRealtime(startDelay);

        if (screens != null)
        {
            foreach (var s in screens)
            {
                if (s == null || s.group == null) continue;
                yield return ShowScreen(s);
            }
        }
        SceneManager.LoadScene(nextSceneName);
    }

    private IEnumerator ShowScreen(Screen s)
    {
        skipRequested = false;
        float shownAt = Time.unscaledTime;

        // 페이드 인 (건너뛰기 요청 시 바로 페이드 아웃으로)
        yield return Fade(s.group, 0f, 1f, s.fadeIn, shownAt);

        // 유지
        float t = 0f;
        while (t < s.hold && !skipRequested)
        {
            t += Time.unscaledDeltaTime;
            CheckSkip(shownAt);
            yield return null;
        }

        // 페이드 아웃
        yield return Fade(s.group, s.group.alpha, 0f, s.fadeOut, -1f);
        s.group.gameObject.SetActive(false);
    }

    private IEnumerator Fade(CanvasGroup g, float from, float to, float duration, float skipCheckStart)
    {
        if (duration <= 0f) { g.alpha = to; yield break; }
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            g.alpha = Mathf.Lerp(from, to, t / duration);
            if (skipCheckStart >= 0f)
            {
                CheckSkip(skipCheckStart);
                if (skipRequested) { g.alpha = Mathf.Max(g.alpha, 1f); yield break; }
            }
            yield return null;
        }
        g.alpha = to;
    }

    private void CheckSkip(float shownAt)
    {
        if (Time.unscaledTime - shownAt < skipLockTime) return;

        var kb = Keyboard.current;
        var mouse = Mouse.current;
        var pad = Gamepad.current;
        if ((kb != null && kb.anyKey.wasPressedThisFrame) ||
            (mouse != null && mouse.leftButton.wasPressedThisFrame) ||
            (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame)))
            skipRequested = true;
    }
}