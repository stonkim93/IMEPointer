# 장기 UX 개선 구현 계획

이전에 분석된 개선 과제 중 2건에 대한 구현 계획입니다. 앱의 사용성과 리소스 효율성을 크게 높이는 핵심 개선 사항입니다.

## 제안하는 변경 사항

### 1. DPI 불일치 해결 (포인터 크기 문제)
현재 `Program.cs`의 `RebuildStateAssets` 메서드에서 포인터 크기를 결정할 때 `GetSystemMetrics(SM_CXCURSOR)`를 기준으로 하고 있어, 다중 모니터 환경 등에서 DPI 스케일링이 변경될 때 크기 불일치가 발생할 수 있습니다.
- **수정 방안**: `GetSystemMetrics` 결과를 무시하고, 베이스 커서 크기(32px)에 현재 활성화된 모니터의 팩터(`_currentDpiScale`)를 직접 곱하여 일관된 크기를 유지하도록 수정합니다.
- **파일**: `Program.cs`

#### [MODIFY] [Program.cs](file:///d:/VSCODE/IMEPointer/Program.cs)
- `_pointerPhysicalSize` 계산 로직 변경
```csharp
// 기존
int sysCursorWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXCURSOR);
_pointerPhysicalSize = sysCursorWidth > 0 ? sysCursorWidth : Math.Max(32, (int)Math.Round(32 * _currentDpiScale));

// 변경
_pointerPhysicalSize = Math.Max(32, (int)Math.Round(32 * _currentDpiScale));
```

---

### 2. 이벤트 기반 IME 감지로 폴링 CPU 절감
현재 앱은 `System.Windows.Forms.Timer`를 이용해 매우 짧은 주기로 현재 포커스된 창의 IME 상태를 지속적으로 폴링(Polling)하고 있어 불필요한 CPU 자원을 소모합니다. 
- **수정 방안**: `SetWinEventHook` API를 사용하여 윈도우 포커스가 변경될 때(`EVENT_SYSTEM_FOREGROUND`, `EVENT_OBJECT_FOCUS`)만 IME 상태를 체크하도록 아키텍처를 변경합니다. 키보드 입력이 일어날 때도 체크하도록 전역 키보드 훅 이벤트에 연동합니다.

#### [MODIFY] [NativeMethods.cs](file:///d:/VSCODE/IMEPointer/NativeMethods.cs)
- `SetWinEventHook`, `UnhookWinEvent` P/Invoke 추가
- 상수 추가 (`EVENT_SYSTEM_FOREGROUND = 0x0003`, `EVENT_OBJECT_FOCUS = 0x8005`, `WINEVENT_OUTOFCONTEXT = 0`)
- `WinEventDelegate` 델리게이트 선언

#### [MODIFY] [Program.cs](file:///d:/VSCODE/IMEPointer/Program.cs)
- `_stateCheckTimer`를 완전히 제거하거나 사용을 중지합니다.
- `MainForm` 초기화 시 `SetWinEventHook`을 등록하여 콜백 메서드(`WinEventCallback`)를 연결합니다.
- 창 포커스가 변경되거나, 활성 창이 바뀔 때 콜백이 실행되며 여기서 기존 `ProcessStateCheck` 로직을 한 번씩만 수행하도록 변경합니다.
- 폼 소멸 시 `UnhookWinEvent`를 호출하여 리소스 누수를 방지합니다.

## 검증 계획

### 수동 검증
1. **DPI 확인**: 해상도 및 배율(예: 150%, 200%)이 다른 모니터 간 창을 이동할 때, 색상 커서의 크기가 일관적으로 스케일링 되는지 확인합니다.
2. **CPU 점유율 확인**: 작업 관리자를 열어 `IMEPointer.exe`의 CPU 점유율을 확인합니다. 기존엔 가만히 있어도 약간의 점유율이 발생했다면, 수정 후에는 타이핑이나 창 전환이 없을 때 **0%**에 수렴해야 합니다.
3. **기능 정상 동작 확인**: 창을 이동하거나, 다른 프로그램 클릭 시 인디케이터나 포인터가 제때 즉각적으로 반응하는지 확인합니다.
