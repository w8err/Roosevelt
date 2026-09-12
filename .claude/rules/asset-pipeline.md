---
paths:
  - "Assets/Art/**"
  - "Assets/Prefabs/Environment/**"
  - "Assets/Editor/LabSceneBuilder.cs"
  - "Assets/Editor/ArtTexturePostprocessor.cs"
---
# 에셋 파이프라인

에셋 작업 전에 `D:\04_Project Files\Roosevelt\에셋_제작_과정.md`(절차)와 `에셋_규격서_지하연구소.md`(규격)를 읽는다.

- 모듈 원본 `Art/Lab/Lab_Modules.blend`는 MCP로 열지 않고 `blender.exe -b ... -P 스크립트`로 고친다. 고치기 전에 백업한다.
- 공용 함수는 `Art/tools/labkit.py`. 새 모듈은 `make_doors.py`, 변형은 `make_doorpair.py`를 본뜬다.
- 아틀라스는 `Atlas.is_free`로 확인한 빈 영역에만 칠하고, 규격서 3장 표를 갱신한다.
- 모듈 단독과 배치 모두 `zfight_pairs`가 0이어야 한다. 방을 이을 때는 등을 맞댄 벽과 `Door`/`DoorPair` 짝을 쓴다.
- Unity 확인은 `Temp/LabSceneBuilder.request`를 만든 뒤 `Logs/Editor.log`의 `[LabSceneBuilder]` 줄과 `Logs/<씬 이름>.png`로 한다. 빌드는 창이 뒤에 있어도 돌지만, 새 스크립트는 Unity 포커스 후에 컴파일된다.
- FBX 이름을 바꿀 때는 `.meta`도 함께 옮긴다.
- Blender는 여러 세션이 공유한다. 저장·익스포트 전에 씬 오브젝트 목록을 확인한다.
