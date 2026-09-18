using System;
using UnityEditor;
using UnityEngine;
using UnityTools.UI;

namespace UnityTools.Editor
{
    /// <summary>
    /// 이름표 칸을 숫자 대신 <b>이름 드롭다운</b>으로 그린다.
    ///
    /// 이름표 목록은 프로젝트가 <see cref="UIId.Register"/>로 등록한 enum에서 가져온다.
    /// 등록 전이면 숫자 칸 그대로 둔다 — 그리는 쪽이 멋대로 목록을 만들면 없는 이름표를 고르게 된다.
    /// </summary>
    [CustomPropertyDrawer(typeof(UIIdAttribute))]
    public class UIIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Integer || UIId.Names.Length == 0)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            // BeginProperty/EndProperty로 감싸야 프리팹 Variant에서 **바뀐 값 표시(굵은 글씨·파란 줄)와
            // 우클릭 되돌리기**가 살아 있다. 직접 그리면서 이걸 빼면 그 표시가 통째로 사라진다.
            label = EditorGUI.BeginProperty(position, label, property);

            var names = UIId.Names;
            var values = UIId.Values;

            int current = -1;

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != property.intValue) continue;

                current = i;
                break;
            }

            // 목록에 없는 값은 **숫자를 그대로 보여준다.** 예전엔 못 찾으면 0번(보통 None)을 고른
            // 것처럼 그려서, enum에서 빠진 이름표를 물고 있는 프리팹이 "이름표 없음"으로 보였다 —
            // 실제로는 그 값이 그대로 저장돼 있고 Collect도 그 id로 등록한다(2026-09-18 코드 검토).
            if (current < 0)
            {
                var shown = new string[names.Length + 1];

                Array.Copy(names, shown, names.Length);
                shown[names.Length] = $"<목록에 없음: {property.intValue}>";

                names = shown;
                current = names.Length - 1;
            }

            EditorGUI.BeginChangeCheck();

            // 값이 서로 다른 여러 부품을 한 번에 고른 경우. 이 표시가 없으면 한 값만 보이고,
            // 그걸 건드리는 순간 나머지가 조용히 그 값으로 덮인다.
            bool mixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;

            int picked = EditorGUI.Popup(position, label.text, current, names);

            EditorGUI.showMixedValue = mixed;

            // 마지막 자리는 "목록에 없음" 표시라 고를 게 없다 — 골라도 값을 안 바꾼다.
            if (EditorGUI.EndChangeCheck() && picked < values.Length) property.intValue = values[picked];

            EditorGUI.EndProperty();
        }
    }
}
