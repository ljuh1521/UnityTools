using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityTools.UI;

namespace UnityTools.Editor
{
    /// <summary>
    /// 눌림 연출이 켜진 버튼 중 <b>줄 맞춤 부품(LayoutGroup) 바로 아래에서 기준점이 가운데가 아닌 것</b>을
    /// 찾는다.
    ///
    /// 기준점이 가운데가 아니면 연출이 버튼 가운데를 지키려고 <b>위치를 옮기는데</b>, 줄 맞춤 부품은
    /// 자식의 자리를 직접 정한다. 둘이 같은 칸을 서로 쓰면 한 프레임씩 어긋나 떨려 보인다.
    /// 줄 맞춤 부품 아래에서는 기준점을 옮길 이유가 애초에 없으니(부품이 기준점만큼 되돌려 자리를
    /// 잡는다) 기준점을 가운데로 돌리면 된다. 2026-10-07 DefenceR 실측으로 이 조합은 0곳이었다 —
    /// 생기는 순간 알려 주려고 둔다.
    ///
    /// <b>프리팹만 본다. 씬은 안 연다</b>(열면 편집 상태가 바뀐다) — 그래서 결과에 그렇게 적는다.
    /// </summary>
    public static class ButtonPressCheck
    {
        private const string Tag = "[버튼 눌림 검사]";

        [EditorValidator("버튼 눌림 검사", 16)]
        public static void Validate()
        {
            var problems = new HashSet<string>();
            int prefabs = 0, buttons = 0, effectOn = 0, underLayout = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", Folders()))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null) continue;

                prefabs++;

                foreach (var button in prefab.GetComponentsInChildren<ButtonUI>(true))
                {
                    buttons++;

                    if (!button.pressEffectEnabled) continue;

                    effectOn++;

                    if (button.transform is not RectTransform rect || rect.parent == null) continue;

                    if (!rect.parent.TryGetComponent<LayoutGroup>(out var group) || !group.enabled) continue;

                    if (IgnoresLayout(rect)) continue;

                    underLayout++;

                    if ((rect.pivot - new Vector2(0.5f, 0.5f)).sqrMagnitude < 1e-8f) continue;

                    problems.Add($"{Path.GetFileName(path)} / {PathOf(rect, prefab.transform)} — " +
                                 $"기준점 {rect.pivot}, 부모 {group.GetType().Name}");
                }
            }

            // "몇 개를 봤는지"를 늘 같이 적는다. 버튼을 하나도 못 찾았으면 깨끗한 게 아니라 못 본 것이다.
            // 버튼 수는 프리팹마다 따로 센 자리다 — 중첩된 같은 버튼이 프리팹 수만큼 겹쳐 세진다.
            // 단위를 안 붙이면 다른 집계와 맞춰 볼 때 어긋난 줄 모른다.
            string seen = $"프리팹 {prefabs}개 · 버튼 {buttons}자리(프리팹마다 따로 셈) · 연출 켬 {effectOn}자리 · " +
                          $"줄 맞춤 아래 {underLayout}자리 · 씬은 안 봄";

            if (buttons == 0)
            {
                Debug.LogWarning($"{Tag} 버튼을 하나도 못 찾았습니다({seen}). **깨끗하다는 뜻이 아닙니다.**");
                return;
            }

            if (problems.Count == 0)
            {
                Debug.Log($"{Tag} 이상 없음 ({seen}).");
                return;
            }

            Debug.LogWarning($"{Tag} 줄 맞춤 부품 아래에서 기준점이 가운데가 아닌 버튼 {problems.Count}개 ({seen}).\n" +
                             "  누를 때 위치 보정과 줄 맞춤이 같은 칸을 써서 떨려 보입니다. 기준점을 가운데로 돌리세요.\n  " +
                             string.Join("\n  ", problems));
        }

        /// <summary>
        /// 프로젝트(<c>Assets</c>)와 <b>이 패키지 자신의 프리팹</b>을 같이 훑는다. 예전에는 Assets 만 봐서,
        /// 프로젝트 프리팹이 없는 SDK Host 에서는 돌릴 때마다 "버튼을 하나도 못 찾았다" 경고가 떴다 —
        /// 늘 뜨는 경고는 진짜 경고까지 무시하게 만든다(2026-10-07 코드 검토). 패키지 프리팹(그리드·
        /// 스크롤뷰 등)에 생긴 문제도 이제 여기서 잡힌다.
        /// </summary>
        private static string[] Folders()
        {
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ButtonUI).Assembly);

            return info != null ? new[] { "Assets", info.assetPath } : new[] { "Assets" };
        }

        private static bool IgnoresLayout(RectTransform rect)
        {
            foreach (var ignorer in rect.GetComponents<ILayoutIgnorer>())
            {
                if (ignorer.ignoreLayout) return true;
            }

            return false;
        }

        private static string PathOf(Transform node, Transform root)
        {
            var path = node.name;

            for (var p = node.parent; p != null && p != root; p = p.parent) path = $"{p.name}/{path}";

            return path;
        }
    }
}
