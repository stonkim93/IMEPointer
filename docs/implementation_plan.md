# Implement Pointer and Indicator Features from Code_IMEPointer

이 계획은 기존 `Code_IMEPointer` 폴더에 구현되어 있던 포인터 커스텀 기능과 엑셀/한글 작은원 표시(Indicator) 기능, 그리고 한글_Default(WinDefault) 캡스 모드를 현재 앱에 병합하기 위한 계획입니다.

## User Review Required

> [!WARNING]
> 현재 앱(`IMEJapanese`)에 이미 많은 한자 변환 및 키맵핑 후킹 로직이 추가되어 있습니다. `Code_IMEPointer`의 포인터 가로채기(SystemParametersInfo 등) 및 오버레이 렌더링 로직을 가져와 합칠 때 성능 저하(깜빡임 등)가 발생하는지 추후 확인이 필요합니다. 

## Open Questions

없습니다. 지시하신 대로 `Code_IMEPointer/Program.cs`를 참조하여 기존 코드를 복원하겠습니다.

## Proposed Changes

### 1. `AppConfig` 업데이트 (Config.cs)
`Code_IMEPointer`의 설정값들을 `IMEPointer\Config.cs` 내 `AppConfig` 클래스에 복원합니다.
- 포인터 메뉴 활성화 옵션 (`ShowPointerWinDefault`, `ShowPointerWinColor`, `ShowPointerNewColor`)
- 엑셀/한글 작은원(Indicator) 표시 옵션 및 사이즈/타겟 앱 리스트 (`ShowSmallCircleMenu`, `IndicatorTargetApps`, `IndicatorSize`, `IndicatorOffset`)
- 캡스모드 한글(Default) 옵션 (`ShowCapsHangul`)
- 초기값 (`DefaultPointerMode`)

### 2. MainForm 상태 변수 및 에셋 (Program.cs)
`Program.cs` 내 `MainForm` 클래스에 포인터와 인디케이터 관리를 위한 상태 변수와 메서드를 복원합니다.
- `PointerMode` 열거형 및 현재 상태 변수(`_activePointerMode`)
- `CapsMode`에 `Hangul` (또는 `WinDefault`) 추가
- `StateAssets` 클래스에 포인터용 핸들(`ArrowNewPtr`, `IBeamNewPtr` 등) 변수 추가 및 해제(`Dispose`) 로직 추가
- 인디케이터 렌더링용 GDI 변수(`_dcIndicatorScreen`, `_hBmpIndicator` 등)

### 3. 트레이 메뉴 연결 (Program.cs)
`BuildTrayMenu()`에서 `TODO`로 남겨둔 메뉴들의 핸들러를 실제 구현체로 연결합니다.
- "WIN Default Pointer" -> `UpdatePointerMode(PointerMode.WinDefault)`
- "WIN Color Pointer" -> `UpdatePointerMode(PointerMode.WinColor)`
- "NEW Color Pointer" -> `UpdatePointerMode(PointerMode.NewColor)`
- "한글_Default" -> `UpdateCapsMode(CapsMode.WinDefault)`
- "엑셀/한글 작은원 표시" -> `_isMiniIndicatorEnabled` 토글 및 설정 반영

### 4. 핵심 포인터/인디케이터 로직 (Program.cs)
기존 `Code_IMEPointer/Program.cs`에서 사용된 핵심 메서드들을 가져옵니다.
- `UpdatePointerMode()`: 포인터 상태 갱신
- `EvaluatePointerIsIBeam()`, `EvaluatePointerIsArrow()`: 현재 마우스 포인터 상태 감지 (IBeam인지 Arrow인지 등)
- `RenderMiniIndicator()`, `UpdateLayeredIndicator()`, `RenderIndicatorBuffer()`: 엑셀과 한글 등 특정 앱에서 텍스트 커서 옆에 작은 원(Indicator)을 띄우는 로직
- `RebuildStateAssets()` 및 `ApplyVisualState()`: 포인터 아이콘 교체 로직 병합

### 5. `NativeMethods` 및 `ImeNativeCore` 보완 (필요 시)
`Code_IMEPointer`에서 사용하던 Win32 API 중 현재 누락된 것이 있다면(예: `CopyIcon`, `GetCursorInfo`, `UpdateLayeredWindow` 등) 확인 후 추가합니다.

## Verification Plan

### Manual Verification
1. 앱 빌드 및 실행 후 트레이 메뉴에서 "NEW Color Pointer"를 선택했을 때 마우스 포인터(텍스트 커서 및 기본 커서)가 색상이 적용된 커스텀 모양으로 변하는지 확인.
2. 엑셀(Excel) 혹은 한글(HWP) 창을 띄운 상태에서 "엑셀/한글 작은원 표시" 기능을 켜면, 텍스트 커서 옆에 상태에 맞는 작은 색상 원이 따라다니는지 확인.
3. 한글_Default 캡스모드로 전환 시 기존 한글 동작에 문제 없는지 확인.
