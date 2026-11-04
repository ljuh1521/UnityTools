using UnityEngine;
using UnityEngine.UI;

namespace UnityTools.UI
{
    /// <summary>
    /// 아무것도 그리지 않고 누름만 받는다. 투명한 Image 와 달리 스프라이트·재질이 필요 없다.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class RaycastArea : Graphic
    {
        protected override void OnPopulateMesh(VertexHelper vh) => vh.Clear();
    }
}
