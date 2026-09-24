# 크리처: Stalk

작성 2026-09-12. 레퍼런스는 `Art/Image/fe5c128b76a66d2f6726bc4b38bc5b9f.jpg`다(섬유 다발 목, 세로 틈이 난 꼬투리 머리, 땅 가까이에서 뿌리로 갈라지는 다리).

## 설정
- 리좀 네트워크(`../03_아트_레퍼런스/비주얼_컨셉_리좀오염.md`)에 속한, 떠도는 개체다. 수피는 Tree_Twisted의 뼈색보다 조금 회색이고, 머리에는 가로 골이 있는 어두운 세로 틈이 있고, 그 속에 CCTV 렌즈와 같은 빨간 렌즈(눈)가 있다. 한 번 뺐다가 사용자가 비교해 보고 있는 편이 낫다고 해서 되돌렸다(2026-09-12).
- 행동: 집 주변 14m를 천천히 배회한다. 플레이어가 22m 안에 들어오면 멈춰서 몸을 돌리고, 머리 틈으로 응시한다. 공격은 없다(게임플레이 역할 미정).
- 눈의 빛(발광): 배회하는 동안에는 렌즈가 희미하게 숨 쉬듯 빛난다(0.5배). 응시를 시작하면 고개가 돌아가는 속도에 맞춰 4배까지 밝아지고 조금 떨린다. 틈의 가운데 골도 약하게 함께 빛난다. 발광 마스크는 `T_Creature_Stalk_Emission.png`(아틀라스와 같은 배치)이고, 세기는 StalkWalker의 `eyeIdle`·`eyeStare`로 조절한다. 발광색은 팔레트 빨강을 순수한 빨강 쪽으로 옮긴 색이다. 팔레트 빨강을 그대로 1배 넘게 밝히면 분홍색으로 날아가기 때문이다.
- 뿌리 발은 들릴 때 갈퀴처럼 오므라들고 내려놓기 직전에 다시 펼쳐진다. 땅에 박힌 발은 몸이 돌아도 따라 돌지 않는다.

## 변형
| 이름 | 다리 | 높이 | 삼각형 | 뼈 | 걸음 |
|---|---|---:|---:|---:|---|
| A | 3, 휜 목 | 9.8m | 956 | 31 | 0.5m/s, 한 번에 한 발, 회전 11°/s |
| B | 2(죽마), 큰 머리 | 9.1m | 640 | 20 | 0.55m/s, 회전 14°/s |
| C | 4, 작음 | 6.0m | 944 | 34 | 0.6m/s, 대각선 두 발, 회전 22°/s |

## 파일
- 생성 스크립트: `Art/tools/make_stalk.py`. 결과물 `Art/Creatures/Stalk.blend`는 생성 파일이라 손으로 고치지 않는다. 검수 렌더는 `Art/Creatures/review/`
- Unity 에셋: `Assets/Art/Creatures/Stalk/`의 `Meshes/SK_Creature_Stalk_*.fbx`, `Textures/T_Creature_Stalk_Atlas.png`, `Materials/`
- 프리팹 `Assets/Prefabs/Creatures/PF_Creature_Stalk_*`, 테스트 씬 `Assets/Scenes/Creature_Stalk_Test.unity`
- 코드: `Assets/Scripts/Creatures/StalkWalker.cs`(런타임 걷기), `Assets/Editor/CreatureBuilder.cs`(프리팹·씬·캡처)

## 절차
1. `blender.exe -b --factory-startup --python-exit-code 1 -P Art/tools/make_stalk.py`
2. Unity 메뉴 `Roosevelt > Creatures > Build Stalks`, 또는 `Temp/CreatureBuilder.request`를 만든다. 프리팹과 테스트 씬이 만들어지고, `Logs/Creature_Stalk_*.png`(정지 자세, 6초 걷기 시뮬레이션)가 저장된다. 로그에는 이동 거리, 걸음 수, 가장 낮았던 엉덩이 높이, IK 초과 길이가 찍힌다.
3. 숲 배치: `make_forest_test.py`가 `Forest_Test.json`에 `creatures`(프리팹 이름, 위치, 회전)를 쓴다. LabSceneBuilder는 지형 충돌체를 발이 딛는 땅으로 연결한다.
4. 숲 검수: `Roosevelt > Creatures > Review In Forest`, 또는 `Temp/CreatureForestReview.request`. 40초 배회시킨 뒤 `Logs/Creature_Forest_*.png`를 저장한다.

## 리그와 코드 약속
- 뼈 이름은 Hip, Neck_1..n, Head, Leg<i>_Upper/Lower/Foot, Leg<i>_Root<j>이다(여기에 FBX leaf `_end`가 붙는다). StalkWalker가 이 이름으로 뼈를 찾는다.
- 모든 회전을 휴지 자세 기준으로 다시 계산하므로 뼈 축은 상관없다. 휴지 자세가 곧 바인드 자세이자 걷기 기준 자세다.
- 무릎은 조금 굽혀 둔다(IK 여유). 너무 곧게 펴 두면 발이 멀어질 때 몸이 내려앉는다.
- FBX는 Generic 리그로 가져온다. None으로 가져오면 스킨이 사라진다. Animator는 빌더가 지운다.
- 정강이에 CapsuleCollider(Ignore Raycast 레이어)가 있어 플레이어가 다리를 통과하지 못한다.

## 조정
- 속도, 회전, 걸음 시간, 걸음 높이, 걸음 트리거는 CreatureBuilder의 `Gaits` 표에서 바꾼다. 배회 반경과 응시 거리는 StalkWalker 인스펙터에서 바꾼다(기본 14m, 22m).

## 검수 기록 (2026-09-12, 숲 40초 배회)
- A는 7.9m를 33걸음, B는 4.6m를 10걸음, C는 12.3m를 74걸음 걸었다. IK 초과는 최대 1.5cm였다.
- 비탈에서는 몸이 가장 많이 내려앉았다. 휴지 높이보다 A는 1.1m, C는 0.9m 낮아졌다. 발이 낮은 곳을 디디면 다리가 닿도록 엉덩이를 내리기 때문이다. 캡처에서는 눈에 띄지 않았지만 Play에서 무릎을 꿇는 것처럼 보이면, `make_stalk.py`에서 무릎을 더 굽혀 여유를 늘린다.

## 남은 것
- Play에서 직접 걸어 보며 확인하기(응시 전환, 다리 충돌)
- 사운드(발 뽑는 소리, 삐걱임), 응시 연출(렌즈 발광, 카메라 떨림), 게임플레이 역할
- 경로 찾기가 없다. 구체 스윕으로 막힌 목적지를 피하기만 하고, 앞이 막히면 멈췄다가 다른 곳을 고른다.
