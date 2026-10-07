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

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
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

                    var target = button.pressScaleTarget != null ? button.pressScaleTarget : button.transform;

                    if (target is not RectTransform rect || rect.parent == null) continue;

                    if (!rect.parent.TryGetComponent<LayoutGroup>(out var group) || !group.enabled) continue;

                    if (IgnoresLayout(rect)) continue;

                    underLayout++;

                    if ((rect.pivot - new Vector2(0.5f, 0.5f)).sqrMagnitude < 1e-8f) continue;

                    problems.Add($"{Path.GetFileName(path)} / {PathOf(rect, prefab.transform)} — " +
                                 $"기준점 {rect.pivot}, 부모 {group.GetType().Name}");
                }
            }

            // "몇 개를 봤는지"를 늘 같이 적는다. 버튼을 하나도 못 찾았으면 깨끗한 게 아니라 못 본 것이다.
            string seen = $"프리팹 {prefabs}개 · 버튼 {buttons}개 · 연출 켬 {effectOn}개 · " +
                          $"줄 맞춤 아래 {underLayout}개 · 씬은 안 봄";

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
