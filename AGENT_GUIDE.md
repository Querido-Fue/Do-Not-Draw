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

- 연출 내용의 최종 권위는 `C:/Users/choo7/Downloads/DO_NOT_DRAW_연출상세기획.txt`다. 다만 사용자가 대화에서 명시적으로 덮어쓴 항목은 문서보다 우선한다. 현재 공간 미술은 문서의 꽃무늬 벽/3×4m 지시 대신 **콘크리트 벽/6×4.8m**를 사용한다.
- 최종 흐름은 19개 카드 단계로 구성한다. 12번 결과 카드는 플레이어의 회전 여부에 따라 `GOOD.` 또는 `I SAW YOU LOOK.`을 선택하므로, 카드 비주얼 에셋은 분기 변형을 포함해 20개다. 최종 카드·사실·신호·시퀀스는 `Assets/DoNotDraw/Narrative/Final/`에 있다.
- `Tools > Do Not Draw > Build Final Flow Experience`는 최종 데이터와 `ClosedRoom`의 `FINAL EXPERIENCE - FLOW AUTHORITY` 루트를 재생성하는 멱등 빌더다. 최종 루트나 생성 자산을 수동으로 고쳤다면 재빌드 시 덮어써질 수 있으므로 빌더 코드를 먼저 수정한다.
- 방 구조는 `Assets/DoNotDraw/Editor/Final/DetailedRoomSetFactory.cs`가 생성한다. 두 방은 각각 6m(가로) × 4.8m(깊이) × 3m(높이)이며 0.2m 간격으로 이어진다. 문/창은 `x=-2/0/+2`에 배치한다. 벽과 천장에는 `DoNotDraw/WorldSpaceConcrete` 트라이플래너 셰이더로 생성한 `Materials/Final/WorldSpaceConcrete.mat`을 사용해 큐브 크기나 면 방향이 달라도 콘크리트 텍스처의 월드 스케일이 유지되게 한다.
- `ClosedRoomStoryDirector`는 오프닝 입력 잠금, 암전 전환, 문/스위치 상호작용, 창문 환영, 추적 실루엣, 회전 판정, 탈출과 엔딩을 신호 기반으로 실행한다. 방 크기에 의존하는 플레이어·위협·조명 지점은 씬에 직접 하드코딩하지 않고 `DetailedRoomSetRefs` 마커를 통해 전달한다.
- `CardDefinition`은 글자 손상 단계, 텍스트 페이드, 이중 노출, 공개 시 들림을 보관하고 `CardDeckPresenter`가 실제 공개 연출을 담당한다.
- 빌더는 원본 `Assets/Sounds/voice.mp3`를 보존하고 무음 구간을 기준으로 자른다. 첫 감지 구간은 톤 테스트라 제외하고 2번부터 21개 카드 음성으로 `Assets/Sounds/voice/`에 생성한다.
- 무작위/상시 전구 깜빡임은 사용하지 않는다. 조명 변화는 `ClosedRoomStoryDirector`가 기획된 큐에서만 `완전 암전 → 검은 화면 중 오브젝트/상태 변경 → 조명 복구` 순서로 실행한다.
- 이전에 만든 `AgedFloralWallpaper` 텍스처/재질은 보존하되 최종 씬 빌더에서는 참조하지 않는다.
- `Assets/DoNotDraw/Narrative/Prototype/`은 기반 회귀 확인용으로 남겨두며 최종 씬 러너에는 연결하지 않는다.
