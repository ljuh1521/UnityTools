using System.Linq;
using System.Text;
using Spine.Unity;
using UnityEditor;
using UnityEngine;

namespace UnityTools.Editor
{
    /// <summary>
    /// 스파인 에셋의 애니메이션·이벤트·스킨 이름을 콘솔에 찍는다.
    ///
    /// 이름을 모르면 코드를 못 쓴다 — 애니메이션 이름 한 글자가 틀리면 조용히 재생이 안 되고,
    /// 이벤트 이름은 스파인 편집기를 열어야만 보인다. 에이전트 브릿지의 <c>spine &lt;에셋 경로&gt;</c>로 부른다.
    ///
    /// 이 어셈블리는 spine-unity 패키지가 있을 때만 컴파일된다(asmdef의 defineConstraints).
    /// 없는 프로젝트에서는 통째로 빠지므로 코어는 스파인을 몰라도 된다.
    /// </summary>
    [InitializeOnLoad]
    public static class SpineInfoLogger
    {
        static SpineInfoLogger()
        {
            AgentBridge.Register("spine", (argument, report) =>
            {
                Dump(argument);
                return false;
            });

            // 스파인은 에디터에서 한 번 초기화해야 메시가 생긴다 — 안 하면 미리보기가 빈 그림으로 나온다.
            PrefabPreviewCapture.PreRender.Add(instance =>
            {
                foreach (var spine in instance.GetComponentsInChildren<SkeletonAnimation>(true))
                {
                    if (spine.skeletonDataAsset == null) continue;

                    spine.Initialize(true);
                    spine.LateUpdate();
                }
            });
        }

        public static void Dump(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(path);

            if (asset == null)
            {
                Debug.LogError($"[스파인 정보] 에셋을 찾지 못했습니다: {path}");
                return;
            }

            var data = asset.GetSkeletonData(true);

            if (data == null)
            {
                Debug.LogError($"[스파인 정보] 스켈레톤을 읽지 못했습니다: {path}");
                return;
            }

            var report = new StringBuilder();

            report.AppendLine($"[스파인 정보] {asset.name}  (크기 {data.Width:0.##} × {data.Height:0.##})");

            report.AppendLine("  스킨: " + string.Join(", ", data.Skins.Select(s => s.Name)));
            report.AppendLine("  이벤트: " + (data.Events.Count == 0
                ? "(없음)"
                : string.Join(", ", data.Events.Select(e => e.Name))));

            report.AppendLine("  애니메이션:");

            foreach (var animation in data.Animations)
            {
                var events = animation.Timelines.OfType<Spine.EventTimeline>().ToList();

                report.Append($"    {animation.Name} : {animation.Duration:0.###}s");

                foreach (var timeline in events)
                {
                    foreach (var e in timeline.Events)
                    {
                        report.Append($"  [이벤트 {e.Data.Name} @ {e.Time:0.###}s]");
                    }
                }

                report.AppendLine();
            }

            Debug.Log(report.ToString());
        }
    }
}
