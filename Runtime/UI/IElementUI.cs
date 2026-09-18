using UnityEngine;

namespace UnityTools.UI
{
    /// <summary>
    /// 화면 부품 하나. <b>이름표(<see cref="Id"/>)로 찾아 쓰는 것</b>이 이 체계의 전부다 —
    /// 인스펙터에서 하나하나 연결하지 않아도 되고, 계층 구조를 바꿔도 코드가 안 깨진다.
    ///
    /// 이름표가 <c>int</c>인 이유: 게임마다 필요한 이름표가 다른데(어떤 게임은 Build·Spawn,
    /// 다른 게임은 Deck·Draw), 공용 패키지가 그 목록을 갖고 있으면 게임마다 <b>패키지 파일을
    /// 고쳐야 한다.</b> 그러면 공용이 아니다. 그래서 목록은 프로젝트가 자기 enum으로 갖고,
    /// 패키지는 그 값을 숫자로만 받는다 — <see cref="UIId.Register"/> 참조.
    /// </summary>
    public interface IElementUI
    {
        /// <summary>이름표. 안 붙인 것은 <see cref="UIId.None"/>.</summary>
        int Id { get; }

        RectTransform RectTransform { get; }
        GameObject GameObject { get; }
    }
}
