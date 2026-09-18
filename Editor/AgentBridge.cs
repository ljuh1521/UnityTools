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
    ///   refresh              에셋 다시 읽기(코드를 고쳤으면 이걸로 컴파일시킨다).
    ///                        컴파일이 실제로 걸렸는지 다음 줄에 보고한다.
    ///   recompile            변경 감지를 건너뛰고 컴파일을 직접 요청한다(느리다).
    ///                        refresh가 "컴파일이 안 걸렸습니다"라고 했는데 고친 게 분명할 때 쓴다.
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

        // refresh·recompile이 컴파일을 실제로 걸었는지 세는 자리. 도메인 리로드를 넘겨야 해서 EditorPrefs에 둔다.
        private const string WatchKey = "UnityTools.AgentBridge.WatchCompile";
        private const string CompiledKey = "UnityTools.AgentBridge.CompiledCount";
        private const string SeenKey = "UnityTools.AgentBridge.CompileSeen";
        private const string IdKey = "UnityTools.AgentBridge.BatchId";
        private const string DeadlineKey = "UnityTools.AgentBridge.CompileDeadline";
        private const string ActiveKey = "UnityTools.AgentBridge.BatchActive";

        // 컴파일을 부탁한 뒤 유니티가 실제로 시작하기까지 걸리는 시간을 봐 준다. 이만큼은 "안 걸렸다"고
        // 판정하지 않는다 — 시작 전에 판정하면 멀쩡히 도는 컴파일을 무동작으로 잘못 보고한다.
        private const double RefreshGrace = 2;

        // recompile은 요청을 큐에 넣고 나중 틱에서 처리하므로 더 길게 본다.
        private const double RecompileGrace = 15;

        private const string Root = "Logs/Agent";
        private const string RequestFile = Root + "/request.txt";
        private const string ResponseFile = Root + "/response.txt";
        private const string StatusFile = Root + "/status.txt";

        private const string ShotRoot = "Logs/PlayCapture";

        // 너무 자주 파일을 두드리지 않는다.
        private const double PollSeconds = 0.5;

        private static double _nextPoll;
        // 콘솔은 메모리가 아니라 파일에 쌓는다. 한 배치 안에서도 refresh·play는 도메인 리로드를
        // 일으켜 정적 필드를 날려 버리기 때문이다 — 예전에는 Run 한 번의 범위에서만 받아서
        // **play가 도는 동안 나온 로그를 아무도 못 받았다.** 그 배치의 응답에는 `# 콘솔` 절 자체가
        // 안 붙었고, 그래서 빈 결과가 "경고가 없다"가 아니라 "안 봤다"가 됐다(2026-09-18).
        private const string ConsoleFile = Root + "/console.txt";

        // 플레이가 길면 로그가 끝없이 쌓인다. 이만큼에서 멈추고 끊겼다는 것을 응답에 적는다.
        private const long ConsoleLimit = 256 * 1024;

        /// <summary>
        /// 이 명령은 지금 못 하니 조건이 풀린 뒤 <b>자기부터 다시</b> 돌려야 한다 — 그렇게 표시해 둔 줄.
        /// <see cref="Run"/>이 남은 명령 앞에 도로 붙인다.
        ///
        /// 예전에는 <see cref="Execute"/>가 <see cref="Pending"/>에 직접 끼워 넣었는데, 돌아온 자리에서
        /// Run이 그 값을 남은 명령으로 덮어써서 <b>그 줄이 조용히 사라졌다</b> — 플레이 중에 refresh를
        /// 보내면 플레이만 멈추고 컴파일은 안 되면서 응답은 "완료"로 끝났다(2026-09-18 코드 검토).
        /// </summary>
        private static string _repeatLine;

        /// <summary>추가 명령. 키는 명령 이름, 값은 (인자, 보고서) → 기다려야 하면 true.</summary>
        private static readonly Dictionary<string, Func<string, StringBuilder, bool>> Extra = new();

        static AgentBridge()
        {
            EditorApplication.update += Poll;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
            CompilationPipeline.compilationStarted += OnCompilationStarted;

            // 콘솔은 늘 듣고 있는다. 배치가 도는 동안만 실제로 적는다(<see cref="Collect"/>).
            // 도메인 리로드 뒤에도 여기가 다시 돌아 붙으므로 play 중의 로그가 안 빠진다.
            Application.logMessageReceived += Collect;
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

        /// <summary>
        /// 컴파일을 걸어 달라고 부탁한 뒤 결과를 보고해야 하는 상태인가.
        /// <c>refresh</c>·<c>recompile</c>이 켜고, 보고하면서 끈다.
        /// </summary>
        private static bool WatchingCompile
        {
            get => EditorPrefs.GetBool(WatchKey, false);
            set => EditorPrefs.SetBool(WatchKey, value);
        }

        /// <summary>지켜보기 시작한 뒤 실제로 다시 만들어진 어셈블리 수.</summary>
        private static int CompiledCount
        {
            get => EditorPrefs.GetInt(CompiledKey, 0);
            set => EditorPrefs.SetInt(CompiledKey, value);
        }

        /// <summary>
        /// 컴파일이 실제로 돌았는가. <b>판정은 이 값으로 한다</b> — 어셈블리 개수로 하면 안 된다.
        ///
        /// 어셈블리별 완료 이벤트(<c>assemblyCompilationFinished</c>)는 유니티가 빌드 캐시로 결과를
        /// 채울 때 안 뜬다. 2026-09-15 DefenceR 실측: Csc가 203개 돌고 어셈블리를 다시 로드했는데도
        /// 개수가 0이었다(로그에는 compile time=2 ms, CacheWrite 다수). 그래서 "돌았는가"는
        /// 전체 컴파일 시작 이벤트와 폴링 중 관측으로 따로 잡는다.
        /// </summary>
        private static bool CompileSeen
        {
            get => EditorPrefs.GetBool(SeenKey, false);
            set => EditorPrefs.SetBool(SeenKey, value);
        }

        /// <summary>
        /// 이번 배치를 부르는 이름. 넣는 쪽이 <c>id &lt;토큰&gt;</c>으로 정하고, 응답의 시작·끝 줄에 되돌려준다.
        ///
        /// 이게 없으면 <c>response.txt</c>가 <c># 완료</c>로 끝난 것을 보고도 <b>그게 방금 보낸 명령의
        /// 것인지 이전 것인지 알 수 없다.</b> 2026-09-17에 그것 때문에 이전 실행 결과를 읽고 잘못
        /// 보고한 일이 세 번 있었다(결과에 특정 단어가 있는지로 우회했는데, 그 단어가 이전 실행에도
        /// 있으면 그대로 속는다).
        /// </summary>
        private static string BatchId
        {
            get => EditorPrefs.GetString(IdKey, string.Empty);
            set => EditorPrefs.SetString(IdKey, value ?? string.Empty);
        }

        /// <summary>
        /// 배치가 도는 중인가. 이 동안에만 콘솔을 적는다 — 배치 밖에서 나는 로그까지 담으면
        /// 다음 배치의 응답에 남의 로그가 섞인다.
        /// </summary>
        private static bool BatchActive
        {
            get => EditorPrefs.GetBool(ActiveKey, false);
            set => EditorPrefs.SetBool(ActiveKey, value);
        }

        /// <summary>이 시각까지는 "컴파일이 안 걸렸다"고 판정하지 않는다.</summary>
        private static double CompileDeadline
        {
            get => double.TryParse(EditorPrefs.GetString(DeadlineKey, "0"),
                NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0;
            set => EditorPrefs.SetString(DeadlineKey, value.ToString(CultureInfo.InvariantCulture));
        }

        private const string ToggleMenu = UnityToolsMenu.Root + "에이전트 브릿지";

        [MenuItem(ToggleMenu, false, 80)]
        private static void Toggle()
        {
            Enabled = !Enabled;
            Pending = string.Empty;
            _repeatLine = null;

            // 미뤄 둔 명령을 버리면 그 명령이 켜 둔 컴파일 감시와 콘솔 수집도 같이 버려야 한다.
            ClearWatch();
            BatchActive = false;

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
            // 지나는 길에 컴파일을 봤다는 사실을 남긴다 — 이벤트가 캐시 경로에서 안 뜨는 경우의 보루다.
            if (EditorApplication.isCompiling)
            {
                if (WatchingCompile) CompileSeen = true;

                return;
            }

            if (EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying) return;

            // 기다리는 중이면 아직 손대지 않는다.
            if (EditorApplication.timeSinceStartup < WaitUntil) return;

            // 컴파일을 부탁해 놓고 아직 시작도 안 했으면 판정을 미룬다. 여기서 안 기다리면 요청 직후
            // 0.5초 만에 "안 걸렸습니다"가 나가서, 곧 시작될 컴파일을 무동작으로 잘못 보고한다
            // (2026-09-15 DefenceR 실측: recompile이 그렇게 두 번 무동작으로 보고됐다).
            if (WatchingCompile && !CompileSeen &&
                EditorApplication.timeSinceStartup < CompileDeadline) return;

            string commands = Pending;
            bool continued = !string.IsNullOrEmpty(commands);

            if (!continued)
            {
                if (!File.Exists(RequestFile)) return;

                commands = File.ReadAllText(RequestFile);
                File.Delete(RequestFile);

                BatchId = ExtractId(commands);

                // 이제부터 이 배치가 끝날 때까지 콘솔을 받는다. 앞 배치가 남긴 것은 버린다.
                BatchActive = true;
                if (File.Exists(ConsoleFile)) File.Delete(ConsoleFile);

                // 새 배치가 왔다는 건 앞 배치가 끝났다는 뜻이다. 그때까지 남아 있는 감시는
                // 주인이 없으므로 **보고하지 않고 버린다** — 안 그러면 이 배치의 첫 이어달리기에
                // 엉뚱한 컴파일 판정이 붙는다.
                if (WatchingCompile) ClearWatch();
            }

            Pending = string.Empty;
            WaitUntil = 0;

            // 기다렸다 이어서 도는 경우엔 앞선 기록을 지우지 않는다 — 지우면 무엇을 했는지 사라진다.
            Run(commands, continued);
        }

        private static void Run(string commands, bool append)
        {
            var report = new StringBuilder();

            report.AppendLine((append ? "# 이어서" : "# 실행") + Label() + " " + Stamp());

            if (append) ReportCompile(report);


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

                    // 조건이 안 돼서 못 돈 명령은 남은 것 앞에 도로 붙인다. 여기서 안 붙이면
                    // 아래 대입이 그 줄을 덮어써 통째로 사라진다.
                    if (_repeatLine != null)
                    {
                        rest = (_repeatLine + "\n" + rest).Trim();
                        _repeatLine = null;
                    }

                    Pending = rest.Length > 0 ? rest : "#";

                    Flush(report, "대기 중 — 끝나면 남은 명령을 이어서 실행합니다.", append);
                    return;
                }
            }
            catch (Exception e)
            {
                report.AppendLine("!! " + e);
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

                        _repeatLine = line;

                        return true;
                    }

                    WatchCompile(RefreshGrace);
                    AssetDatabase.Refresh();
                    return true;

                case "recompile":
                    // refresh는 유니티가 "바뀐 파일"을 스스로 찾아야 도는데, 밖에서 고친 .cs를 놓칠 때가 있다.
                    // 이건 refresh가 "컴파일 안 걸렸다"고 보고했는데 고친 게 분명할 때 쓰는 탈출구다.
                    //
                    // CleanBuildCache를 쓰는 이유는 "캐시된 빌드 결과를 모두 지워 전체 스크립트를 다시
                    // 빌드"가 문서상 보장이기 때문이다. 기본값(None)은 "바뀐 스크립트만"이라, 변경 감지가
                    // 실패한 상황 — 정확히 이 명령을 쓰는 상황 — 에서 무엇을 하는지 보장이 없다.
                    // 탈출구는 느려도 확실한 쪽이 맞다. 대신 전부 다시 만들어 수 분 걸린다.
                    //
                    // 주의: 컴파일이 돌았는지를 Library/ScriptAssemblies의 DLL 시각으로 재지 말 것.
                    // 유니티(Bee)는 Library/Bee/artifacts에 만들고 내용이 달라졌을 때만 저기로 복사하므로,
                    // 소스가 그대로면 다시 컴파일해도 시각이 안 변한다 — 2026-09-15에 그걸 근거로
                    // "이 명령이 무동작"이라는 틀린 결론을 냈다(실제로는 두 번 다 돌고 있었다).
                    if (EditorApplication.isPlaying)
                    {
                        report.AppendLine("   플레이 중이라 먼저 정지합니다.");

                        EditorApplication.isPlaying = false;

                        _repeatLine = line;

                        return true;
                    }

                    report.AppendLine("   전체를 다시 만듭니다 — 프로젝트에 따라 수 분 걸립니다.");

                    WatchCompile(RecompileGrace);
                    AssetDatabase.Refresh();
                    CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
                    return true;

                case "id":
                    // 배치를 읽기 전에 이미 뽑아 뒀다. 여기서는 넘긴다.
                    return false;

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

            if (type == null)
            {
                report.AppendLine($"!! 타입을 찾지 못했습니다: {typeName}");
                return;
            }

            // GetMethod(이름, 플래그)는 오버로드가 있으면 예외를 던진다(UnityEditor 쪽에 흔하다).
            // 이름으로 다 모은 뒤 부를 수 있는 모양만 고른다.
            MethodInfo noArgument = null;
            MethodInfo stringArgument = null;
            bool anyByName = false;

            foreach (var candidate in type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (candidate.Name != methodName) continue;

                anyByName = true;

                var signature = candidate.GetParameters();

                if (signature.Length == 0) noArgument ??= candidate;
                else if (signature.Length == 1 && signature[0].ParameterType == typeof(string))
                    stringArgument ??= candidate;
            }

            if (!anyByName)
            {
                report.AppendLine($"!! 찾지 못했습니다: {target}");
                return;
            }

            var method = argument != null ? stringArgument ?? noArgument : noArgument;

            if (method == null)
            {
                report.AppendLine(argument != null
                    ? $"!! 인자를 받는 판이 없습니다: {target}"
                    : $"!! 인자가 필요합니다: {target} <문자열>");
                return;
            }

            method.Invoke(null, method.GetParameters().Length == 1 ? new object[] { argument } : null);
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

        /// <summary>
        /// 콘솔 한 줄을 받는다. <b>배치가 도는 동안이면 파일에 적는다</b> — 메모리에 두면
        /// 도메인 리로드(refresh·play)에 날아가서 정작 플레이 중의 로그를 못 받는다.
        /// </summary>
        private static void Collect(string condition, string stackTrace, LogType type)
        {
            if (!Enabled || !BatchActive) return;

            try
            {
                // 너무 쌓이면 멈춘다. 끊겼다는 것은 Flush가 파일 크기를 보고 응답에 적는다.
                var file = new FileInfo(ConsoleFile);

                if (file.Exists && file.Length >= ConsoleLimit) return;

                // 스택은 길기만 하고 대부분 쓸모없다. 오류일 때만 종류를 앞에 붙인다.
                string head = type == LogType.Log ? "" : $"[{type}] ";

                Directory.CreateDirectory(Root);
                File.AppendAllText(ConsoleFile, head + condition + Environment.NewLine, Encoding.UTF8);
            }
            catch (IOException)
            {
                // 로그를 적다 실패했다고 브릿지가 멈추면 안 된다. 그 줄만 버린다.
            }
        }

        /// <summary>이제부터 컴파일이 도는지 지켜본다. 컴파일을 거는 명령이 부른다.</summary>
        /// <param name="grace">유니티가 컴파일을 시작할 때까지 봐 줄 시간(초).</param>
        private static void WatchCompile(double grace)
        {
            WatchingCompile = true;
            CompiledCount = 0;
            CompileSeen = false;
            CompileDeadline = EditorApplication.timeSinceStartup + grace;
        }

        /// <summary>
        /// 지켜보기를 없던 일로 한다. <b>보고하지 않고 버릴 때</b> 쓴다 — 감시를 켠 배치가 끝나
        /// 버렸는데 상태만 남으면, 한참 뒤 엉뚱한 배치에 컴파일 판정이 붙는다(2026-09-18 코드 검토).
        /// </summary>
        private static void ClearWatch()
        {
            WatchingCompile = false;
            CompiledCount = 0;
            CompileSeen = false;
            CompileDeadline = 0;
        }

        /// <summary>
        /// 컴파일이 실제로 걸렸는지 보고한다.
        ///
        /// 이게 없으면 <c>refresh</c> 다음의 <c>call</c> 실패가 <b>오타인지 옛 어셈블리인지</b> 구분되지 않는다.
        /// 둘 다 "찾지 못했습니다"로 똑같이 나오기 때문이다 — 실제로 그것 때문에 하루에 세 번 헛돌았다
        /// (2026-09-15, DefenceR). 유니티가 밖에서 고친 .cs를 못 알아채는 일이 있어서 생기는 문제다.
        /// </summary>
        private static void ReportCompile(StringBuilder report)
        {
            if (!WatchingCompile) return;

            int count = CompiledCount;
            bool ran = CompileSeen;

            ClearWatch();

            if (ran)
            {
                // 개수는 알 수 있을 때만 붙인다 — 캐시로 채워지면 어셈블리별 이벤트가 안 떠서 0이다.
                report.AppendLine(count > 0
                    ? $"   컴파일 걸렸습니다 — 어셈블리 {count}개를 다시 만들었습니다."
                    : "   컴파일 걸렸습니다.");
                return;
            }

            report.AppendLine("   컴파일이 안 걸렸습니다 — 바뀐 스크립트를 찾지 못했습니다. " +
                              "방금 고친 코드인데 call이 \"찾지 못했습니다\"로 나오면 오타가 아니라 " +
                              "옛 어셈블리를 보고 있는 것이니 recompile을 넣으세요(전체를 다시 만들어 느립니다).");
        }

        private static void OnCompilationStarted(object context)
        {
            if (Enabled && WatchingCompile) CompileSeen = true;
        }

        private static void OnAssemblyCompiled(string assembly, CompilerMessage[] messages)
        {
            if (!Enabled) return;

            if (WatchingCompile)
            {
                CompiledCount++;
                CompileSeen = true;
            }

            var errors = new List<string>();

            foreach (var message in messages)
            {
                if (message.type != CompilerMessageType.Error) continue;

                errors.Add($"{message.file}({message.line}): {message.message}");
            }

            if (errors.Count == 0) return;

            var block = new StringBuilder();

            block.AppendLine();
            block.AppendLine($"# 컴파일 오류 {Stamp()}");

            foreach (string error in errors) block.AppendLine("  " + error);

            // 오류 블록이 '# 완료' 뒤에 붙으면 읽는 쪽은 배치가 안 끝난 것으로 본다. 오류를 고쳐도
            // 응답 파일은 그대로라 스스로 안 풀린다 — 2026-09-17에 그래서 브릿지가 한 번 잠겼고
            // 사용자가 로그를 지워야 했다. 이어서 돌 명령이 없으면 여기서 다시 닫는다.
            if (string.IsNullOrEmpty(Pending))
            {
                block.Append(Tail("완료"));
                BatchId = string.Empty;
                BatchActive = false;
            }

            Directory.CreateDirectory(Root);
            File.AppendAllText(ResponseFile, block.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// 그동안 쌓인 콘솔을 보고서에 옮기고 파일을 비운다. 이어달리기마다 비우므로
        /// 각 구간의 로그가 그 구간 응답에 붙는다 — 플레이 중에 난 것도 여기로 들어온다.
        /// </summary>
        private static void AppendConsole(StringBuilder report)
        {
            if (!File.Exists(ConsoleFile)) return;

            string text;

            try
            {
                var file = new FileInfo(ConsoleFile);
                bool cut = file.Length >= ConsoleLimit;

                text = File.ReadAllText(ConsoleFile, Encoding.UTF8);
                File.Delete(ConsoleFile);

                if (text.Length == 0) return;

                report.AppendLine();
                report.AppendLine("# 콘솔");

                foreach (string line in text.Split('\n'))
                {
                    string trimmed = line.TrimEnd('\r');

                    if (trimmed.Length > 0) report.AppendLine("  " + trimmed);
                }

                // 잘린 것을 말하지 않으면 "이게 전부"로 읽힌다.
                if (cut) report.AppendLine($"  … 로그가 {ConsoleLimit / 1024}KB를 넘어 이후는 안 받았습니다.");
            }
            catch (IOException)
            {
                report.AppendLine();
                report.AppendLine("# 콘솔 — 읽지 못했습니다(파일이 잠겨 있었습니다).");
            }
        }

        private static void Flush(StringBuilder report, string tail, bool append)
        {
            AppendConsole(report);

            report.AppendLine();
            report.Append(Tail(tail));

            Directory.CreateDirectory(Root);

            if (append) File.AppendAllText(ResponseFile, report.ToString(), Encoding.UTF8);
            else File.WriteAllText(ResponseFile, report.ToString(), Encoding.UTF8);

            // 배치가 끝났으면 이름을 놓는다. 들고 있으면 나중에 배치 밖에서 난 컴파일 오류가
            // 그 이름을 달고 완료 줄을 쓰고, 같은 이름을 다시 쓰는 쪽이 그 낡은 줄을 자기 것으로
            // 읽는다 — 이름을 붙인 이유가 바로 그걸 막으려는 것이었다(2026-09-18 코드 검토).
            if (tail != "완료") return;

            BatchId = string.Empty;

            // 배치가 끝났으니 콘솔도 그만 받는다 — 계속 받으면 다음 배치 응답에 남의 로그가 섞인다.
            BatchActive = false;
        }

        private static void WriteStatus()
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(StatusFile, Stamp(), Encoding.UTF8);
        }

        /// <summary>넣는 쪽이 <c>id &lt;토큰&gt;</c>으로 이번 배치 이름을 정한다. 없으면 빈 문자열.</summary>
        private static string ExtractId(string commands)
        {
            foreach (string raw in commands.Split('\n'))
            {
                string line = raw.Trim();

                if (!line.StartsWith("id ")) continue;

                // 대괄호가 섞이면 읽는 쪽이 줄을 가려낼 때 깨진다.
                return line.Substring(3).Trim().Replace("[", string.Empty).Replace("]", string.Empty);
            }

            return string.Empty;
        }

        private static string Label()
        {
            string id = BatchId;

            return id.Length == 0 ? string.Empty : " [" + id + "]";
        }

        /// <summary>배치 이름을 붙인 끝 줄. 읽는 쪽이 자기가 보낸 배치인지 가릴 수 있게 한다.</summary>
        private static string Tail(string text)
        {
            return "# " + text + Label() + Environment.NewLine;
        }

        private static string Stamp()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
    }
}
