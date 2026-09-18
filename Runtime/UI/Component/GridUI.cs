using System.Collections.Generic;
using UnityEngine;

namespace UnityTools.UI
{
    public class GridUI : GenericUI
    {
        private int maxCount;
        private readonly List<ElementUI> _uiList = new();

        protected override bool Collect()
        {
            bool result = base.Collect();

            if (result)
            {
                for (int i = 0; i < RectTransform.childCount; i++)
                {
                    _uiList.Add(RectTransform.GetChild(i).GetComponent<ElementUI>());
                }

                maxCount = _uiList.Count;
            }

            return result;
        }

        public int MaxCount
        {
            get
            {
                Collect();
                return maxCount;
            }
        }

        public T GetUI<T>(int index) where T : ElementUI
        {
            Collect();

            if (index < 0 || index >= _uiList.Count) return null;

            // 수집은 한 번만 돈다. 그때 자식에 컴포넌트가 아직 없었으면 null이 그대로 굳어 계속 빈 것으로 보인다
            // (에디터에서 도구로 읽을 때 그랬다). 비어 있는 자리만 다시 찾아 메운다.
            if (_uiList[index] == null && index < RectTransform.childCount)
            {
                _uiList[index] = RectTransform.GetChild(index).GetComponent<ElementUI>();
            }

            return _uiList[index] as T;
        }
        
        
        public T AddPrefabUI<T>(T prefab) where T : ElementUI
        {
            T element = Instantiate(prefab, RectTransform);
            
            _uiList.Add(element);

            return element;
        }

    }
}
