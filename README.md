# Unity Tools

유니티 프로젝트마다 다시 만들게 되는 에디터 도구를 모은 개인 패키지.
**스택에 의존하지 않는 것만** 넣는다 — 특정 게임의 스키마·데이터 파이프라인은 프로젝트에 남긴다.

## 설치

`Packages/manifest.json`에 한 줄 넣는다.

```json
"com.ljuh.unitytools": "https://github.com/ljuh1521/UnityTools.git"
```

로컬 소스로 붙이려면(같은 드라이브에 나란히 둔 경우):

```json
"com.ljuh.unitytools": "file:../../UnityTools"
```

## 들어 있는 것

### 에이전트 브릿지 — `Tools/유니티 툴즈/에이전트 브릿지`

에디터 밖에서 넣은 명령을 실행하고 결과를 파일로 돌려준다.
**사람이 메뉴를 눌러주고 콘솔을 옮겨 적을 필요가 없어진다.**

기본은 꺼져 있다. 메뉴로 켠다(파일로 메뉴를 실행하는 기능이라 모르는 새 돌면 안 된다).

| 파일 | 뜻 |
|---|---|
| `Logs/Agent/request.txt` | 넣을 명령. 한 줄에 하나. 실행되면 지워진다 |
| `Logs/Agent/response.txt` | 실행 로그 + 콘솔 전체 + 컴파일 오류. 끝나면 `# 완료` |
| `Logs/Agent/status.txt` | 살아 있는지 알리는 시각(켜져 있을 때만 갱신) |

명령:

| 명령 | 하는 일 |
|---|---|
| `refresh` | 에셋 재임포트. **C#을 고쳤으면 반드시 먼저.** 컴파일 뒤 남은 명령을 이어서 실행 |
| `menu <메뉴 경로>` | 메뉴 항목 실행 |
| `call <네임스페이스.타입.메서드> [문자열]` | 정적 메서드 직접 호출. 메뉴를 늘리지 않고 생성기·검사에 닿는다 |
| `capture <프리팹 에셋 경로>` | 프리팹을 렌더해 `Logs/UIPreview`에 저장 |
| `play` / `stop` | 플레이 모드 |
| `wait <초>` | 다음 명령까지 대기(로딩·연출) |
| `shot <이름>` | 지금 게임 화면을 `Logs/PlayCapture`에 저장(플레이 중에만) |
| `spine <에셋 경로>` | 스파인의 애니메이션·이벤트·스킨 이름을 콘솔에 (spine-unity 있을 때만) |

명령을 더 붙이려면:

```csharp
[InitializeOnLoad]
public static class MyCommands
{
    static MyCommands()
    {
        // 핸들러가 true를 돌려주면 컴파일·시간 대기 후 남은 명령을 이어서 실행한다.
        AgentBridge.Register("mycmd", (argument, report) => { ...; return false; });
    }
}
```

**한계:** 실제 터치·드래그 입력은 재현하지 못한다. 플레이 모드를 검증하려면
**상태를 만드는 정적 메서드를 프로젝트에 두고** `call`로 부른 뒤 `shot`으로 찍는다.
키 입력도 마찬가지 — 키를 읽는 곳과 처리하는 곳을 나눠 두면 처리 쪽을 `call`로 부를 수 있다.

**주의:** 방금 만든 메뉴 항목은 `ExecuteMenuItem`이 못 찾는 경우가 있다(같은 경로가 이전에 서브메뉴였을 때).
그럴 땐 `call`을 쓴다.

### 프리팹 미리보기 — `Tools/유니티 툴즈/프리팹 미리보기 저장`

프리팹을 렌더해 `Logs/UIPreview/<이름>.png`로 저장한다. UI(RectTransform)는 월드 캔버스에 얹어
1픽셀 = 1유닛으로, 월드 오브젝트는 렌더러 경계를 재서 찍는다.

프로젝트에서 배경색·기준 해상도를 바꿀 수 있다:

```csharp
PrefabPreviewCapture.Background = new Color(0.42f, 0.62f, 0.28f, 1f);
PrefabPreviewCapture.ReferenceResolution = new Vector2(1080, 1920);
```

에디터에서 한 번 초기화해야 메시가 생기는 런타임이 있으면 렌더 전 훅을 더한다
(스파인은 이미 들어 있다):

```csharp
PrefabPreviewCapture.PreRender.Add(instance => { ... });
```

### 프리팹 지킴이 — `PrefabKeeper`

