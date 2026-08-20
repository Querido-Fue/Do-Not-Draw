# Do Not Draw 프로젝트 가이드

## 프로젝트 위치와 기준 씬

- Unity 프로젝트 루트: `Do Not Draw/`
- 기준 씬: `Do Not Draw/Assets/Scenes/ClosedRoom.unity`
- Unity 6000.5.8f1, URP, Input System을 사용한다.

## 카드/내러티브 기반 구조

런타임 흐름은 다음 책임 경계를 유지한다.

1. `PlayerInteractionRouter`가 화면 중앙 레이·거리 판정, F키 입력, 공용 프롬프트를 중앙에서 담당한다. 카드 덱, 문, 스위치는 `PlayerInteractableBehaviour`로 등록하며, `InteractableOuterGlow`가 실제로 활성화된 현재 조준 대상만 상호작용할 수 있다.
2. `CardDeckInteraction`은 현재 카드 스테이션의 활성 상태와 러너 호출만 담당한다.
3. `CardSequenceRunner`는 단계 상태, 대기 조건, 자동/수동 실행, 조건부 카드, 분기와 완료를 담당한다. 씬 상호작용에 의한 외부 진행은 다음 프레임에 처리해 신호 콜백 재진입을 막는다.
4. `CardDeckPresenter`는 카드 생성, 이동/뒤집기, 덱 높이, 배치, 카드·음성 효과음을 담당한다. 카드 공개 신호는 착지 시점에 보내고, 음성이 끝날 때까지 다음 드로우를 잠근다.
5. `CardSequenceDefinition`과 `CardDefinition`은 씬 참조가 없는 순수 데이터 에셋이다.
6. `StoryBlackboard`는 플레이 중 변하는 서사 상태를 보관한다.
7. `StoryFact`/`StoryCondition`은 타입이 있는 조건을 정의하고, `StorySignal`/`StorySignalListener`는 러너와 씬 연출 오브젝트를 분리한다.

최종 씬 연출은 `ClosedRoomStoryDirector`가 신호를 받아 조명, 두 번째 문/방, 실루엣, 시선·회전 판정, 추적 연출과 엔딩을 조정한다. 장면 전용 상호작용과 구역 코드는 `Assets/DoNotDraw/Scripts/World/`, 공용 상호작용 코드는 `Assets/DoNotDraw/Scripts/Interaction/`에 둔다.

관련 런타임 코드는 `Assets/DoNotDraw/Scripts/Narrative/`, 검증 도구는 `Assets/DoNotDraw/Editor/Narrative/`에 둔다.

## 반드시 지킬 규칙

- 최종 기획 내용은 카드 번호나 거대한 `switch` 문으로 하드코딩하지 않는다. 카드, 단계, 조건, 신호, 전이는 에셋 데이터로 작성한다.
- `stableId`는 에셋 종류 안에서 고유해야 하며, 다른 에셋에서 사용하기 시작한 뒤에는 바꾸지 않는다.
- ScriptableObject에는 씬 오브젝트 참조나 플레이 중 변하는 값을 저장하지 않는다. 런타임 값은 `StoryBlackboard`와 `CardSequenceRunner`에만 둔다.
- 씬 연출은 `StorySignalListener` 또는 전용 리스너 컴포넌트로 연결한다. 신호 콜백 안에서 러너를 즉시 재시작/중지/완료시키는 재진입 호출은 피하고, 사실값과 조건 또는 다음 프레임 명령으로 연결한다.
- 플레이어 상호작용 컴포넌트에서 F키를 직접 폴링하거나 자체 프롬프트를 만들지 않는다. 반드시 `PlayerInteractionRouter`를 통한다.
- 상호작용 가능 상태의 UX 표시는 예외 없이 `PlayerInteractionRouter`가 선택한 단일 대상의 아우터 글로우와 일치시킨다. 글로우가 활성화되지 않은 대상은 `Interact`에서도 실행을 거부한다.
- 전이는 위에서부터 첫 번째로 만족하는 항목을 사용한다. 조건 없는 기본 전이는 목록의 마지막에 둔다.
- 정수 조건은 정수로, 실수 조건은 실수로 비교한다. `StoryFact` 타입과 설정 값 타입을 섞지 않는다.
- 프로토타입 에셋은 기반 동작 확인용이다. 미확정 기획서의 카드 문구, 연출 순서, 결말을 여기에 확정 콘텐츠처럼 넣지 않는다.

