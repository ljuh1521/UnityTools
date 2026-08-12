using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityTools.Editor
{
    /// <summary>
    /// 프리팹을 에디터에서 렌더해 PNG로 저장한다.
    ///
    /// 만든 결과를 <b>사람이 화면을 보고 말로 옮기지 않아도 되게</b> 하려고 둔다.
    /// 생성기를 고칠 때마다 스크린샷을 부탁하면 왕복이 길어진다 — 이 그림을 파일로 남기면
    /// 만든 쪽이 직접 확인하고 다음 수정을 넣을 수 있다.
    ///
    /// 저장 위치는 <c>Logs/UIPreview</c>다(git 무시 폴더라 커밋에 안 섞인다).
    /// </summary>
    public static class PrefabPreviewCapture
    {
        private const string OutputDir = "Logs/UIPreview";

        // 열려 있는 씬에 임시로 놓고 찍는다. 다른 오브젝트가 화면에 끼지 않게 멀리 떨어뜨린다.
        private static readonly Vector3 Stage = new(10000, 10000, 0);

        /// <summary>배경색. 프리팹이 배경과 같은 색이면 안 보이므로 프로젝트에서 바꿀 수 있게 둔다.</summary>
        public static Color Background = new(0.22f, 0.24f, 0.28f, 1f);

        /// <summary>앵커로만 크기가 잡힌 UI 프리팹을 찍을 때 쓸 화면 크기.</summary>
        public static Vector2 ReferenceResolution = new(1080, 1920);

        private const int MaxSize = 2048;
        private const float WorldPadding = 1.15f;

        // 월드 오브젝트는 크기가 유닛이라 그대로 픽셀로 쓰면 몇 픽셀짜리 그림이 나온다.
        // 스프라이트 기본값과 같은 배율로 바꿔 찍는다.
        private const float WorldPixelsPerUnit = 100f;

        /// <summary>
        /// 렌더 직전에 인스턴스를 손볼 기회. 에디터에서 한 번 초기화해야 메시가 생기는 런타임(스파인 등)을
        /// 코어가 알지 않아도 되게 하는 자리다. <c>[InitializeOnLoad]</c>에서 더한다.
        /// </summary>
        public static readonly List<Action<GameObject>> PreRender = new();

        [MenuItem(UnityToolsMenu.Root + "프리팹 미리보기 저장", false, 70)]
        public static void CaptureSelection()
        {
            var targets = Selection.GetFiltered<GameObject>(SelectionMode.Assets);

            if (targets.Length == 0)
            {
                Debug.LogError("[미리보기] 프로젝트 창에서 프리팹을 고른 뒤 실행하세요.");
                return;
            }

            var saved = new List<string>();

            foreach (var target in targets)
            {
                string path = Capture(target);

                if (path != null) saved.Add(path);
            }

            if (saved.Count == 0) return;

            Debug.Log($"[미리보기] {saved.Count}장 저장\n  " + string.Join("\n  ", saved));
            EditorUtility.RevealInFinder(saved[0]);
        }

        /// <summary>프리팹 하나를 찍어 저장하고 파일 경로를 돌려준다.</summary>
        public static string Capture(GameObject prefab)
        {
            if (prefab == null) return null;

            Directory.CreateDirectory(OutputDir);

            var temp = new List<GameObject>();

            var instance = UnityEngine.Object.Instantiate(prefab);
            Hide(instance);
            temp.Add(instance);

            var cameraGo = new GameObject("PreviewCamera", typeof(Camera));
            Hide(cameraGo);
            temp.Add(cameraGo);

            var camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.cullingMask = ~0;

            int width, height;

            if (instance.transform is RectTransform)
            {
                SetupUI(instance, camera, out width, out height);
            }
            else if (!SetupWorld(instance, camera, out width, out height))
            {
                Debug.LogWarning($"[미리보기] 그릴 게 없습니다: {prefab.name}");
                Cleanup(temp);
                return null;
            }

            string file = Path.Combine(OutputDir, prefab.name + ".png");
            Render(camera, width, height, file);

            Cleanup(temp);

            return file.Replace("\\", "/");
        }

        /// <summary>UI는 월드 스페이스 캔버스에 얹어 1픽셀 = 1유닛으로 찍는다.</summary>
        private static void SetupUI(GameObject instance, Camera camera, out int width, out int height)
        {
            var rect = (RectTransform)instance.transform;
            var size = rect.sizeDelta;

            // 앵커로만 크기가 잡힌 프리팹은 sizeDelta가 0이다. 그때는 화면 크기로 본다.
            if (size.x <= 1 || size.y <= 1) size = ReferenceResolution;

            var canvasGo = new GameObject("PreviewCanvas", typeof(RectTransform), typeof(Canvas));
            Hide(canvasGo);

            canvasGo.transform.position = Stage;

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;

            var canvasRect = (RectTransform)canvasGo.transform;
            canvasRect.sizeDelta = size;

            rect.SetParent(canvasRect, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            camera.transform.position = Stage + new Vector3(0, 0, -100);
            camera.orthographicSize = size.y * 0.5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;

            Fit(size.x, size.y, out width, out height);

            // 캔버스는 인스턴스와 함께 지워지지 않으므로 부모로 바꿔 같이 지워지게 한다.
            instance.transform.SetParent(canvasRect, true);
        }

        /// <summary>월드 오브젝트는 렌더러 경계를 재서 카메라를 맞춘다.</summary>
        private static bool SetupWorld(GameObject instance, Camera camera, out int width, out int height)
        {
            width = height = 0;

            instance.transform.position = Stage;

            foreach (var hook in PreRender)
            {
                try { hook(instance); }
                catch (Exception e) { Debug.LogWarning($"[미리보기] 준비 훅에서 예외: {e.Message}"); }
            }

            var renderers = instance.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0) return false;

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);

            if (bounds.size.x <= 0 || bounds.size.y <= 0) return false;

            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.center.z - 100);
            camera.orthographicSize = bounds.size.y * 0.5f * WorldPadding;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;

            Fit(bounds.size.x * WorldPixelsPerUnit * WorldPadding,
                bounds.size.y * WorldPixelsPerUnit * WorldPadding, out width, out height);

            return true;
        }

        private static void Render(Camera camera, int width, int height, string file)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;

            camera.targetTexture = target;
            camera.aspect = (float)width / height;
            camera.Render();

            RenderTexture.active = target;

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();

            File.WriteAllBytes(file, texture.EncodeToPNG());

            RenderTexture.active = previous;
            camera.targetTexture = null;

            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(target);
        }

        /// <summary>가로세로 비를 지키면서 긴 변을 MaxSize 안으로 넣는다.</summary>
        private static void Fit(float sourceWidth, float sourceHeight, out int width, out int height)
        {
            float scale = Mathf.Min(1f, MaxSize / Mathf.Max(sourceWidth, sourceHeight));

            width = Mathf.Max(4, Mathf.RoundToInt(sourceWidth * scale));
            height = Mathf.Max(4, Mathf.RoundToInt(sourceHeight * scale));
        }

        /// <summary>씬에 저장되지 않게 표시한다 — 임시 오브젝트가 씬에 남으면 안 된다.</summary>
        private static void Hide(GameObject go)
        {
            go.hideFlags = HideFlags.HideAndDontSave;

            foreach (var child in go.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        private static void Cleanup(List<GameObject> temp)
        {
            foreach (var go in temp)
            {
                if (go == null) continue;

                // UI는 임시 캔버스 밑으로 들어가 있어 루트부터 지운다.
                var root = go.transform.root.gameObject;

                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
