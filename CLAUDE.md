# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 프로젝트 상태

Unity 6 URP 프로젝트. 게임플레이 코드는 1인칭 컨트롤러(`Assets/Scripts/Player/`)와 연구소 문(`Assets/Scripts/Environment/`)뿐이고, 게임 컨셉도 미정이다. `Assets/TutorialInfo/`와 `Assets/Readme.asset`은 템플릿 안내용이라 지워도 된다.

세계관은 전작 NOMANUAL의 Rhizome Solution(R.S)을 잇는다. 설정 문서는 상위 폴더의 `세계관_리좀솔루션.md`이고, 스토리·기획 작업 전에 읽는다.

## 폴더 구분

- `D:\04_Project Files\Roosevelt\Roosevelt\` (이 저장소): 개발 루트. 게임 코드와 에셋은 여기에만 둔다. origin은 `https://github.com/w8err/Roosevelt`, 브랜치는 `main`.
- `D:\04_Project Files\Roosevelt\` (상위 폴더): 기획서, 분석, 작업 메모 같은 문서를 자유롭게 두는 공간. git 밖이라 GitHub에 올라가지 않는다.

## 에디터 & 명령어

에디터: Unity `6000.6.0f1`, 경로는 `D:\02_Unity Files\6000.6.0f1\Editor\Unity.exe`. 같은 폴더에 2022.3.5f1도 설치되어 있지만 이 프로젝트에는 쓰지 않는다.

배치모드 명령은 에디터가 이 프로젝트를 열어 둔 상태면 실패한다(`Temp/UnityLockfile`). 에디터를 먼저 닫아야 한다.

에디터 로그는 `%LOCALAPPDATA%\Unity\Editor\Editor.log`가 아니라 프로젝트의 `Logs/Editor.log`에 쌓인다. 컴파일 에러(`error CS`)와 `Debug.Log` 출력은 이 파일에서 확인한다.

```powershell
$unity = "D:\02_Unity Files\6000.6.0f1\Editor\Unity.exe"
$proj  = "D:\04_Project Files\Roosevelt\Roosevelt"

# 컴파일 확인 (에러는 로그에서 "error CS" 검색)
& $unity -batchmode -nographics -quit -projectPath $proj -logFile "$proj\Logs\batch.log"

# 테스트 실행 (com.unity.test-framework): EditMode 또는 PlayMode
& $unity -batchmode -nographics -projectPath $proj -runTests -testPlatform EditMode `
  -testResults "$proj\Logs\results.xml" -logFile "$proj\Logs\test.log"

# 단일 테스트/클래스만 실행: -testFilter에 전체 이름(정규식 가능)
& $unity -batchmode -nographics -projectPath $proj -runTests -testPlatform EditMode `
  -testFilter "Namespace.ClassName.MethodName" -testResults "$proj\Logs\results.xml"
