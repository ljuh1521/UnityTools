using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace UnityTools.Editor
{
    /// <summary>
    /// 에디터 밖에서 넣은 명령을 받아 실행하고 결과를 파일로 돌려준다.
    ///
    /// 코드를 고치는 쪽(작업자)이 결과를 직접 확인하려면 <b>메뉴 실행 → 콘솔 확인 → 그림 확인</b>이
    /// 필요한데, 이건 전부 에디터 안에서만 된다. 그래서 사람이 매번 메뉴를 눌러주고 화면을 옮겨 적어야 했다.
    /// 이 브릿지를 놓으면 그 왕복이 사라진다 — 명령을 파일로 넣으면 에디터가 알아서 돌리고,
    /// 콘솔 로그와 미리보기 그림을 파일로 남긴다.
    ///
    /// <b>기본은 꺼져 있다.</b> Tools/유니티 툴즈/에이전트 브릿지 로 켜고 끈다 —
    /// 파일로 메뉴를 실행하는 기능이라 모르는 새 돌아가면 안 된다.
    ///
    /// 주고받는 파일(전부 git 무시 폴더):
    ///   Logs/Agent/request.txt   넣을 명령. 한 줄에 하나.
    ///   Logs/Agent/response.txt  실행 결과와 그동안의 콘솔 로그.
    ///   Logs/Agent/status.txt    살아 있는지 알리는 시각(켜져 있을 때만 갱신).
    ///
    /// 명령:
    ///   refresh              에셋 다시 읽기(코드를 고쳤으면 이걸로 컴파일시킨다)
    ///   menu &lt;메뉴 경로&gt;      메뉴 항목 실행
    ///   call &lt;네임스페이스.타입.메서드&gt; [문자열]  메뉴에 없는 생성기·검사를 직접 호출(정적)
    ///   capture &lt;에셋 경로&gt;   프리팹을 렌더해 Logs/UIPreview에 저장
    ///   play / stop          플레이 모드 시작·정지
    ///   wait &lt;초&gt;            다음 명령까지 기다린다(로딩·연출을 넘길 때)
    ///   shot &lt;이름&gt;          지금 게임 화면을 Logs/PlayCapture에 저장(플레이 모드 전용)
    ///
    /// 프로젝트·선택 패키지가 <see cref="Register"/>로 명령을 더 붙일 수 있다.
    /// </summary>
    [InitializeOnLoad]
    public static class AgentBridge
    {
        private const string EnabledKey = "UnityTools.AgentBridge.Enabled";
        private const string PendingKey = "UnityTools.AgentBridge.Pending";
        private const string WaitKey = "UnityTools.AgentBridge.WaitUntil";

        private const string Root = "Logs/Agent";
        private const string RequestFile = Root + "/request.txt";
        private const string ResponseFile = Root + "/response.txt";
        private const string StatusFile = Root + "/status.txt";

        private const string ShotRoot = "Logs/PlayCapture";

        // 너무 자주 파일을 두드리지 않는다.
        private const double PollSeconds = 0.5;

        private static double _nextPoll;
        private static readonly List<string> Captured = new();
        private static bool _collecting;

        /// <summary>추가 명령. 키는 명령 이름, 값은 (인자, 보고서) → 기다려야 하면 true.</summary>
        private static readonly Dictionary<string, Func<string, StringBuilder, bool>> Extra = new();

        static AgentBridge()
        {
            EditorApplication.update += Poll;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
        }

        /// <summary>
        /// 명령을 하나 더 붙인다. 선택 의존(스파인 등)이나 프로젝트 전용 명령을 코어를 고치지 않고 끼우는 자리다.
        /// <c>[InitializeOnLoad]</c> 정적 생성자에서 부른다. 핸들러는 컴파일·시간 대기가 필요하면 true를 돌려준다.
        /// </summary>
        public static void Register(string command, Func<string, StringBuilder, bool> handler)
        {
            if (string.IsNullOrEmpty(command) || handler == null) return;

            Extra[command] = handler;
        }

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, false);
            set => EditorPrefs.SetBool(EnabledKey, value);
        }

        /// <summary>컴파일 때문에 미뤄 둔 명령. 도메인 리로드를 넘겨야 해서 EditorPrefs에 둔다.</summary>
        private static string Pending
        {
            get => EditorPrefs.GetString(PendingKey, string.Empty);
            set => EditorPrefs.SetString(PendingKey, value);
        }

        /// <summary>이 시각까지는 다음 명령을 미룬다. 로딩·연출을 기다릴 때 쓴다.</summary>
        private static double WaitUntil
        {
            get => double.TryParse(EditorPrefs.GetString(WaitKey, "0"),
                NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0;
            set => EditorPrefs.SetString(WaitKey, value.ToString(CultureInfo.InvariantCulture));
        }

        private const string ToggleMenu = UnityToolsMenu.Root + "에이전트 브릿지";

        [MenuItem(ToggleMenu, false, 80)]
        private static void Toggle()
        {
            Enabled = !Enabled;
            Pending = string.Empty;

            if (!Enabled)
            {
                Debug.Log("[에이전트 브릿지] 껐습니다.");
                return;
            }

            Directory.CreateDirectory(Root);

            Debug.Log($"[에이전트 브릿지] 켰습니다. {RequestFile}에 명령을 넣으면 실행하고 " +
                      $"{ResponseFile}에 결과를 남깁니다.");
        }

        [MenuItem(ToggleMenu, true)]
        private static bool ToggleCheck()
        {
            Menu.SetChecked(ToggleMenu, Enabled);

            return true;
        }

        private static void Poll()
        {
            if (!Enabled) return;
            if (EditorApplication.timeSinceStartup < _nextPoll) return;

            _nextPoll = EditorApplication.timeSinceStartup + PollSeconds;

            // 깨어난 것 자체를 먼저 적는다. 아래 관문 뒤에 적으면 컴파일·임포트가 길어질 때
            // 갱신이 멈춰 밖에서는 브릿지가 죽은 것처럼 보인다 — 실제로 그렇게 헷갈렸다.
            WriteStatus();

            // 컴파일·임포트·플레이 전환 중에는 명령을 돌리면 안 된다. 끝나면 다음 차례에 이어서 한다.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying) return;

            // 기다리는 중이면 아직 손대지 않는다.
            if (EditorApplication.timeSinceStartup < WaitUntil) return;

            string commands = Pending;
            bool continued = !string.IsNullOrEmpty(commands);

            if (!continued)
            {
                if (!File.Exists(RequestFile)) return;

                commands = File.ReadAllText(RequestFile);
                File.Delete(RequestFile);
            }

            Pending = string.Empty;
            WaitUntil = 0;

            // 기다렸다 이어서 도는 경우엔 앞선 기록을 지우지 않는다 — 지우면 무엇을 했는지 사라진다.
            Run(commands, continued);
        }

        private static void Run(string commands, bool append)
        {
            var report = new StringBuilder();

            report.AppendLine(append ? "# 이어서 " + Stamp() : "# 실행 " + Stamp());

            Captured.Clear();
            _collecting = true;
            Application.logMessageReceived += Collect;

            try
            {
                var lines = commands.Split('\n');

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();

                    if (line.Length == 0 || line.StartsWith("#")) continue;

                    report.AppendLine($"> {line}");

                    if (!Execute(line, report)) continue;

                    // refresh·play는 도메인 리로드를, wait·shot은 시간을 기다린다. 남은 명령은 이어서 돌린다.
                    // 남은 게 없어도 빈 줄 하나를 남긴다 — 그래야 기다림이 끝난 뒤 '완료'가 찍힌다.
                    string rest = string.Join("\n", lines, i + 1, lines.Length - i - 1).Trim();

                    Pending = rest.Length > 0 ? rest : "#";

                    Flush(report, "대기 중 — 끝나면 남은 명령을 이어서 실행합니다.", append);
                    return;
                }
            }
            catch (Exception e)
            {
                report.AppendLine("!! " + e);
            }
            finally
            {
                Application.logMessageReceived -= Collect;
                _collecting = false;
            }

            Flush(report, "완료", append);
        }

        /// <summary>명령 하나를 실행한다. 컴파일을 기다려야 하면 true.</summary>
        private static bool Execute(string line, StringBuilder report)
        {
            int space = line.IndexOf(' ');
            string command = space < 0 ? line : line.Substring(0, space);
            string argument = space < 0 ? string.Empty : line.Substring(space + 1).Trim();

            switch (command)
            {
                case "refresh":
                    // 플레이 중에는 유니티가 컴파일을 미룬다. 그 상태로 새로 읽히면 브릿지가 멈춘 것처럼 보인다
                    // (실제로 여러 번 그랬다). 먼저 플레이를 끄고, 꺼진 다음에 이어서 읽는다.
                    if (EditorApplication.isPlaying)
                    {
                        report.AppendLine("   플레이 중이라 먼저 정지합니다.");

                        EditorApplication.isPlaying = false;

                        Pending = line + Environment.NewLine + Pending;

                        return true;
                    }

                    AssetDatabase.Refresh();
                    return true;

                case "menu":
                    if (!EditorApplication.ExecuteMenuItem(argument))
                        report.AppendLine($"!! 메뉴를 찾지 못했습니다: {argument}");
                    return false;

                case "call":
                    CallMethod(argument, report);
                    return false;

                case "play":
                    if (EditorApplication.isPlaying) return false;

                    EditorApplication.isPlaying = true;
                    return true;

                case "stop":
                    if (!EditorApplication.isPlaying) return false;

                    EditorApplication.isPlaying = false;
                    return true;

                case "wait":
                    WaitUntil = EditorApplication.timeSinceStartup + ParseSeconds(argument);
                    return true;

                case "shot":
                    return Shot(argument, report);

                case "capture":
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(argument);

                    if (prefab == null) report.AppendLine($"!! 프리팹을 찾지 못했습니다: {argument}");
                    else report.AppendLine("   " + PrefabPreviewCapture.Capture(prefab));

                    return false;

                default:
                    if (Extra.TryGetValue(command, out var handler)) return handler(argument, report);

                    report.AppendLine($"!! 모르는 명령: {command}");
                    return false;
            }
        }

        /// <summary>
        /// 지금 게임 화면을 파일로 남긴다. 플레이 모드에서만 뜻이 있다.
        /// 실제 기록은 프레임 끝에 일어나므로 잠깐 기다렸다 다음 명령으로 넘어간다.
        /// </summary>
        private static bool Shot(string name, StringBuilder report)
        {
            if (!EditorApplication.isPlaying)
            {
                report.AppendLine("!! 플레이 모드가 아닙니다. play 먼저 넣으세요.");
                return false;
            }

            Directory.CreateDirectory(ShotRoot);

            string file = $"{ShotRoot}/{(string.IsNullOrEmpty(name) ? "shot" : name)}.png";

            if (File.Exists(file)) File.Delete(file);

            ScreenCapture.CaptureScreenshot(file);
            report.AppendLine("   " + file);

            WaitUntil = EditorApplication.timeSinceStartup + 1.5;

            return true;
        }

        private static double ParseSeconds(string argument)
        {
            return double.TryParse(argument, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                ? Math.Clamp(value, 0, 120)
                : 1;
        }

        /// <summary>메뉴에 없는 생성기·검사를 이름으로 부른다. 메뉴를 늘리지 않고도 전부 닿을 수 있다.</summary>
        private static void CallMethod(string command, StringBuilder report)
        {
            // "타입.메서드 인자" — 인자는 하나까지, 문자열로만 넘긴다.
            int space = command.IndexOf(' ');

            string target = space < 0 ? command : command.Substring(0, space);
            string argument = space < 0 ? null : command.Substring(space + 1).Trim();

            int dot = target.LastIndexOf('.');

            if (dot < 0)
            {
                report.AppendLine($"!! 형식이 틀렸습니다(네임스페이스.타입.메서드): {target}");
                return;
            }

            string typeName = target.Substring(0, dot);
            string methodName = target.Substring(dot + 1);

            var type = FindType(typeName);

            var method = type?.GetMethod(methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            if (method == null)
            {
                report.AppendLine($"!! 찾지 못했습니다: {target}");
                return;
            }

            var parameters = method.GetParameters();

            if (parameters.Length > 1 || (parameters.Length == 1 && parameters[0].ParameterType != typeof(string)))
            {
                report.AppendLine($"!! 무인자이거나 문자열 하나를 받는 메서드만 부를 수 있습니다: {target}");
                return;
            }

            if (parameters.Length == 1 && argument == null)
            {
                report.AppendLine($"!! 인자가 필요합니다: {target} <문자열>");
                return;
            }

            method.Invoke(null, parameters.Length == 1 ? new object[] { argument } : null);
        }

        /// <summary>
        /// 어셈블리 이름을 몰라도 타입을 찾는다. asmdef를 쓰는 프로젝트는 Assembly-CSharp이 아니라
        /// 제 이름의 어셈블리로 들어가므로, 흔한 두 곳을 먼저 보고 없으면 전부 훑는다.
        /// </summary>
        private static Type FindType(string typeName)
        {
            var type = Type.GetType($"{typeName}, Assembly-CSharp-Editor")
                       ?? Type.GetType($"{typeName}, Assembly-CSharp");

            if (type != null) return type;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName, false);

                if (type != null) return type;
            }

            return null;
        }

        private static void Collect(string condition, string stackTrace, LogType type)
        {
            if (!_collecting) return;

            // 스택은 길기만 하고 대부분 쓸모없다. 오류일 때만 첫 줄을 붙인다.
            string head = type == LogType.Log ? "" : $"[{type}] ";

            Captured.Add(head + condition);
        }

        private static void OnAssemblyCompiled(string assembly, CompilerMessage[] messages)
        {
            if (!Enabled) return;

            var errors = new List<string>();

            foreach (var message in messages)
            {
                if (message.type != CompilerMessageType.Error) continue;

                errors.Add($"{message.file}({message.line}): {message.message}");
            }

            if (errors.Count == 0) return;

            Directory.CreateDirectory(Root);
            File.AppendAllText(ResponseFile,
                $"\n# 컴파일 오류 {Stamp()}\n  " + string.Join("\n  ", errors) + "\n");
        }

        private static void Flush(StringBuilder report, string tail, bool append)
        {
            if (Captured.Count > 0)
            {
                report.AppendLine();
                report.AppendLine("# 콘솔");

                foreach (string log in Captured) report.AppendLine("  " + log);
            }

            report.AppendLine();
            report.AppendLine("# " + tail);

            Directory.CreateDirectory(Root);

            if (append) File.AppendAllText(ResponseFile, report.ToString(), Encoding.UTF8);
            else File.WriteAllText(ResponseFile, report.ToString(), Encoding.UTF8);
        }

        private static void WriteStatus()
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(StatusFile, Stamp(), Encoding.UTF8);
        }

        private static string Stamp()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
    }
}
