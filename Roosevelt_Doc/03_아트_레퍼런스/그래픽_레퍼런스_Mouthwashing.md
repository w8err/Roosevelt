# 그래픽 레퍼런스: Mouthwashing (Wrong Organ, 2024)

> 현재 역할: 색감·조명 참고용. 인물은 SIREN, 맵은 Fatum Betula로 바뀜. 전체는 `그래픽_레퍼런스.md`

표기: [출처] 확인된 사실 / [관찰] 플레이 영상 기반 추정, 실제 화면으로 재확인 필요

## 1. 확인된 사실
- 엔진 Unity. 우리와 같음 [출처: Wikipedia]
- PS1 계열 로우폴리 레트로 스타일. 아트 담당 Johanna Kasurinen이 Puppet Combo 영향 언급 [Wikipedia]
- 스타일 선택 이유 [Skybox Critics 인터뷰]
  - 3D 경험 없어도 "실행 가능한" 스타일
  - 저해상도는 "goofy와 grotesque 사이의 얇은 선". 사실적 그래픽보다 공포 요소를 덜 어색하게 넣을 수 있음
- 팔레트는 처음부터 "노을색(sunset colors)". 우주 속 노을이 핵심 비주얼 [인터뷰]
- 거주 구역의 대형 LED 스크린(가짜 노을 풍경)이 따뜻한 광원이자 고립감을 드러내는 장치 [인터뷰]
- 영향 받은 영화: Alien, Event Horizon, Sunshine, The Thing, The Shining, Pandorum [Wikipedia]
- 평가: "로우폴리 호러 스타일 속 AAA급 아트 디렉션"(GamesRadar+), 린치·아르젠토에 비유(Guardian) [Wikipedia]
- 사양: 최소 GTX 560, 권장 GTX 1050, 3GB [Steam]

## 2. 스타일 요소 분해 [관찰]
- 폴리곤: 각지고 큼직한 형태. 실루엣은 명확, 디테일은 텍스처가 담당
- 텍스처: 저해상도에 필터링 없이(point) 픽셀이 보임. 로고·문구·포스터 같은 그래픽 디자인 요소가 많음
- 캐릭터: 단순한 머리 형태에 이목구비를 텍스처로 그려 넣음. 과장된 얼굴로 감정 전달
- 조명: 강한 단색 광원(주황·노랑·빨강 경고등, 차가운 청록). 대비 높고 색 수 적음
- 공간: 좁은 복도, 어두운 주변부, 시야 끝을 어둠·안개가 먹음
- 연출: 컷 전환과 흔들림·일그러짐으로 환각 표현. 저해상도라 현실→환각 전환이 자연스러움
- 톤: 밝고 채도 높은 색인데 불쾌함. 귀여움과 끔찍함의 공존

## 3. 참고 팔레트
- 공식 팔레트는 없음. 방향만: 노을 주황·분홍·노랑 + 청록·녹색 + 경고 빨강
- 팬 추출 팔레트(비공식, 참고만): #5b9a8c #a6d8af #f4e1a4 #f8b89b #d64c4c [colormagic.app]

## 4. Roosevelt 적용안 (Unity 6 URP)
- 렌더: 저해상도 렌더 후 업스케일(Render Scale 낮춤 또는 RenderTexture + point 필터)
- 텍스처: Filter Mode=Point, 크기 64~256, mipmap 끔 검토
- 셰이더: URP Lit 대신 Unlit/Simple Lit + 버텍스 컬러 조명 검토. PS1식 버텍스 흔들림·affine 왜곡은 선택 사항. Mouthwashing은 약하게 쓰는 편 [관찰]
- 후처리: 색 보정(노을 LUT), 비네트, 약한 그레인, 필요 시 디더링·색 수 제한
- 안개: 거리 안개로 시야 제한 + 어두운 톤
- 품질 티어: Mobile/PC 두 RP asset 모두 반영
- 모델링 규칙(Blender): 오브젝트당 수백 면 이내, flat shading, 디테일은 텍스처로. 나무상자(WoodenCrate, 120면)가 기준 예시

## 5. 확인 필요
- 버텍스 스냅·affine 텍스처 왜곡 실제 적용 여부
- 내부 렌더 해상도, 후처리 종류(그레인·색수차 등)
- 참고 자료: 공식 아트북 "Mouthwashing: Design Works"(Lost in Cult)

## 출처
- https://en.wikipedia.org/wiki/Mouthwashing_(video_game)
- https://skyboxcritics.com/2025/07/14/mouthwashings-genesis-sick-jokes-and-the-thin-line-between-goofy-and-grotesque-an-interview-with-wrong-organ/
- https://store.steampowered.com/app/2475490/Mouthwashing/
- https://www.lostincult.co.uk/mouthwashing
- https://colormagic.app/palette/677da3a7a253ac9f50cf15cc
