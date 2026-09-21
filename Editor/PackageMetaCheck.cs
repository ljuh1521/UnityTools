using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace UnityTools.Editor
{
    /// <summary>
    /// 패키지 안의 파일·폴더마다 짝이 되는 <c>.meta</c>가 있는지, 그리고 그게 <b>저장소에 올라가</b>
    /// 있는지 본다.
    ///
    /// <c>.meta</c>가 빠지면 유니티는 그 파일을 <b>아예 없는 것처럼</b> 다룬다 — 컴파일에도 안 들어가고
    /// 오류도 경고도 안 난다. 2026-09-21에 새 검사 파일을 올렸는데 짝이 없어서, 받은 쪽에서 "검사가
    /// 안 나타난다"로 한참 헤맸다. <b>증상이 "없는 것처럼 굼"이라 만든 쪽은 끝까지 모른다.</b>
    ///
    /// 두 가지를 갈라 본다. 함정이 서로 다르다:
    /// <list type="bullet">
    /// <item>패키지 폴더를 <b>유니티가 안 보고 있으면</b>(패키지 저장소만 있고 프로젝트가 없으면)
    /// 짝이 아예 안 만들어진다 — 2026-09-21에 당한 것이 이쪽이다.</item>
    /// <item>유니티가 보고 있으면 짝은 만들어지는데, 이번엔 <b>커밋에서 빠진다</b> — 만든 쪽 디스크에는
    /// 있으니 아무 이상이 없어 보이고, 받는 쪽에서만 없다.</item>
    /// </list>
    /// </summary>
    public static class PackageMetaCheck
    {
        private const string Tag = "[패키지 짝 검사]";

        [EditorValidator("패키지 짝 검사", 3)]
        public static void Validate()
        {
            var info = PackageInfo.FindForAssembly(typeof(PackageMetaCheck).Assembly);

            if (info == null) return;   // 패키지가 아니라 프로젝트 안에 그대로 들어 있다

            string root = info.resolvedPath;
            var missing = new List<string>();
            int looked = 0;

            Walk(new DirectoryInfo(root), root, missing, ref looked);

            if (looked == 0)
            {
                // 0건은 "깨끗하다"가 아니라 "못 봤다"다 — 경로가 바뀌면 조용히 이렇게 된다.
                Debug.LogWarning($"{Tag} {root}에서 볼 것을 하나도 못 찾았습니다. 경로를 확인하세요.");
                return;
            }

            var untracked = Untracked(root, out string gitNote);

            if (missing.Count == 0 && untracked.Count == 0)
            {
                Debug.Log($"{Tag} 이상 없음 ({looked}자리 확인{gitNote}).");
                return;
            }

            var lines = new List<string>();

            if (missing.Count > 0)
            {
                lines.Add($"짝이 없습니다 {missing.Count}개 — 유니티가 이 파일들을 없는 것처럼 다룹니다:");
                lines.AddRange(missing.Select(m => "    " + m));
            }

            if (untracked.Count > 0)
            {
                lines.Add($"저장소에 안 올라갔습니다 {untracked.Count}개 — 내 디스크에만 있어 " +
                          "받는 쪽에서는 없습니다:");
                lines.AddRange(untracked.Select(u => "    " + u));
            }

            Debug.LogWarning($"{Tag} {looked}자리 중 문제 {missing.Count + untracked.Count}개.\n  " +
                             string.Join("\n  ", lines));
        }

        /// <summary>유니티가 무시하는 이름. 이걸 안 빼면 문서 폴더 등이 전부 헛경보로 뜬다.</summary>
        private static bool Ignored(string name) =>
            name.StartsWith(".", StringComparison.Ordinal) ||
            name.EndsWith("~", StringComparison.Ordinal) ||
            name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "cvs", StringComparison.OrdinalIgnoreCase);

        private static void Walk(DirectoryInfo dir, string root, List<string> missing, ref int looked)
        {
            foreach (var file in dir.GetFiles())
            {
                if (file.Name.EndsWith(".meta", StringComparison.Ordinal) || Ignored(file.Name)) continue;

                looked++;

                if (!File.Exists(file.FullName + ".meta")) missing.Add(Relative(file.FullName, root));
            }

            foreach (var child in dir.GetDirectories())
            {
                if (Ignored(child.Name)) continue;

                looked++;

                if (!File.Exists(child.FullName + ".meta")) missing.Add(Relative(child.FullName, root) + "/");

                Walk(child, root, missing, ref looked);
            }
        }

        private static string Relative(string path, string root) =>
            path.Substring(root.Length).TrimStart('\\', '/').Replace("\\", "/");

        /// <summary>
        /// 저장소에 안 올라간 것들. 패키지 폴더가 저장소가 아니면(받아 둔 사본이면) 볼 수 없으므로
        /// <paramref name="note"/>에 그렇게 적는다 — <b>조용히 빈손으로 돌아오면 "깨끗하다"로 읽힌다.</b>
        /// </summary>
        private static List<string> Untracked(string root, out string note)
        {
            var found = new List<string>();

            if (!Directory.Exists(Path.Combine(root, ".git")))
            {
                note = " · 저장소가 아니라 커밋 여부는 못 봤습니다";
                return found;
            }

            try
            {
                var start = new System.Diagnostics.ProcessStartInfo("git", "status --porcelain --untracked-files=all")
                {
                    WorkingDirectory = root,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var process = System.Diagnostics.Process.Start(start);

                if (process == null)
                {
                    note = " · git을 실행하지 못해 커밋 여부는 못 봤습니다";
                    return found;
                }

                string output = process.StandardOutput.ReadToEnd();

                if (!process.WaitForExit(5000))
                {
                    try { process.Kill(); } catch { /* 이미 끝났으면 그만이다 */ }

                    note = " · git이 답하지 않아 커밋 여부는 못 봤습니다";
                    return found;
                }

                foreach (string line in output.Split('\n'))
                {
                    // `?? 경로` — 추적되지 않는 것만 본다. 고쳐 놓고 커밋 안 한 것(` M`)은 여기 관심사가
                    // 아니다(그건 사람이 아는 상태다). 없는 파일이 조용히 빠지는 것만 잡는다.
                    if (!line.StartsWith("?? ", StringComparison.Ordinal)) continue;

                    string path = line.Substring(3).Trim().Trim('"');

                    found.Add(path);
                }

                note = "";

                return found;
            }
            catch (Exception e)
            {
                note = $" · 커밋 여부는 못 봤습니다({e.Message})";
                return found;
            }
        }
    }
}
