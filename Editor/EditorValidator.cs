using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityTools.Editor
{
    /// <summary>
    /// 검사 메서드에 붙인다. 인자 없는 <b>정적</b> 메서드여야 하고, 반환값은 있어도 무시한다.
    ///
    /// 검사마다 메뉴를 두면 "어느 걸 돌려야 하지"가 되고 결국 아무도 안 돌린다 —
    /// 붙여만 두면 <see cref="EditorValidatorRunner"/>가 한 항목에서 전부 돌린다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class EditorValidatorAttribute : Attribute
    {
        public readonly string Label;
        public readonly int Order;

        public EditorValidatorAttribute(string label = null, int order = 0)
        {
            Label = label;
            Order = order;
        }
    }

    /// <summary>붙어 있는 검사를 한 번에 돌린다. 전부 읽기만 하므로 순서는 보기 좋게만 정한다.</summary>
    public static class EditorValidatorRunner
    {
        [MenuItem(UnityToolsMenu.Root + "검사", false, 20)]
        public static void ValidateAll()
        {
            var found = new List<(int order, string label, MethodInfo method)>();

            foreach (var method in TypeCache.GetMethodsWithAttribute<EditorValidatorAttribute>())
            {
                var attribute = method.GetCustomAttribute<EditorValidatorAttribute>();

                if (!method.IsStatic || method.GetParameters().Length > 0)
                {
                    Debug.LogWarning($"[검사] {method.DeclaringType?.Name}.{method.Name}은 인자 없는 " +
                                     "정적 메서드가 아니라 건너뜁니다.");
                    continue;
                }

                found.Add((attribute.Order, attribute.Label ?? method.DeclaringType?.Name, method));
            }

            if (found.Count == 0)
            {
                Debug.LogWarning("[검사] 검사가 없습니다. 검사 메서드에 [EditorValidator]를 붙이세요.");
                return;
            }

            found.Sort((a, b) => a.order != b.order
                ? a.order.CompareTo(b.order)
                : string.Compare(a.label, b.label, StringComparison.Ordinal));

            foreach (var item in found)
            {
                // 하나가 터져도 나머지는 돌아야 한다 — 안 그러면 첫 검사가 막힌 동안 나머지가 잠든다.
                try
                {
                    item.method.Invoke(null, null);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[검사] {item.label}이 예외로 멈췄습니다: {e.InnerException ?? e}");
                }
            }

            Debug.Log($"[검사] {found.Count}개 완료 — 위 로그에 오류·경고가 없으면 이상 없습니다.");
        }
    }
}
