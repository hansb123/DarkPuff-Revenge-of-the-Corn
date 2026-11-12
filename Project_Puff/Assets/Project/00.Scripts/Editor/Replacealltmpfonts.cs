#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 프로젝트 안의 모든 TextMeshPro 텍스트(씬 + 프리팹)의 폰트를 한 번에 바꾸는 에디터 도구.
/// 사용: 메뉴 Tools → Replace All TMP Fonts → 새 폰트 에셋 지정 → Replace
/// 이 파일은 반드시 Assets/Editor/ 폴더 안에 둘 것 (빌드에는 포함되지 않음).
/// 실행 전에 GitHub Desktop 등으로 현재 상태를 커밋해 두는 것을 권장.
/// </summary>
public class ReplaceAllTmpFonts : ScriptableWizard
{
    [Tooltip("모든 텍스트에 적용할 TMP 폰트 에셋")]
    public TMP_FontAsset newFont;
    [Tooltip("프로젝트의 모든 씬 (.unity)")]
    public bool includeScenes = true;
    [Tooltip("프로젝트의 모든 프리팹 (.prefab)")]
    public bool includePrefabs = true;

    [MenuItem("Tools/Replace All TMP Fonts")]
    private static void Open()
    {
        DisplayWizard<ReplaceAllTmpFonts>("Replace All TMP Fonts", "Replace");
    }

    private void OnWizardUpdate()
    {
        isValid = newFont != null;
        helpString = "모든 TMP 텍스트의 Font Asset을 바꿉니다. 실행 전에 커밋/백업을 권장합니다.";
    }

    private void OnWizardCreate()
    {
        int texts = 0, assets = 0;

        if (includePrefabs)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/")) continue;   // 패키지 안 프리팹은 건드리지 않음

                var root = PrefabUtility.LoadPrefabContents(path);
                int n = Apply(root);
                if (n > 0) { PrefabUtility.SaveAsPrefabAsset(root, path); texts += n; assets++; }
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        if (includeScenes)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var original = SceneManager.GetActiveScene().path;

            foreach (var guid in AssetDatabase.FindAssets("t:Scene"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/")) continue;

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int n = 0;
                foreach (var go in scene.GetRootGameObjects()) n += Apply(go);
                if (n > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    texts += n; assets++;
                }
            }
            if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[ReplaceAllTmpFonts] {assets}개 파일, TMP 텍스트 {texts}개의 폰트를 '{newFont.name}'로 바꿨습니다.");
    }

    private int Apply(GameObject root)
    {
        int count = 0;
        var list = new List<TMP_Text>();
        root.GetComponentsInChildren(true, list);
        foreach (var t in list)
        {
            if (t.font == newFont) continue;
            t.font = newFont;
            EditorUtility.SetDirty(t);
            count++;
        }
        return count;
    }
}
#endif