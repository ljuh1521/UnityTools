using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityTools.UI
{
    /// <summary>
    /// 부품을 <b>이름표로 찾아 쓰게</b> 해주는 화면 묶음. 한 번 훑어 이름표 → 부품 표를 만들어 두고,
    /// 그다음부터는 <see cref="Get{TElement}(int)"/>으로 꺼낸다.
    ///
    /// 인스펙터에서 부품을 하나하나 연결하지 않아도 되고, 계층 구조를 바꿔도 코드가 안 깨진다.
    ///
    /// <b>중첩된 묶음에서 멈춘다</b>(<see cref="CollectElements"/>) — 안쪽 묶음은 자기 부품을 자기가
    /// 관리하므로, 안쪽 이름표가 바깥으로 새지 않는다. 같은 이름표를 화면마다 재사용할 수 있는 게
    /// 이 규칙 덕분이다. 대신 "안쪽 부품이 왜 null인지"가 계층만 봐서는 안 보이니, 진단할 때는
    /// 이 경계를 같이 봐야 한다.
    /// </summary>
    public class GenericUI : ElementUI
    {
        private readonly Dictionary<int, IElementUI> _elementMap = new();
        private bool _isCollected = false;
        protected bool _isInited = false;

        protected virtual void Awake()
        {
            Collect();
        }

        protected virtual void Start() { }

        protected virtual bool Collect()
        {
            if (_isCollected) return false;

            foreach (Transform child in RectTransform)
            {
                CollectElements(child);
            }

            _isCollected = true;
            return true;
        }

        private void CollectElements(Transform current)
        {
            if (current.TryGetComponent<IElementUI>(out var element))
            {
                if (element.Id != UIId.None)
                {
                    if (!_elementMap.ContainsKey(element.Id))
                    {
                        _elementMap[element.Id] = element;
                    }
                    else
                    {
                        Debug.LogWarning($"이름표가 겹칩니다: {UIId.Name(element.Id)} : {element.GameObject.name}");
                    }
                }
            }

            if (element as GenericUI) return;

            foreach (Transform child in current)
            {
                CollectElements(child);
            }
        }

        public virtual void UpdateGenericUI(bool isEditor) { }

        public virtual void UpdateUI() { }

        protected void EndEditor(params Object[] elementUI)
        {
#if UNITY_EDITOR
            // 🔹 저장 가능 상태로 처리
            foreach (var ui in elementUI)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(ui);
                EditorUtility.SetDirty(ui);
            }

            // 🔹 씬 뷰 즉시 반영
            SceneView.RepaintAll();
#endif
        }

        // 이름표는 숫자로만 받는다. 프로젝트의 enum을 그대로 넘기고 싶으면 그쪽에서 확장 메서드를
        // 하나 둔다 — `public static T Get<T>(this GenericUI ui, UIName id) => ui.Get<T>((int)id);`
        //
        // 여기서 `Enum`을 받는 판을 두면 안 된다. enum을 `Enum`으로 넘기는 순간 참조 변환이 일어나
        // **호출마다 힙 할당**이 생기고, 매 프레임 도는 화면에서 GC 끊김으로 나온다
        // (2026-09-18 코드 검토 지적 — 전투 화면이 확정 프레임마다 이 자리를 지난다).

        public TElement Get<TElement>(int id) where TElement : class, IElementUI
        {
            Collect();

            if (_elementMap.TryGetValue(id, out var elem))
            {
                return elem as TElement;
            }

            return null;
        }

        public bool TryGet<TElement>(int id, out TElement result) where TElement : class, IElementUI
        {
            result = null;

            Collect();

            if (_elementMap.TryGetValue(id, out var elem))
            {
                result = elem as TElement;
                if (result != null)
                {
                    return true;
                }
            }

            return false;
        }

        public RectTransform GetTransform(int id)
        {
            var elementUI = Get<IElementUI>(id);

            return elementUI?.RectTransform;
        }

        public bool TryGetTransform(int id, out RectTransform result)
        {
            if (TryGet<IElementUI>(id, out var elementUI))
            {
                result = elementUI.RectTransform;
                return true;
            }

            result = null;
            return false;
        }

        public GameObject GetObject(int id)
        {
            var elementUI = Get<IElementUI>(id);

            return elementUI?.GameObject;
        }

        public bool TryGetObject(int id, out GameObject result)
        {
            if (TryGet<IElementUI>(id, out var elementUI))
            {
                result = elementUI.GameObject;
                return true;
            }

            result = null;
            return false;
        }
    }
}
