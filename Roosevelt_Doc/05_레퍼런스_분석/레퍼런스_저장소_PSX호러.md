# 레퍼런스 저장소: PSX 호러 / 리미널 공간

학습·참고용으로 내려받은 외부 저장소 목록. **에셋을 Roosevelt에 직접 가져다 쓰지 않는다.**
다운로드 위치: `F:\02_Works\02_Unity Files\_Reference_PSXHorror\` (총 7.2GB, 2026-09-15 클론)

## 라이선스 요약

| 저장소 | 라이선스 | Roosevelt에서 |
|---|---|---|
| Kodrin/URP-PSX | MIT | **코드 사용 가능** (저작권 표시 필요) |
| Natteens/retropsx-urp | MIT | **코드 사용 가능** (저작권 표시 필요) |
| fer-moreira/psx-effect-urp | Unlicense | **코드 사용 가능** (사실상 퍼블릭 도메인) |
| 89Mods/InfiniteBackrooms | MIT (+README에서 크레딧 요구) | 코드 참고, 쓰려면 크레딧 |
| cnharlan/GD_BlueMist | **GPL-3.0** | 코드 복사 금지 (전염성). 보고 배우는 것만 |
| MichaElL0/Something-in-the-woods | **라이선스 없음** | 복사 금지. 구조만 참고 |
| TobinCavanaugh/The-Cabin-On-The-Shore | **라이선스 없음** | 복사 금지. 구조만 참고 |

> 라이선스 없음 = 기본 저작권이 살아있음. 읽고 배우는 건 자유지만 코드·에셋 복사는 안 된다.
> 두 저장소 모두 에셋스토어 유료 패키지가 무단 커밋되어 있어(Krearthur GOPainter / NatureManufacture
> Forest Environment) 그쪽 파일은 특히 손대지 않는다.

## 1. Kodrin/URP-PSX — ★920, MIT ⭐가장 중요

`URP-PSX/URP-PSX/Assets/`

URP용 PSX 렌더링 3종 세트. 각각 `*RenderFeature.cs` + `*Controller.cs`(Volume 컴포넌트) + `.shader` 구성.

- `Scripts/Pixelation/` + `Shaders/Pixelation.shader` — 해상도 다운샘플
- `Scripts/Dithering/` + `Shaders/Dithering.shader` — 색상 밴딩 + 디더
- `Scripts/Fog/` + `Shaders/Fog.shader` — 거리 안개
- `Shaders/URP_PSX_PBR_Master.shadergraph`, `URP_PSX_Unlit_Master.shadergraph` — 정점 지터(vertex snapping) + 아핀 텍스처 워핑
- `Shaders/HLSL/CustomLighting.hlsl`

Roosevelt 연결: 이미 `Assets/Shaders/DistantHaze.shader`가 있으니 Fog는 겹칠 수 있다.
Pixelation/Dithering은 Point 필터 + 64px/m 아틀라스 방향과 잘 맞는다.

## 2. Natteens/retropsx-urp — MIT, **Unity 6 대상**

`retropsx-urp/` — Runtime/Editor로 나뉜 정식 패키지 구조(asmdef 포함).
Kodrin 것보다 최신이고 Unity 6를 명시적으로 노린다. 프로파일 기반(ScriptableObject)이라 설정 관리가 낫다.

- `Runtime/Rendering/Passes/` — RenderGraph 시대 패스 구현
- `Runtime/Profiles/` — Color/Fog/Geometry/Lighting/Raster/Texture 프로파일 분리
- `Runtime/Atmosphere/Volumetrics/` — 볼류메트릭 라이트
- `Editor/Import/RetroTextureProfileApplicator.cs` — 임포트 시 텍스처 설정 강제.
  Roosevelt의 `ArtTexturePostprocessor.cs`와 같은 역할이라 비교해볼 것.

## 3. 89Mods/InfiniteBackrooms — MIT, VRChat/UdonSharp

`InfiniteBackrooms/Assets/TholinStuff/Udon/`

무한 리미널 공간 절차 생성. **UdonSharp라 그대로는 Unity에서 안 돌아간다** — 알고리즘만 읽는다.
Udon 제약으로 2D 배열을 전부 1D로 평탄화해놨는데, 이건 Roosevelt에선 불필요한 제약.

- `MazeGenerator.cs` — recursive division 변형. 재귀 대신 **고정 스택 + while 루프**라 프레임 분할 실행 가능.
  벽마다 통로를 최대 3개 뚫어 외길 방지. 작은 구획에서 확률적으로 분할 중단 → "큰 방" 생성.
- `Chunk.cs` — 미로 데이터를 메시로 굽기. **라이트 개수 제한 때문에 청크를 4×4 메시로 분할**한다는
  주석이 있음. 어두운 실내에서 포인트 라이트 여러 개 쓸 때 그대로 부딪히는 문제.
- `WorldGenerator.cs` — 청크 로드 디렉터. 프레임당 반복 횟수를 잘라 히칭 방지.
- `Xorshift.cs`, `LampPlacer.cs`, `PlayerChunkPosition.cs`

## 4. cnharlan/GD_BlueMist — GPL-3.0, Godot ⚠️

`GD_BlueMist/PSX Shader/shaders/` — PSX 셰이더 12종(lit/unlit × metal/transparent/alpha-scissor,
밴드 디더, LCD 포스트, 라이트 볼륨). Godot 셰이더 언어라 URP로 그대로 못 옮긴다.

**GPL이므로 코드를 베끼면 Roosevelt 전체를 GPL로 공개해야 한다.** 기법을 눈으로 보고
직접 새로 구현하는 용도로만. 같은 효과는 위 MIT 저장소들에 다 있으니 사실 볼 일이 적다.

레포 설명의 "surrealist Appalachian town"은 실제로 없음 — 셰이더 데모 프리미티브 4개가 전부인
프로토타입에서 2022년에 멈췄다.

## 5. MichaElL0/Something-in-the-woods — 라이선스 없음

`Something-in-the-woods/Assets/Scripts/` — 직접 작성 43개.
**주의: `Assets/URP/shader/URP-PSX/`는 위 1번 Kodrin 저장소를 벤더링한 것**이므로 원본을 봐야 한다.

모델 링용이 아니라 **짧은 호러의 구조** 참고용:
- `StoryBlock.cs`, `RoadBlock.cs` — 진행 게이팅
- `MuffleAmbience.cs`, `MuffleLampHum.cs` — 위치별 환경음 감쇠
- `jumpscareTrigger.cs`, `ScreamTrigger.cs`, `RakeTrigger.cs` — 트리거 이벤트 분리 방식
- `RakeAI.cs` — 추격 AI
- `AudioManager.cs` + `Sound.cs` — 사운드 풀

## 6. TobinCavanaugh/The-Cabin-On-The-Shore — 라이선스 없음, 2GB

용량 대부분이 에셋스토어 나무 패키지(NatureManufacture). 본인 제작 `.blend` 12개
(보트·붐박스·책상의자·부두·쓰레기통·손전등·기름통·오븐·싱크·태양광패널·물·역기)는
**Blender 원본 파일이라 토폴로지·UV 구성을 뜯어보기 좋다** — 로우폴리 소품 모델링 학습용.

## 존재하지 않는 레포

"The Corridor", "The Disappearance"는 GitHub 검색에 없다. 출처가 부정확했던 것으로 보인다.
