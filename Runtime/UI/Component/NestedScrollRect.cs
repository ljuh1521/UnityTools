using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnityTools.UI
{
    /// <summary>
    /// 자기 방향이 아닌 끌기를 바깥으로 넘기는 ScrollRect. 유니티는 끌기를 눌린 자리에서 가장 가까운
    /// 받는 곳 하나에만 주므로, 가로 목록 안에서 시작한 세로 끌기는 그대로 두면 바깥 세로 목록에 안 간다.
    /// 방향이 둘 다 꺼진 배치용이면 모든 끌기를 넘긴다.
    /// </summary>
    public class NestedScrollRect : ScrollRect
    {
        // 넘기는 동안만 값이 있다.
        private GameObject _outer;
        private PointerEventData _outerEvent;

        public override void OnInitializePotentialDrag(PointerEventData eventData)
        {
            // 안쪽을 눌러도 바깥의 관성이 멈추게 한다.
            if (transform.parent != null)
                ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, eventData, ExecuteEvents.initializePotentialDrag);

            base.OnInitializePotentialDrag(eventData);
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            if (IsCrossDrag(eventData) && transform.parent != null)
                _outer = ExecuteEvents.GetEventHandler<IDragHandler>(transform.parent.gameObject);

            if (_outer == null)
            {
                base.OnBeginDrag(eventData);
                return;
            }

            _outerEvent = eventData;
            ExecuteEvents.Execute(_outer, eventData, ExecuteEvents.beginDragHandler);
        }

        public override void OnDrag(PointerEventData eventData)
        {
            if (_outer != null) ExecuteEvents.Execute(_outer, eventData, ExecuteEvents.dragHandler);
            else base.OnDrag(eventData);
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            if (_outer != null) EndOuterDrag(eventData);
            else base.OnEndDrag(eventData);
        }

        protected override void OnDisable()
        {
            // 꺼지면 뗌을 못 받으므로, 여기서 안 끝내면 바깥이 끌리는 중인 채로 남는다.
            if (_outer != null) EndOuterDrag(_outerEvent);

            base.OnDisable();
        }

        // 프레임 하나의 이동량은 순간값이라 흔들리므로 누른 자리부터의 이동으로 판단한다. 같으면 자기 방향이다.
        private bool IsCrossDrag(PointerEventData eventData)
        {
            Vector2 moved = eventData.position - eventData.pressPosition;

            if (horizontal && vertical) return false;
            if (horizontal) return Mathf.Abs(moved.y) > Mathf.Abs(moved.x);
            if (vertical) return Mathf.Abs(moved.x) > Mathf.Abs(moved.y);
            return true;
        }

        private void EndOuterDrag(PointerEventData eventData)
        {
            ExecuteEvents.Execute(_outer, eventData, ExecuteEvents.endDragHandler);
            _outer = null;
            _outerEvent = null;
        }
    }
}
