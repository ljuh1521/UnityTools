# Unity Tools

유니티 프로젝트마다 다시 만들게 되는 것을 모은 개인 패키지.
**스택에 의존하지 않는 것만** 넣는다 — 특정 게임의 스키마·데이터 파이프라인은 프로젝트에 남긴다.

안에 성격이 다른 두 갈래가 있다.

- **`Editor/`** — **작업자가 화면을 볼 수 없다는 전제**에서 나온 도구. 메뉴를 누르고 콘솔을 읽고
  화면을 보는 일을 파일 왕복으로 대신한다. 사람이 편집기 앞에 앉아 있으면 대부분 필요 없다.
- **`Runtime/`** — 빌드에도 들어가는, **프로젝트를 안 가리는 재사용 조각.** 게임 기능이라
  위 전제와는 무관하다.

무엇을 넣을지 헷갈리면 이 둘 중 어느 쪽인지부터 정한다. 어느 쪽도 아니면 프로젝트에 남긴다.

## 설치

`Packages/manifest.json`에 한 줄 넣는다.

```json
"com.ljuh.unitytools": "https://github.com/ljuh1521/UnityTools.git"
```

로컬 소스로 붙이려면(같은 드라이브에 나란히 둔 경우):

```json
"com.ljuh.unitytools": "file:../../UnityTools"
```

## Editor

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

### 프리팹 인스턴스 진단 — `PrefabInstanceInfo`

지금 만지는 오브젝트가 **중첩 프리팹 인스턴스**인지, 원본이 어디인지 알려준다.

```
call UnityTools.Editor.PrefabInstanceInfo.Dump Assets/Prefabs/UI/Foo.prefab|Grid/Card
```

프리팹 에셋 경로와 그 안쪽 오브젝트 경로를 `|`로 잇는다(`call`은 문자열 인자가 하나뿐이라 이렇게 합친다).
안쪽 경로를 비우면 루트 자체를 본다.

중첩 인스턴스에 건 값은 **바깥 프리팹 파일 안의 오버라이드로만** 저장되고 원본 에셋은 안 바뀐다.
스프라이트·색상·크기처럼 자리마다 다른 게 정상인 값은 인스턴스만 고쳐도 되지만, 컴포넌트 종류·
머티리얼·레이캐스트 설정처럼 **그 부품 자체의 문제**를 고칠 때 원본을 빠뜨리면 다른 데서 새로
인스턴스화할 때 그대로 재발한다. 그 실수를 실제로 한 번 하고 나서 만들었다.

## Runtime

지금까지는 에디터 전용이었는데 0.4.0부터 **빌드에도 들어가는 코드**가 들어 있다.

### UI 아웃라인 — `UI/Outline` 셰이더 + `OutlineWidthModifier`

사각형이 아닌 실루엣(아치·별 모양 등)에도 **두께가 고른** UI 테두리.

흔한 방법인 "같은 스프라이트를 뒤에 하나 더 깔고 사각형 틀 안쪽으로 밀어넣기"는 실루엣이 사각형이
아니면 두께가 고르지 않게 나온다. 이 셰이더는 대신 원본 알파의 가장자리를 따라 링을 직접 그려서,
모양이 복잡해도 두께가 일정하다. 9슬라이스(보더) 스프라이트도 지원한다 — 모서리는 안 늘리고
가운데만 늘리는 렌더링과 같은 방식으로 UV를 구간별로 나눠 매핑한다.

쓰는 법: 이 셰이더를 **뒤 레이어**에 씌우고, 그 위에 같은 스프라이트를 인셋 없이 그대로 그리는
**앞 레이어**를 겹친다. 뒤 레이어에서 원본 밖으로 삐져나온 링만 보이게 된다.

폭·보더 값은 `OutlineWidthModifier.SetWidth`로 넘긴다. 값을 머티리얼 프로퍼티가 아니라 정점의
uv 채널에 실어 보내므로, **인스턴스마다 값이 달라도 머티리얼 하나를 공유**할 수 있다.
uv2/uv3는 Canvas의 Additional Shader Channels를 켜야 실제로 메쉬에 실리는데, 이 컴포넌트가
필요할 때 스스로 켠다(꺼져 있으면 값이 조용히 버려진다).

## 선택 의존

`Editor/Spine/`은 `spine-unity`가, `Editor/UI/`와 `Runtime/UI/`는 `com.unity.ugui`가 있을 때만
컴파일된다(asmdef `versionDefines` + `defineConstraints`). 없는 프로젝트에서는 어셈블리째 빠지므로
코어는 그것들을 몰라도 된다. 다른 선택 의존도 같은 방식으로 더한다.

## 넣을 것과 안 넣을 것

넣는다 — 위 두 갈래 중 하나에 확실히 들어맞고, 어떤 유니티 프로젝트에서도 참인 것.
`Editor/`는 작업 왕복을 없애는 것(에디터 자동화, 프리팹·텍스처 취급, 생성기 골격),
`Runtime/`은 프로젝트를 안 가리는 재사용 조각.

안 넣는다 — 특정 게임의 데이터 스키마, 특정 백엔드·시뮬레이션 SDK에 묶인 것, 특정 UI 체계 전용 생성기.
**두 번째 프로젝트가 실제로 필요로 할 때 옮긴다.** 표본이 하나일 때 일반화하면 틀린 추상이 된다.

`Runtime/`이 서넛으로 늘면 그때 별도 패키지로 나눌지 판단한다 — 지금은 하나뿐이라 쪼갤 근거가 없다.
