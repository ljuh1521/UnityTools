using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// UnityEditor에도 PackageInfo가 따로 있어 그냥 쓰면 모호하다.
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace UnityTools.Editor
{
    /// <summary>
    /// 이 패키지가 <b>붙인 프로젝트의 자산에 기대고 있지 않은지</b> 스스로 본다.
    ///
    /// 패키지가 프로젝트 것을 물면 다른 프로젝트에서 그 기능이 죽는데, <b>오류가 안 난다</b> —
    /// <see cref="Resources.Load"/>는 못 찾으면 그냥 null을 주고, 그 null을 쓰는 쪽은 대개
    /// 조용히 아무것도 안 그린다.
    ///
    /// 그리고 이건 <b>어떤 검사에도 안 걸린다.</b> 경로가 문자열이라 컴파일러가 안 보고,
    /// 타입 참조를 보는 검사도 프리팹 guid를 보는 검사도 문자열은 안 본다.
    /// 2026-09-18에 실제로 그래서 <c>OutlineUI</c>·<c>ShadowUI</c>가 프로젝트 머티리얼 둘을
    /// 붙든 채로 이관 검증을 전부 통과했다 — 우연히 발견했지 잡아낸 게 아니다.
    ///
    /// 사람이 기억해야만 발동하는 규칙은 안 지켜지므로 검사로 만들어 둔다.
    /// </summary>
    public static class PackageSelfCheck
    {
        private static readonly Regex ResourcesLoad =
            new(@"Resources\.Load(?:All)?\s*(?:<[^>]+>)?\s*\(\s*""([^""]+)""", RegexOptions.Compiled);

        private static readonly Regex ShaderFind =
            new(@"Shader\.Find\s*\(\s*""([^""]+)""", RegexOptions.Compiled);

        private static readonly Regex ShaderName =
            new(@"^\s*Shader\s+""([^""]+)""", RegexOptions.Compiled | RegexOptions.Multiline);

        [EditorValidator("패키지 자립 검사", 5)]
        public static void Validate()
        {
            var info = PackageInfo.FindForAssembly(typeof(PackageSelfCheck).Assembly);

            if (info == null)
            {
                // 패키지가 아니라 프로젝트 안에 그대로 들어 있는 경우다. 그러면 검사할 것이 없다.
                return;
            }

            string root = info.resolvedPath;

            var sources = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            var problems = new List<string>();
            int looked = 0;

            // 이 패키지가 실제로 갖고 있는 Resources 자산과 셰이더 이름
            var owned = OwnedResources(root);
            var shaders = OwnedShaderNames(root);

            foreach (string file in sources)
            {
                string name = Path.GetFileName(file);

                foreach (string line in File.ReadAllLines(file))
                {
                    // 주석에 적어 둔 예시까지 세면 없는 문제를 만든다.
                    string code = Strip(line);

                    foreach (Match m in ResourcesLoad.Matches(code))
                    {
                        looked++;

                        if (owned.Contains(m.Groups[1].Value.Replace('\\', '/'))) continue;

                        problems.Add($"{name}: Resources.Load(\"{m.Groups[1].Value}\") — " +
                                     "이 패키지 안에 없습니다. 붙인 프로젝트에 있으면 거기서만 됩니다.");
                    }

                    foreach (Match m in ShaderFind.Matches(code))
                    {
                        looked++;

                        if (shaders.Contains(m.Groups[1].Value)) continue;

                        problems.Add($"{name}: Shader.Find(\"{m.Groups[1].Value}\") — " +
                                     "이 패키지 안에 없습니다. 유니티 내장 셰이더면 무시해도 됩니다.");
                    }
                }
            }

            if (looked == 0)
            {
                // 0건은 "깨끗하다"가 아니라 "안 봤다"일 수 있다 — 소스를 못 찾았으면 그렇게 말한다.
                Debug.LogWarning($"[패키지 자립 검사] {info.name}에서 문자열로 자산을 찾는 자리를 " +
                                 $"하나도 못 찾았습니다(소스 {sources.Length}개). 경로나 표기가 바뀌었는지 보세요.");
                return;
            }

            if (problems.Count == 0)
            {
                Debug.Log($"[패키지 자립 검사] 이상 없음 ({info.name}, 문자열 조회 {looked}건).");
                return;
            }

            Debug.LogWarning($"[패키지 자립 검사] 패키지 밖을 가리키는 자리 {problems.Count}건입니다" +
                             $"(전체 {looked}건). 다른 프로젝트에 붙이면 오류 없이 그 기능만 죽습니다.\n  " +
                             string.Join("\n  ", problems));
        }

        /// <summary>패키지 안 <c>Resources/</c>가 담고 있는 것. <c>Resources.Load</c>가 쓰는 표기로 모은다.</summary>
        private static HashSet<string> OwnedResources(string root)
        {
            var set = new HashSet<string>();

            foreach (string dir in Directory.GetDirectories(root, "Resources", SearchOption.AllDirectories))
            {
                foreach (string file in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories))
                {
                    if (file.EndsWith(".meta")) continue;

                    string rel = file.Substring(dir.Length + 1).Replace('\\', '/');

                    // 확장자를 뗀 것이 Resources.Load에 넘기는 이름이다.
                    set.Add(Path.ChangeExtension(rel, null));
                }
            }

            return set;
        }

        private static HashSet<string> OwnedShaderNames(string root)
        {
            var set = new HashSet<string>();

            foreach (string file in Directory.GetFiles(root, "*.shader", SearchOption.AllDirectories))
            {
                var m = ShaderName.Match(File.ReadAllText(file));

                if (m.Success) set.Add(m.Groups[1].Value);
            }

            return set;
        }

        /// <summary>줄에서 주석을 잘라낸다 — 문자열 안의 <c>//</c>는 주석이 아니다.</summary>
        private static string Strip(string line)
        {
            bool inString = false;

            for (int i = 0; i < line.Length - 1; i++)
            {
                if (line[i] == '"' && (i == 0 || line[i - 1] != '\\')) inString = !inString;
                else if (!inString && line[i] == '/' && line[i + 1] == '/') return line.Substring(0, i);
            }

            return line;
        }
    }
}