```

`-runTests`에는 `-quit`을 붙이지 않는다(테스트 종료 후 알아서 닫힌다). 테스트를 쓰려면 먼저 해당 폴더에 test assembly(`.asmdef`)가 있어야 한다. 아직 하나도 없다.

## 구성상 알아둘 점

- **입력:** `activeInputHandler: 1`이라 새 Input System만 켜져 있다. `UnityEngine.Input`(레거시 API)을 호출하면 런타임 예외가 난다. 액션 에셋은 `Assets/InputSystem_Actions.inputactions`이고 `Player`(Move/Look/Attack/Interact/Crouch/Jump/Previous/Next/Sprint)와 `UI` 맵이 있다.
- **렌더링:** URP 품질 티어가 두 개다. `Mobile`/`PC` 각각 RP asset과 renderer 한 쌍이 `Assets/Settings/`에 있고, 현재 기본은 `PC`(`m_CurrentQuality: 1`). 렌더 설정을 바꿀 때는 두 티어 모두 반영할지 확인한다.
- **빌드 씬:** `Assets/Scenes/SampleScene.unity` 하나뿐이다.
- **아트 텍스처:** `Assets/Art/` 아래 텍스처는 `Assets/Editor/ArtTexturePostprocessor.cs`가 임포트할 때마다 Point 필터·밉맵 끔·무압축으로 덮어쓴다. Inspector에서 바꿔도 재임포트하면 되돌아가므로 예외가 필요하면 스크립트를 고친다.
- **연구소 씬 빌더:** 메뉴 `Roosevelt > Lab > Build Layouts` 또는 `Temp/LabSceneBuilder.request` 파일 생성(에디터가 1초마다 확인, 열린 씬이 저장된 상태일 때만 실행)으로 `Assets/Editor/LabSceneBuilder.cs`가 `Assets/Art/Environment/{Lab,Forest}/Layouts/*.json`마다 `Assets/Scenes/<씬 이름>.unity`를 만들고(배치마다 `material`·`atlas`·`prefabDir`, FBX 머티리얼 슬롯별 `materials`(아틀라스·컷아웃·`shader`, 먼 산은 `Assets/Shaders/DistantHaze.shader`), 모듈 `scale`, 야외는 `ground`·`environment`로 바닥·해·안개·하늘색 지정) 카메라 렌더를 `Logs/<씬 이름>.png`로 저장한다. 모듈 프리팹은 `Assets/Prefabs/Environment/{Lab/Modules,Forest}/PF_*`이고, 충돌체는 빌더의 이름 태그 목록(`MeshColliderTags`·`NoColliderTags`, 나머지는 BoxCollider)으로 정한다. 숲 배치 `Forest_Test.json`은 손으로 쓰지 않고 `Art/tools/make_forest_test.py`가 만든다. `doors`는 경첩(`LabDoor`) 아래에 문짝·손잡이를 붙여 조립하고, `playerStarts`가 있으면 `Assets/Prefabs/Player/PF_Player.prefab`(없을 때만 생성)을 놓는다. JSON은 Blender 좌표로 기록하고, 빌더가 `(x,y,z)→(-x,z,-y)`, Z축 회전 θ→Y축 회전 -θ로 변환한다.
- **캐릭터 조립:** 메뉴 `Roosevelt > Characters > Assemble Examples` 또는 `Temp/CharacterAssembler.request`로 `Assets/Editor/CharacterAssembler.cs`가 `Assets/Art/Props/Example Character/`의 모듈 캐릭터(PolyMate) 파츠를 뼈 이름으로 한 뼈대에 합쳐 `Assets/Prefabs/Characters/PF_Char_Example_*`와 `Assets/Scenes/Character_Test.unity`를 만든다. FBX가 원작자 PC의 텍스처를 가리켜서, 기본 FBX에서 꺼낸 팔레트 `Textures/T_Char_Example_Palette.png`를 모든 파츠에 입힌다.
- **카메라:** Cinemachine은 에디터 내장 패키지(`com.unity.cinemachine` 6.6.0, 네임스페이스 `Unity.Cinemachine`, API는 CM3)다. `PF_Player`의 `PlayerCamera`가 `CameraTarget`을 따라가고, `PlayerCameraFeel`이 걷는 속도에 맞춰 노이즈 세기·빠르기를 섞는다. 노이즈 프로필은 `Assets/Settings/Cinemachine/Noise_WalkBob.asset`(위아래·좌우 사인파 + 손떨림 회전). 걸음마다 임펄스를 쓰면 카메라가 뚝뚝 떨어져 보여서 뺐다.
- **패키지:** 실험/프리뷰 패키지(`com.unity.ai.assistant` pre, `com.unity.pipeline` exp)가 포함되어 있다. 패키지 관련 이상 동작은 여기부터 의심한다.

## Unity 파일 규칙

- 에셋을 추가·이동·이름변경할 때는 `.meta`도 함께 다룬다. `.meta`의 GUID가 참조를 유지하므로, 에셋만 옮기고 `.meta`를 두고 가면 씬/프리팹 참조가 깨진다.
- `*.csproj`, `*.sln`은 Unity가 생성하는 파일이다. git에서 제외되어 있으니 수정하지 않는다.
- `.unity`, `.prefab`, `.asset`은 YAML이다. 직접 편집은 간단한 필드 수정 정도로만 하고, 구조 변경은 에디터에서 하는 편이 안전하다.
- Git LFS가 설정되어 있지 않다. 텍스처·모델·오디오 같은 큰 바이너리를 추가하기 전에 LFS 설정을 먼저 제안한다.

## CLAUDE.md 관리 규칙

이 파일은 **200줄 내외**로 유지한다. 수정한 뒤에는 줄 수를 확인하고, 200줄을 넘게 되면 내용을 추가하기 전에 사용자에게 아래 중 하나를 제안한다. 삭제와 분리는 사용자가 승인한 뒤에만 실행한다.

1. **삭제 제안:** 코드나 설정 파일을 열면 바로 알 수 있는 내용, 더 이상 사실이 아닌 내용, 다른 곳과 중복된 내용을 후보로 골라 이유와 함께 보여준다.
2. **분리 제안:** 이 파일의 다른 내용과 성격이 동떨어진 내용은 새 md 파일로 옮긴다. 특정 시스템 설계나 특정 작업에서만 필요한 절차가 여기에 해당한다.
   - 위치: `.claude/rules/<주제>.md` (파일명은 영문 kebab-case)
   - 특정 폴더나 파일을 다룰 때만 필요한 내용이면 frontmatter에 `paths`를 넣는다. 그러면 그 파일을 읽을 때만 로드된다.
     ```
     ---
     paths:
       - "Assets/Scripts/Combat/**/*.cs"
     ---
     ```
   - `paths`가 없는 rules 파일은 매 세션 통째로 로드되므로 컨텍스트가 줄지 않는다. 항상 필요한 내용이라면 분리하지 말고 CLAUDE.md 안에서 줄인다.
   - 분리하면 아래 "분리된 문서" 목록에 한 줄을 추가한다.

**판단 기준:** 거의 모든 작업에 필요한 내용이면 CLAUDE.md에 두고, 특정 작업에서만 필요하면 별도 파일로 뺀다.

## 문서 읽기 규칙

1. **세션 시작:** 이 파일은 자동으로 로드된다. 상위 폴더의 `CLAUDE.md`가 import하기 때문이다. 따로 읽을 필요는 없다.
2. **작업 시작 전:** 아래 "분리된 문서" 목록에서 이번 작업과 관련된 문서를 먼저 읽는다. `paths`로 자동 로드되는 문서라도 코드를 읽기 전에는 로드되지 않으므로, 목록을 기준으로 직접 챙긴다.
3. **기획 문서:** 상위 폴더(`D:\04_Project Files\Roosevelt\`)의 문서는 자동 로드되지 않는다. 기획이나 설계와 관련된 작업이면 그곳에서 관련 문서를 찾아 읽는다.
4. **불일치:** 문서와 코드가 다르면 코드를 사실로 본다. 문서는 바로 고치지 않고, 수정안을 사용자에게 제안한다.

### 분리된 문서

- [.claude/rules/asset-pipeline.md](.claude/rules/asset-pipeline.md): Blender 모듈 제작, 아틀라스, FBX 익스포트, 연구소 씬 빌드 등 에셋 작업 전에 읽는다

추가 형식: `- [.claude/rules/<주제>.md](.claude/rules/<주제>.md): 언제 읽어야 하는지 한 줄`
