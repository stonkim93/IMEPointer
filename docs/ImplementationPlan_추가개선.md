# IMEPointer 성능 최적화 구현 계획서 (Implementation Plan)

사용자님의 요청에 따라 **1) 키보드 훅 처리 속도 최적화** 및 **2) 무식한 캐시 초기화(.Clear) 로직 개선**을 위한 작업 계획입니다.

## 1. 캐시 관리 로직 개선 (LRU Cache 도입)
현재 `ImeState._hangulStateCache`와 `MozcDictionary._entryCache`는 설정된 최대 개수에 도달하면 딕셔너리의 `.Clear()`를 호출하여 전체 캐시를 날리고 있습니다. 이는 직후의 캐시 미스율을 급증시켜 성능 저하를 유발합니다.

- **개선 방안**: `ConcurrentLruCache<TKey, TValue>` 클래스를 새로 구현하여 적용합니다.
- **구현 방식**: 
  - `ConcurrentDictionary`와 `LinkedList`를 조합하거나, 사이즈를 초과할 경우 최근 사용되지 않은 항목 일부만 제거하는 경량화된 정책을 구현합니다.
  - `ImeNativeCore.cs` 내부 및 `MozcGoogleDictionary.cs`의 기존 캐시 변수를 이 LRU 캐시로 교체합니다.

## 2. 키보드 훅(Global Input Hook) 지연(Input Lag) 최적화
시스템 키보드 훅(`KbdHookCallback`)은 운영체제의 입력 큐를 직접 차단(Block)하므로 처리 시간이 수 밀리초를 넘어가면 사용자는 타이핑 지연을 느낍니다.

- **문제점 진단**: 
  - `TryResolveKeyboardContext` 내에서 `ImeState.CheckHangulPublic(hFore)`를 호출하는데, 이 함수는 윈도우 메시지인 `SendMessageTimeout`을 사용하여 대상 창의 IME 상태를 조회합니다 (최대 30ms 대기). 키를 누를 때마다 최대 30ms가 지연될 수 있습니다.
- **개선 방안**:
  - `ImeState.CheckHangulPublic` 내부에서 빈번한 `SendMessageTimeout` 호출을 피하기 위해, 짧은 시간(예: 50~100ms) 동안은 이전 조회 결과를 재사용(Time-based Cache)하도록 변경합니다.
  - 키보드 훅 내부에서 무거운 텍스트 연산이나 불필요한 동기화를 줄이고, 판단이 필요한 최소한의 로직만 남긴 뒤 한자 변환 등은 완전히 백그라운드 스레드에서 처리되도록 개선합니다.
  - 훅 내부에서 사용하는 자물쇠(`lock (_compositionLock)`)의 점유 시간을 최소화합니다.

## 진행 순서
1. `ImeNativeCore.cs`에 `ConcurrentLruCache<TKey, TValue>` 클래스를 추가.
2. `ImeState` 및 `MozcDictionary`의 캐시 자료구조를 LRU 캐시로 변경.
3. `ImeState.CheckHangulPublic` 함수에 타임스탬프 기반의 짧은 캐싱 적용하여 `SendMessageTimeout` 호출 빈도 급감.
4. `GlobalInputHook`의 훅 콜백 내부 로직 정리 및 비동기성 보장.

이 계획대로 코드를 수정해도 될까요? 승인해주시면 `ImeNativeCore.cs` 및 `MozcGoogleDictionary.cs` 수정을 진행하겠습니다.
