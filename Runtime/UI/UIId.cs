using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityTools.UI
{
    /// <summary>
    /// 부품 이름표를 다루는 자리. <b>이름표 목록 자체는 프로젝트가 갖는다</b> — 패키지는 숫자만 받는다.
    ///
    /// 프로젝트는 자기 enum을 하나 만들어 시작할 때 등록한다:
    /// <code>
    /// [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    /// private static void RegisterIds() => UIId.Register(typeof(UIName));
    /// </code>
    /// 등록하면 인스펙터가 숫자 대신 <b>이름 드롭다운</b>으로 그려지고, 경고 로그에도 이름이 찍힌다.
    /// 등록을 안 해도 동작은 한다 — 숫자로 보일 뿐이다.
    /// </summary>
    public static class UIId
    {
        /// <summary>이름표를 안 붙인 부품. 프로젝트 enum의 None도 이 값이어야 한다.</summary>
        public const int None = -1;

        private static Type _enumType;
        private static string[] _names;
        private static int[] _values;

        /// <summary>프로젝트의 이름표 enum을 등록한다.</summary>
        public static void Register(Type enumType)
        {
            if (enumType == null || !enumType.IsEnum)
            {
                Debug.LogError($"[UI 이름표] enum이 아닙니다: {enumType}");
                return;
            }

            _enumType = enumType;
            _names = Enum.GetNames(enumType);

            var raw = Enum.GetValues(enumType);
            _values = new int[raw.Length];

            for (int i = 0; i < raw.Length; i++) _values[i] = Convert.ToInt32(raw.GetValue(i));
        }

        public static Type EnumType => _enumType;

        /// <summary>등록된 이름표 목록. 등록 전이면 빈 배열.</summary>
        public static string[] Names => _names ?? Array.Empty<string>();

        /// <summary>등록된 이름표 값 목록. <see cref="Names"/>와 자리가 같다.</summary>
        public static int[] Values => _values ?? Array.Empty<int>();

        /// <summary>
        /// 이름으로 이름표 값을 찾는다. <b>패키지 부품이 흔한 이름표를 쓸 때</b>의 통로다 —
        /// 예를 들어 <c>OutlineUI</c>는 자기 안의 "Image"와 "Outline"을 찾아야 하는데,
        /// 그 숫자를 패키지가 못박으면 프로젝트 enum의 번호와 어긋난다. 이름으로 찾으면
        /// 프로젝트가 번호를 어떻게 매기든 맞는다.
        ///
        /// 프로젝트 enum에 그 이름이 없으면 <see cref="None"/>을 주고 한 번만 알린다.
        /// </summary>
        public static int Of(string name)
        {
            if (_values == null)
            {
                // 등록 자체가 안 된 경우를 조용히 넘기면 공용 부품이 전부 제 자리를 못 찾는데
                // **오류 하나 없이 그냥 안 그려진다** — 테두리·그림자가 사라진 채로 넘어간다.
                if (_missing.Add("(등록 없음)"))
                    Debug.LogWarning("[UI 이름표] 목록이 등록되지 않았습니다 — UIId.Register(typeof(내enum))를 " +
                                     "시작할 때 부르세요. 그전까지 공용 부품은 자기 자리를 못 찾습니다.");

                return None;
            }

            for (int i = 0; i < _names.Length; i++)
            {
                if (_names[i] == name) return _values[i];
            }

            if (_missing.Add(name))
            {
                Debug.LogWarning($"[UI 이름표] 프로젝트 이름표 목록({_enumType?.Name})에 '{name}'이 없습니다. " +
                                 "이 이름을 쓰는 공용 부품은 그 자리를 못 찾습니다.");
            }

            return None;
        }

        private static readonly HashSet<string> _missing = new();

        /// <summary>숫자를 사람이 읽는 이름으로. 등록 전이거나 모르는 값이면 숫자 그대로.</summary>
        public static string Name(int id)
        {
            if (_values != null)
            {
                for (int i = 0; i < _values.Length; i++)
                {
                    if (_values[i] == id) return _names[i];
                }
            }

            return id.ToString();
        }
    }

    /// <summary>
    /// 이 필드를 이름표로 그린다 — 인스펙터에서 숫자 대신 드롭다운이 된다.
    /// 프로젝트가 <see cref="UIId.Register"/>를 안 했으면 그냥 숫자 칸으로 남는다.
    /// </summary>
    public class UIIdAttribute : PropertyAttribute
    {
    }
}