생성기가 프리팹을 매번 새로 조립해 덮어쓰면, 인스펙터에서 손본 위치·색이 다음 생성에 사라진다.
그러면 사람이 손댈 수 없는 물건이 된다. **세 벌을 비교해** 손질만 이어받는다.

    기준선(지난번 생성값) ─ 지금 프리팹 값 ─ 이번 생성값

지금 값이 기준선과 다르면 사람이 고친 것이라 지키고, 같으면 이번 생성값을 쓴다.
사람이 **직접 추가한 오브젝트**도 함께 옮겨 온다.

생성기의 저장 부분을 이렇게 감싼다:

```csharp
var generated = PrefabKeeper.Capture(root);      // 이번 생성값

// 기준선이 없으면 false — 저장하지 말고 멈춘다.
if (!PrefabKeeper.TryRestore(root, path, generated))
{
    Object.DestroyImmediate(root);
    return;
}

var saving = PrefabKeeper.Capture(root);         // 저장 직전 값
PrefabUtility.SaveAsPrefabAsset(root, path);
Object.DestroyImmediate(root);

PrefabKeeper.WriteBaseline(path, generated, saving);
```

**이미 있는 프리팹인데 기준선이 없으면 만들지 않고 멈춘다.** 그 상태에서는 무엇이 손질인지 가릴 수가
없어, 저장하는 순간 다듬은 값이 조용히 사라진다(예전에는 경고만 내고 덮어썼다 — 실제로 팝업 두 개를
그렇게 날렸다). 지금 프리팹을 버리고 새로 만들 생각이면 **그 프리팹을 지우고** 다시 돌린다.

기준선은 `PrefabKeeper.Root`(기본 `ProjectSettings/PrefabBaseline`) 아래 텍스트로 남는다 —
에셋이 아니라 `.meta`가 안 생긴다. **이미 기준선이 쌓인 프로젝트는 그 경로를 그대로 넣는다.**

`WriteBaseline`은 저장 전후로 값이 달라지는 속성을 찾아 경고한다. 레이아웃이 계산하는 자리·크기처럼
사람이 정한 값이 아닌 것을 추적하면 다음 생성에서 전부 "손질"로 오인하기 때문이다
(실제로 21개를 잘못 붙잡은 적이 있다). 경고가 나오면 추적 목록(`Fields`)에서 뺀다.

### 텍스처 프리셋 — `TexturePresetTools`

```csharp
TexturePresetTools.ApplyToFolder("Assets/Presets/TexturePreset.preset", "Assets/Textures", "텍스처");
TexturePresetTools.FixSpineMaterials("Assets/Sources/Spine");
```

프리셋은 임포터의 **모든** 값을 덮어쓴다. 그래서 스프라이트 시트로 잘라 둔 조각, 9슬라이스 테두리,
피벗, PPU는 되돌릴 수 없게 날아간다 — 그런 텍스처는 건너뛰고 목록으로 알린다.
메뉴는 폴더·프리셋 경로를 아는 프로젝트 쪽에 둔다.

### 검사 모음 — `Tools/유니티 툴즈/검사`

검사 메서드에 속성만 붙이면 한 메뉴에서 전부 돌아간다. 인자 없는 정적 메서드여야 한다.

```csharp
[EditorValidator("UI 프리팹 검사", 10)]
public static void Validate() { ... }
```

검사마다 메뉴를 두면 "어느 걸 돌리지"가 되어 결국 아무도 안 돌린다. 하나가 예외로 죽어도
나머지는 계속 돌린다 — 첫 검사가 막힌 동안 뒤가 잠들면 안 된다.

## 선택 의존

`Editor/Spine/`은 `spine-unity`가, `Editor/UI/`는 `com.unity.ugui`가 있을 때만 컴파일된다
(asmdef `versionDefines` + `defineConstraints`). 없는 프로젝트에서는 어셈블리째 빠지므로
코어는 그것들을 몰라도 된다. 다른 선택 의존도 같은 방식으로 더한다.

## 넣을 것과 안 넣을 것

넣는다 — 어떤 유니티 프로젝트에서도 참인 것(에디터 자동화, 프리팹·텍스처 취급, 생성기 골격).

안 넣는다 — 특정 게임의 데이터 스키마, 특정 백엔드·시뮬레이션 SDK에 묶인 것, 특정 UI 체계 전용 생성기.
**두 번째 프로젝트가 실제로 필요로 할 때 옮긴다.** 표본이 하나일 때 일반화하면 틀린 추상이 된다.