## 최종 기획 반영 절차

1. 필요한 상태를 `StoryFact` 에셋으로 만든다.
2. 조명, 문, 오디오, 카메라 같은 씬 반응마다 `StorySignal`을 만들고 리스너를 연결한다.
3. 카드별 표현을 `CardDefinition`으로 만든다.
4. `CardSequenceDefinition` 단계에 실행 방식, 대기, 조건, 신호, 전이를 조합한다.
5. `Tools > Do Not Draw > Validate Narrative Assets`를 실행해 오류와 경고를 해결한다.
6. 플레이 모드에서는 `CardSequenceRunner` 커스텀 인스펙터의 디버그 버튼으로 단계별 확인이 가능하다.

## 현재 최종 흐름 구현

- 연출 내용의 최종 권위는 `C:/Users/choo7/Downloads/DO_NOT_DRAW_연출상세기획_프로토타입A.txt`다. 다만 사용자가 대화에서 명시적으로 덮어쓴 항목은 문서보다 우선한다. 현재 공간 미술은 문서의 꽃무늬 벽/3×4m 지시 대신 **밝고 섬뜩한 백룸 스타일/6×4.8m**를 사용한다. 벽은 무늬가 약한 황색 벽지, 바닥과 천장은 `Assets/Asset/BackroomsLikeAsset`의 PBR 메시/재질, 천장 조명 외형은 팩 천장에 포함된 사각 발광 패널의 반복으로 구성한다.
- 최종 `FinalFlowSequence`는 19개 번호 카드, 방 귀환 경계용 `s05a_next_room_card`, 이벤트 전환/대기 5개를 합친 25단계다. 12번 결과 카드는 플레이어의 회전 여부에 따라 `GOOD.` 또는 `I SAW YOU LOOK.`을 선택하므로, 카드 비주얼 에셋은 보조 카드와 분기 변형을 포함해 21개다. 최종 카드·사실·신호·시퀀스는 `Assets/DoNotDraw/Narrative/Final/`에 있다.
- `Tools > Do Not Draw > Build Final Flow Experience`는 최종 데이터와 `ClosedRoom`의 `FINAL EXPERIENCE - FLOW AUTHORITY` 루트를 재생성하는 멱등 빌더다. 최종 루트나 생성 자산을 수동으로 고쳤다면 재빌드 시 덮어써질 수 있으므로 빌더 코드를 먼저 수정한다.
- 두 방의 북쪽 벽은 체크 표시됐던 왼쪽 문 슬롯을 벽과 몰딩으로 막고 중앙 창과 오른쪽 문만 둔다. 첫 방 오른쪽 문은 방 사이 이동용이고, 두 번째 방 오른쪽 문은 최종 탈출용이다. 스토리 문, 백색 출구광, 탈출 가드, 엔딩 트리거와 후방 림 앵커는 모두 오른쪽 문 축(`x=+2`)에 맞춘다.
- 방 구조는 `Assets/DoNotDraw/Editor/Final/DetailedRoomSetFactory.cs`가 생성한다. 두 방은 각각 6m(가로) × 4.8m(깊이) × 3m(높이)이며 0.2m 간격으로 이어진다. 기존 북벽 슬롯 축은 `x=-2/0/+2`지만, 현재 열린 구성은 중앙 창 `x=0`과 오른쪽 문 `x=+2`뿐이다. 벽은 `DoNotDraw/BackroomsWorldSurface` 트라이플래너 셰이더와 `Textures/Backrooms/`의 벽지 알베도를 사용한다. 바닥·천장 시각 레이어는 백룸 팩의 `Tiles_01_Fill.prefab`을 방 크기에 맞춰 스케일하고 팩의 메시와 UV를 유지한다. 천장은 팩의 오피스 천장 PBR 재질과 내장 발광 패널을 그대로 사용하고, 바닥 슬롯만 원본 `Floor_Carpet_Mat`에서 복제한 프로젝트 소유 `BackroomsAssetCarpetTinted.mat`로 덮어써 밝은 황베이지 색감과 낮춘 노멀 강도를 적용한다. 충돌은 렌더러를 끈 기존 바닥·천장 큐브가 담당하며, 팩 메시의 `MeshCollider`는 제거한다. 벽 하단 몰딩에는 팩의 `Wall_trim_mat`를 사용하고, 천장에는 촘촘한 0.6m 레일 대신 외곽 프레임만 추가한다.
- 화면 후처리는 `Assets/DoNotDraw/PostProcessing/ClosedRoomHorrorProfile.asset`의 전역 Volume 효과 뒤에 `Assets/Settings/PC_Renderer.asset`의 `CRT Monitor Post Process` Full Screen Pass를 실행한다. 이 패스는 `CRTMonitorPostProcess.mat`과 `CRTMonitorPostProcess.shader`를 사용해 게임 카메라 영상에 곡면, 스캔라인, 인광 마스크, 미세 수평 지터와 플리커를 합성한다. `ScreenSpaceOverlay` UI는 프롬프트 가독성을 위해 CRT 왜곡 대상에서 제외한다.
- 화면 UI는 `Assets/DoNotDraw/Scripts/UI/ResolutionIndependentCanvas.cs`를 공통 진입점으로 사용한다. 월드 스페이스를 제외한 모든 Canvas는 1920×1080 기준 `Scale With Screen Size / Expand`로 통일해 기준 영역이 어떤 종횡비에서도 잘리지 않게 하며, 씬 로드 시 런타임에서 다시 보정한다. `FinalExperienceBuilder`도 같은 규칙을 씬에 직렬화한다. 설정 팝업의 `Panel`은 1820×980 고정 기준 프레임으로 중앙 정렬해 1920×1080에서 의도한 50px 여백을 유지한다. 전체 화면 암전과 배경 이미지는 스트레치 앵커로 실제 화면 끝까지 채운다.
- `ClosedRoomStoryDirector`는 3600K 즉시 하드 스타트, 카드마다 0.2초/15~20% 조명 딥과 형광등 틱, 암전 전환, 문/스위치 상호작용, 창문 환영, 추적 실루엣, 회전 판정, 탈출과 엔딩을 신호 기반으로 실행한다. #3/#4/#5는 미준수 시 같은 단계로 되돌아가 같은 카드를 다시 뽑으며, 플레이어 대신 조명·문·위치를 강제로 바꾸는 폴백은 두지 않는다. #3의 실제 끄기→켜기 뒤에는 형광등을 3400K로 복구한다. 방 크기에 의존하는 플레이어·위협·조명 지점은 씬에 직접 하드코딩하지 않고 `DetailedRoomSetRefs` 마커를 통해 전달한다. 추적 실루엣의 시각 메시에는 `Assets/ExternalModels/BackroomsEntity/BackroomsEntity.prefab`을 사용하며, 빌더가 모든 서브메시에 공용 실루엣 재질을 적용하고 충돌체를 제거한다.
- #5 이후 두 번째 방에서 #6 카드를 뽑기 전에 첫 방으로 되돌아오면 `s05a_next_room_card`를 보여주고 `s05a_wait_reenter`에서 실제 재입장을 기다린다. 입장/귀환/엔딩 구역은 `NarrativeZoneTrigger`와 중력 없는 키네마틱 Rigidbody를 함께 생성해 `CharacterController`의 물리 트리거 콜백을 보장한다.
- `CardDefinition`은 글자 손상 단계, 텍스트 페이드, 이중 노출, 공개 시 들림을 보관하고 `CardDeckPresenter`가 실제 공개 연출을 담당한다.
- 빌더는 원본 `Assets/Sounds/voice.mp3`와 무음 구간 기준의 21개 분할 음성을 `Assets/Sounds/voice/`에 보존한다. 프로토타입 A에서는 두 `CardDeckPresenter`의 `voiceNarrationEnabled`를 꺼 실제 카드 음성 재생은 하지 않는다.
- 최종 효과음의 권위 소스는 `Assets/Sounds/01_fluorescent_buzz_loop.wav`부터 `20_fluorescent_starter_tick.wav`까지의 번호형 파일군이다. `FinalExperienceBuilder.FinalAudioClips`가 형광등·시계·드론·창문 노이즈·호흡·카드·스위치·문·바람·위협 접근음을 의미별로 연결한다. 플레이어 발소리는 첫 방의 `12_carpet_footstep_single`과 두 번째 방의 `12b_carpet_footstep_room2_pitched`를 `ClosedRoomStoryDirector`가 방 경계에서 전환하며, 뒤쪽 4연속 발소리는 `13_footsteps_behind_4steps_carpet`을 별도 큐로 사용한다.
- 엔딩 준비 시 이동 컨트롤러와 `RandomFootstepPlayer`를 함께 비활성화하고 재생 중 발소리를 즉시 정지한다. 최종 카드 공개 후 덱 줌과 마지막 1초 암전을 포함해 총 5초가 지나면 빌드에서는 `Application.Quit`, 에디터에서는 Play Mode 종료를 실행한다.
- 최종 공간에는 실제 의자, 창문 너머의 의자 잔상, 책상 하부 `Back Apron`, `Impossible Shadow`를 생성하지 않는다. 플레이어는 첫 방 책상 북쪽(+Z)에서 시작해 남쪽(-Z), 즉 책상을 바라본다.
- 초반에는 첫 덱과 `DoNotDraw_Wall 1` 그래피티를 숨기고, 플레이어 기준 정면 우측 벽의 전등 스위치만 처음부터 보이고 상호작용 가능하게 둔다. `OpeningDiscoveryReveal`은 플레이어가 책상 중심 기준 스위치 쪽 절반(-Z)에 들어와 스위치를 중심으로 한 120° 구간(±60°)을 바라보며, 덱 스폰 영역 전체가 카메라 프러스텀 밖에 있을 때 첫 덱만 공개한다. `s02_rear_rule`의 `reveal_opening_graffiti` 공개 신호가 `DO NOT LOOK BEHIND YOU.` 카드의 뒤집기·착지 완료 시 그래피티를 별도로 공개한다. 3번 카드가 나오기 전 스위치 조작은 실제 조명만 토글하며 `light_switch_used` 진행 조건에는 반영하지 않는다. 첫·둘째 방 덱과 미리 놓인 카드는 새 시작 방향에서 읽히도록 Y축 180°를 사용한다.
- 무작위/상시 전구 깜빡임은 사용하지 않는다. 방마다 연출 코드가 제어하는 실제 광원은 백룸 형광등 리그의 단일 포인트 라이트 하나이며, 눈에 보이는 천장 패널은 `Tiles_01_Fill.prefab`에 통합된 발광 메시만 사용한다. 별도 확산광 패널 메시나 추가 광원은 생성하지 않아 이중 조명을 방지하고 URP 추가 광원 비용과 연출 상태 분산을 피한다. 스위치를 끄면 직접 광원과 천장 재질의 HDR 발광을 입력 프레임에 즉시 비활성화한다. 천장 발광은 결합된 바닥·천장 렌더러의 천장 재질 슬롯만 `MaterialPropertyBlock`으로 제어하므로 바닥 메시와 에셋 원본 재질에는 영향을 주지 않는다. 남은 환경광·반사광은 레이트레이싱 잔광을 흉내 내도록 smoothstep 곡선으로 1초 동안 약 52% 감쇠해 기준값의 48%를 유지하며, 이는 이전 80% 감쇠 애니메이션의 약 0.6초 지점 밝기다. 스위치를 켜거나 장면 상태를 초기화할 때는 천장 발광과 기준 환경광을 즉시 복원한다. 그 밖의 조명 변화는 `ClosedRoomStoryDirector`가 기획된 큐에서만 `완전 암전 → 검은 화면 중 오브젝트/상태 변경 → 조명 복구` 순서로 실행한다.
- 이전에 만든 `AgedFloralWallpaper`, `WorldSpaceConcrete`, `AgedWoodFloor` 텍스처/재질은 보존하되 최종 씬 빌더에서는 참조하지 않는다.
- `Assets/DoNotDraw/Narrative/Prototype/`은 기반 회귀 확인용으로 남겨두며 최종 씬 러너에는 연결하지 않는다.
