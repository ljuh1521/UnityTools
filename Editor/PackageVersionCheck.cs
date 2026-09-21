using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace UnityTools.Editor
{
    /// <summary>
    /// <b>지금 물고 있는 패키지 판</b>을 찍고, 깃으로 물었으면 원격 최신과 대조한다.
    ///
    /// 깃 주소로 물면 유니티가 한 번 받아서 <c>Library/PackageCache</c>에 넣고 그 판에 **못 박는다**.
    /// 그래서 패키지를 고쳐 푸시해도 프로젝트는 옛 사본을 계속 쓰는데, <b>오류도 경고도 안 난다</b> —
    /// 고친 게 반영 안 된 줄 모른 채 "왜 안 고쳐졌지"를 한참 헤맨다(2026-09-18에 로컬 참조에서 같은
    /// 형태로 당해 <see cref="AgentBridge"/>에 경고를 넣었는데, 깃으로 바꾸자 그 경고가 조용해지면서
    /// 같은 구멍이 모양만 바꿔 돌아왔다).
    ///
    /// <b>못 물어봤을 때를 "최신"과 갈라 적는다.</b> 인터넷이나 git이 없을 때 아무 말도 안 하면
    /// 이 검사도 "조용해서 괜찮아 보이는" 물건이 된다 — 오늘 온종일 당한 형태가 그것이다.
    /// </summary>
    public static class PackageVersionCheck
    {
        private const string Tag = "[패키지 판 검사]";

        /// <summary>유니티가 해석 결과를 적어 두는 곳. 프로젝트 기준 경로다.</summary>
        private const string LockFile = "Packages/packages-lock.json";

        private const int RemoteTimeoutMs = 5000;

        [EditorValidator("패키지 판 검사", 4)]
        public static void Validate()
        {
            var info = PackageInfo.FindForAssembly(typeof(PackageVersionCheck).Assembly);

            if (info == null) return;   // 패키지가 아니라 프로젝트 안에 그대로 들어 있다

            if (!File.Exists(LockFile))
            {
                Debug.LogWarning($"{Tag} {LockFile}을 못 찾아 어느 판을 물었는지 확인하지 못했습니다. " +
                                 "**최신이라는 뜻이 아닙니다.**");
                return;
            }

            var entry = Entry(File.ReadAllText(LockFile), info.name);

            if (entry == null)
            {
                Debug.LogWarning($"{Tag} {LockFile}에 {info.name} 항목이 없습니다 — 어느 판을 물었는지 " +
                                 "확인하지 못했습니다. **최신이라는 뜻이 아닙니다.**");
                return;
            }

            string source = Field(entry, "source");
            string reference = Field(entry, "version");
            string hash = Field(entry, "hash");

            if (source != "git")
            {
                // 로컬(file:) 참조는 폴더를 그대로 쓰므로 뒤처질 일이 없다. 대신 그쪽은 "고쳐 놓고
                // 컴파일을 안 했다"가 함정이라 AgentBridge의 WarnIfStale이 맡는다.
                Debug.Log($"{Tag} {info.name} {info.version} — {source ?? "?"} 참조라 뒤처질 일이 없습니다.");
                return;
            }

            // **락이 말하는 것과 실제로 받아 둔 것은 다를 수 있다.** 2026-09-21에 DefenceR이
            // 실제로 그랬다 — manifest만 깃으로 고치고 락은 "local"인 채라, 유니티는 계속 옛 판을
            // 쓰는데 락만 읽는 쪽은 그 어긋남을 못 본다. 유니티는 **실제로 받아 둔 것**을 쓰므로
            // "지금 무슨 코드가 도나"의 답은 그쪽이다.
            string installed = InstalledHash(info.resolvedPath);

            // 락에 해시가 없는 것도 어긋남이다 — 실제로는 받아 둔 게 있는데 락이 그걸 안 적고 있다.
            if (installed != null &&
                (string.IsNullOrEmpty(hash) || !hash.StartsWith(installed, StringComparison.OrdinalIgnoreCase)))
            {
                // 나란히 읽는 줄이라 **자릿수를 맞춘다** — 한쪽만 길면 눈으로 대조가 안 된다.
                Debug.LogWarning($"{Tag} 락과 실제가 다릅니다 — 락에는 {Short(hash)}라 적혀 있는데 " +
                                 $"실제로 받아 둔 것은 {Short(installed)}입니다. **도는 것은 실제 쪽입니다.**");
            }

            // 뒤처짐 판정의 기준은 실제로 받아 둔 것이다. 그걸 못 읽었으면 락으로 대신하되
            // **대신했다고 적는다** — 무엇을 근거로 말하는지가 보여야 한다.
            string basis = installed ?? hash;
            string basisNote = installed != null ? "" : " (락 기준 — 받아 둔 것을 못 읽었습니다)";

            string loaded = $"{info.name} {info.version} · {Short(basis)}{basisNote}";

            // `URL#태그`로 판을 못 박았으면 뒤처진 게 아니라 **그렇게 정한 것**이다. 원격 최신과
            // 다르다고 경고하면 그때부터 매번 헛경보가 뜬다.
            int pin = reference?.IndexOf('#') ?? -1;

            if (pin >= 0)
            {
                Debug.Log($"{Tag} {loaded} — {reference.Substring(pin + 1)}에 못 박혀 있습니다. " +
                          "원격 최신과는 대조하지 않습니다.");
                return;
            }

            string url = reference;

            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(basis))
            {
                Debug.LogWarning($"{Tag} {loaded} — 주소나 해시가 없어 대조하지 못했습니다. " +
                                 "**최신이라는 뜻이 아닙니다.**");
                return;
            }

            string remote = RemoteHead(url, out string failure);

            if (remote == null)
            {
                Debug.LogWarning($"{Tag} {loaded} — 원격을 확인하지 못했습니다({failure}). " +
                                 "**최신이라는 뜻이 아니라 모른다는 뜻입니다.**");
                return;
            }

            // 받아 둔 쪽은 12자리라 앞부분만 맞춰 본다.
            if (remote.StartsWith(basis, StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"{Tag} {loaded} — 원격 최신과 같습니다.");
                return;
            }

            Debug.LogWarning($"{Tag} {loaded} — **뒤처져 있습니다.** 원격 최신은 {Short(remote)}입니다.\n" +
                             $"  받으려면 {LockFile}의 hash를 지우거나 원격 값으로 바꾼 뒤 패키지를 다시 풀어야 " +
                             "합니다(에셋 재임포트만으로는 안 됩니다).");
        }

        /// <summary>락 파일에서 그 패키지 항목의 본문만 잘라낸다. 못 찾으면 null.</summary>
        private static string Entry(string json, string package)
        {
            int at = json.IndexOf($"\"{package}\"", StringComparison.Ordinal);

            if (at < 0) return null;

            int open = json.IndexOf('{', at);

            if (open < 0) return null;

            // 항목 안에 중첩 객체(dependencies)가 있어 첫 `}`로 자르면 안 된다.
            int depth = 0;

            for (int i = open; i < json.Length; i++)
            {
                if (json[i] == '{') depth++;
                else if (json[i] == '}' && --depth == 0) return json.Substring(open, i - open + 1);
            }

            return null;
        }

        private static string Field(string entry, string name)
        {
            var match = Regex.Match(entry, $"\"{name}\"\\s*:\\s*\"([^\"]*)\"");

            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// <b>실제로 받아 둔</b> 판의 해시. 유니티는 받은 것을
        /// <c>Library/PackageCache/&lt;이름&gt;@&lt;해시 앞 12자리&gt;</c>에 풀어 두므로 폴더 이름에서 읽는다.
        ///
        /// 유니티 기능 이름을 짐작하지 않으려고 이 방법을 쓴다 — 폴더 이름은 눈으로 확인한 것이고
        /// (2026-09-21에 `abb0619` · `f0abbeb` · `e45ac8c` 세 번 다 이 규칙이었다), 지어낸 API 이름은
        /// 컴파일 전에는 맞는지 알 수 없다.
        ///
        /// 모양이 다르면 <b>지어내지 말고 null</b>을 준다 — 부르는 쪽이 "못 읽었다"고 적는다.
        /// </summary>
        private static string InstalledHash(string resolvedPath)
        {
            if (string.IsNullOrEmpty(resolvedPath)) return null;

            string folder = Path.GetFileName(resolvedPath.TrimEnd('\\', '/'));
            int at = folder.LastIndexOf('@');

            if (at < 0) return null;

            string tail = folder.Substring(at + 1);

            return Regex.IsMatch(tail, "^[0-9a-f]{7,40}$") ? tail : null;
        }

        private static string Short(string hash) =>
            string.IsNullOrEmpty(hash) ? "(해시 없음)" : hash.Substring(0, Math.Min(7, hash.Length));

        /// <summary>원격 기본 가지의 최신 커밋. 못 물어봤으면 null과 이유를 준다.</summary>
        private static string RemoteHead(string url, out string failure)
        {
            failure = null;

            try
            {
                var start = new System.Diagnostics.ProcessStartInfo("git", $"ls-remote \"{url}\" HEAD")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                // 비공개 저장소면 git이 아이디·비밀번호를 물으며 멎는다. 물어보지 말고 실패하게 한다 —
                // 검사가 사람을 기다리는 물건이 되면 안 된다.
                start.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";

                using var process = System.Diagnostics.Process.Start(start);

                if (process == null)
                {
                    failure = "git을 실행하지 못했습니다";
                    return null;
                }

                string output = process.StandardOutput.ReadToEnd();

                if (!process.WaitForExit(RemoteTimeoutMs))
                {
                    // 자격 증명 창을 기다리느라 멎는 경우가 있다. 검사가 거기 매달리면 안 된다.
                    try { process.Kill(); } catch { /* 이미 끝났으면 그만이다 */ }

                    failure = $"{RemoteTimeoutMs / 1000}초 안에 답이 없었습니다";
                    return null;
                }

                if (process.ExitCode != 0)
                {
                    failure = "git ls-remote가 실패했습니다(오프라인이거나 접근 권한이 없습니다)";
                    return null;
                }

                var match = Regex.Match(output, @"^([0-9a-f]{40})\s");

                if (!match.Success)
                {
                    failure = "git 답에서 해시를 못 읽었습니다";
                    return null;
                }

                return match.Groups[1].Value;
            }
            catch (Exception e)
            {
                failure = e.Message;
                return null;
            }
        }
    }
}
