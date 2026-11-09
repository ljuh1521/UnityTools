using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEditor;

namespace UnityTools.Editor
{
    /// <summary>
    /// 에디터가 멈춘 동안 그 순간 떠 있는 창을 <c>Logs/Agent/blocked.txt</c>에 적는다.
    ///
    /// status.txt 는 에디터가 깨어 있을 때만 갱신되므로 멈추면 같이 멈추는데, 밖에서는 그게 확인창 때문인지
    /// 큰 임포트 때문인지 가를 수 없었다 — 2026-11-09 DefenceR 에서 밖에서 고친 씬을 다시 불러올지 묻는 창이 떠
    /// 사람이 누를 때까지 배치가 멈췄고, 기다리는 쪽은 "에디터 꺼짐 / 큰 임포트"로만 안내했다.
    /// 그래서 에디터 대신 따로 도는 스레드가 지켜본다. status.txt 에 섞지 않는 건 그 파일의 수정 시각이
    /// "에디터가 깨어 있다"는 신호라서다. 에디터가 다시 깨어나면 지운다.
    /// </summary>
    [InitializeOnLoad]
    internal static class StallWatch
    {
        // 상태 갱신이 0.5초 간격이고, 창이 뒤로 가 있으면 유니티가 갱신을 늦춘다.
        private const double StallSeconds = 5;

        private static readonly string BlockedFile = Path.GetFullPath("Logs/Agent/blocked.txt");
        private static readonly ManualResetEvent Stopping = new(false);
        private static readonly Thread Watcher;

        // 멈춘 길이는 시계 보정·절전 복귀에 흔들리지 않는 Stopwatch 로 재고, 사람이 읽을 시각만 따로 둔다.
        private static long _beatStamp = Stopwatch.GetTimestamp();
        private static long _beatTicks = DateTime.Now.Ticks;
        private static readonly uint Self = (uint)Process.GetCurrentProcess().Id;
        private static volatile bool _watching;
        private static volatile bool _written;

        static StallWatch()
        {
            // 지난 실행이 멈춘 채 꺼졌으면 그 기록이 남아 있다.
            _written = File.Exists(BlockedFile);

            // 도메인을 내리기 전에 세운다 — 옛 코드의 스레드가 남아 돌면 리로드가 그걸 기다리거나 둘이 겹친다.
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;

            Watcher = new Thread(Run) { IsBackground = true, Name = "UnityTools.StallWatch" };
            Watcher.Start();
        }

        /// <summary>에디터가 깨어날 때마다 부른다. 브릿지가 꺼져 있으면 감시도 쉰다.</summary>
        public static void Beat(bool enabled)
        {
            Interlocked.Exchange(ref _beatStamp, Stopwatch.GetTimestamp());
            Interlocked.Exchange(ref _beatTicks, DateTime.Now.Ticks);
            _watching = enabled;

            if (!_written) return;

            _written = false;

            try { File.Delete(BlockedFile); }
            catch (IOException) { _written = true; }
            catch (UnauthorizedAccessException) { _written = true; }
        }

        private static void Stop()
        {
            Stopping.Set();
            Watcher.Join(2000);
        }

        private static void Run()
        {
            while (!Stopping.WaitOne(1000))
            {
                if (!_watching) continue;

                double stalled = (double)(Stopwatch.GetTimestamp() - Interlocked.Read(ref _beatStamp)) / Stopwatch.Frequency;

                if (stalled < StallSeconds) continue;

                // 이 스레드에서 난 예외는 아무도 받지 않는다. 한 번 못 적어도 다음 바퀴에 다시 적는다.
                try
                {
                    Write(new DateTime(Interlocked.Read(ref _beatTicks)));
                    _written = true;
                }
                catch (Exception)
                {
                    // 적는 중 에디터가 깨어나 지웠거나 파일이 잠긴 것이다.
                }
            }
        }

        private static void Write(DateTime since)
        {
            var text = new StringBuilder();

            text.Append("since: ").Append(Stamp(since)).Append('\n');
            text.Append("checked: ").Append(Stamp(DateTime.Now)).Append('\n');

            List<(string Class, string Title, bool Enabled)> windows = Windows();

            if (windows == null)
            {
                text.Append("modal: unknown\n");
            }
            else
            {
                // 확인창은 주인 창(유니티 본창)을 눌리지 않게 꺼 둔다. 본창이 꺼져 있으면 무언가 응답을 기다리는 중이다.
                bool modal = windows.Exists(w => w.Class == MainClass && !w.Enabled);

                text.Append("modal: ").Append(modal ? "yes" : "no").Append('\n');

                foreach (var w in windows)
                    text.Append("window: ").Append(w.Class).Append(" | ").Append(w.Title).Append('\n');
            }

            Directory.CreateDirectory(Path.GetDirectoryName(BlockedFile)!);
            File.WriteAllText(BlockedFile, text.ToString(), Encoding.UTF8);
        }

        private static string Stamp(DateTime time) =>
            time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        private const string MainClass = "UnityContainerWndClass";

#if UNITY_EDITOR_WIN
        /// <summary>이 프로세스의 보이는 최상위 창. 창 제목은 메시지를 보내지 않는 쪽으로 읽는다 —
        /// 보내는 쪽(GetWindowText)은 창 주인인 멈춘 에디터가 답할 때까지 이 스레드도 같이 멈춘다.</summary>
        private static List<(string Class, string Title, bool Enabled)> Windows()
        {
            var found = new List<(string, string, bool)>();
            var buffer = new StringBuilder(512);

            EnumWindows((handle, _) =>
            {
                GetWindowThreadProcessId(handle, out uint owner);

                if (owner != Self || !IsWindowVisible(handle)) return true;

                buffer.Clear();
                GetClassName(handle, buffer, buffer.Capacity);
                string name = buffer.ToString();

                buffer.Clear();
                InternalGetWindowText(handle, buffer, buffer.Capacity);

                found.Add((name, buffer.ToString(), IsWindowEnabled(handle)));
                return true;
            }, IntPtr.Zero);

            return found;
        }

        private delegate bool EnumWindowsProc(IntPtr handle, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr handle);

        [DllImport("user32.dll")]
        private static extern bool IsWindowEnabled(IntPtr handle);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr handle, StringBuilder name, int capacity);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int InternalGetWindowText(IntPtr handle, StringBuilder text, int capacity);
#else
        private static List<(string Class, string Title, bool Enabled)> Windows() => null;
#endif
    }
}
