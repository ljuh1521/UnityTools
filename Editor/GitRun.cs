using System;

namespace UnityTools.Editor
{
    /// <summary>
    /// git 을 불러 표준 출력을 받는다. <b>제한 시간을 실제로 지킨다.</b>
    ///
    /// 검사들이 편집기 주 흐름에서 도므로 git 이 멎으면 편집기 전체가 응답 없음이 된다. 예전에는
    /// 두 검사가 각자 git 을 불렀는데 둘 다 <b>출력을 끝까지 읽은 뒤에</b> 시간을 재기 시작해서,
    /// git 이 멎으면 제한 시간이 영영 안 걸렸다 — 오류 출력도 넘겨받기만 하고 안 읽어 버퍼가
    /// 차면 서로 기다리다 멎을 수 있었다(2026-10-07 코드 검토). 같은 실수가 두 군데 있었던 건
    /// 같은 코드가 두 군데 있었기 때문이라 여기 하나로 모은다.
    /// </summary>
    internal static class GitRun
    {
        /// <returns>성공하면 true. 실패하면 <paramref name="failure"/>에 사람이 읽을 이유를 담는다 —
        /// 부르는 쪽은 그걸 "못 봤다"로 적어야 한다(빈 결과를 "깨끗하다"로 읽지 않게).</returns>
        public static bool Run(string arguments, string workingDirectory, int timeoutMs,
            out string output, out string failure)
        {
            output = null;
            failure = null;

            try
            {
                var start = new System.Diagnostics.ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = workingDirectory ?? string.Empty,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                // 아이디·비밀번호를 물으며 멎지 않게 한다. 사람을 기다리는 검사는 검사가 아니다.
                start.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";

                using var process = System.Diagnostics.Process.Start(start);

                if (process == null)
                {
                    failure = "git을 실행하지 못했습니다";
                    return false;
                }

                // 읽기는 따로 돌리고 시간은 프로세스 쪽에 건다. 오류 출력도 같이 비워야 버퍼가 차서
                // 서로 기다리는 일이 없다.
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();

                if (!process.WaitForExit(timeoutMs))
                {
                    try { process.Kill(); } catch { /* 이미 끝났으면 그만이다 */ }

                    failure = $"{timeoutMs / 1000}초 안에 답이 없었습니다";
                    return false;
                }

                output = stdout.Result;

                if (process.ExitCode != 0)
                {
                    string why = stderr.Result.Trim();
                    int newline = why.IndexOf('\n');

                    if (newline >= 0) why = why.Substring(0, newline).Trim();

                    failure = why.Length > 0
                        ? $"git이 실패했습니다 — {why}"
                        : $"git이 실패했습니다(종료 코드 {process.ExitCode})";

                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                failure = e.Message;
                return false;
            }
        }
    }
}
