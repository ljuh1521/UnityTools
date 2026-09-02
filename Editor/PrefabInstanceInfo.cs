using UnityEditor;
using UnityEngine;

namespace UnityTools.Editor
{
    /// <summary>
    /// 브릿지로 프리팹을 고치다가 실수하기 쉬운 것 하나를 잡는다 — 지금 만지는 오브젝트가
    /// <b>중첩 프리팹 인스턴스</b>면, 거기 건 값은 그 바깥 프리팹 파일 안의 "인스턴스 오버라이드"로만
    /// 저장되고 원본(중첩된 프리팹 에셋 자체)은 안 바뀐다. 구조·설정(컴포넌트 종류·머티리얼 등)을
    /// 고쳤는데 원본을 빠뜨리면, 그 프리팹을 다른 데서 새로 인스턴스화할 때 같은 문제가 재발한다
    /// (2026-09-01, DefenceR에서 실제로 이렇게 한 번 놓쳤다 — 사용자 지적: "버튼 하나 달랑
    /// 수정하면 문제가 해결되는 줄 알았어?").
    ///
    /// 예: <c>call UnityTools.Editor.PrefabInstanceInfo.Dump Assets/Prefabs/UI/Foo.prefab|Grid/Card</c>
    /// (프리팹 에셋 경로와 그 안쪽 오브젝트 경로를 <c>|</c>로 구분 — <c>call</c>은 문자열 인자
    /// 하나뿐이라 이렇게 합친다. 안쪽 경로를 비우면 루트 자체를 본다.)
    /// </summary>
    public static class PrefabInstanceInfo
    {
        public static void Dump(string arg)
        {
            if (string.IsNullOrEmpty(arg))
            {
                Debug.LogWarning("[프리팹 인스턴스 진단] <프리팹 에셋 경로>|<안쪽 경로> 형식입니다. " +
                                  "예: Assets/Prefabs/UI/Foo.prefab|Grid/Card (안쪽 경로 비우면 루트를 봄)");
                return;
            }

            int bar = arg.IndexOf('|');
            string prefabPath = bar < 0 ? arg : arg.Substring(0, bar).Trim();
            string innerPath = bar < 0 ? string.Empty : arg.Substring(bar + 1).Trim();

            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null)
            {
                Debug.LogWarning($"[프리팹 인스턴스 진단] 프리팹을 못 찾았습니다: {prefabPath}");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var target = string.IsNullOrEmpty(innerPath) ? root.transform : root.transform.Find(innerPath);

                if (target == null)
                {
                    Debug.LogWarning($"[프리팹 인스턴스 진단] 안쪽 경로를 못 찾았습니다: {innerPath}");
                    return;
                }

                var instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(target.gameObject);

                if (instanceRoot == null)
                {
                    Debug.Log($"[프리팹 인스턴스 진단] {target.name} — 중첩 프리팹 인스턴스가 아닙니다. " +
                              $"이 프리팹({prefabPath}) 안에서 직접 고치면 됩니다.");
                    return;
                }

                var sourceAsset = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);
                var sourcePath = sourceAsset != null ? AssetDatabase.GetAssetPath(sourceAsset) : "(원본을 못 찾음)";

                string instanceLabel = instanceRoot == target.gameObject
                    ? target.name
                    : $"{target.name}(인스턴스 루트: {instanceRoot.name})";

                Debug.Log($"[프리팹 인스턴스 진단] {instanceLabel} — 중첩 프리팹 인스턴스입니다. 원본: {sourcePath}");
                Debug.LogWarning($"[프리팹 인스턴스 진단] 값(스프라이트·색상·크기 등)만 고치는 거면 여기(인스턴스)만 고쳐도 됩니다. " +
                                  $"컴포넌트 종류·머티리얼·레이캐스트 설정처럼 '이 부품 자체의 문제'라면, " +
                                  $"원본({sourcePath})도 LoadPrefabContents로 따로 열어 같이 고쳐야 다른 데서 " +
                                  "새로 인스턴스화해도 재발하지 않습니다.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
