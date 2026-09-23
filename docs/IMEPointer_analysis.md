# 🔍 IMEPointer 앱 분석 보고서

> 분석 일시: 2026-09-22  
> 대상 파일: `Program.cs`, `ImeNativeCore.cs`, `Config.cs`, `UIComponents.cs`, `RegistryManager.cs`, `MozcGoogleDictionary.cs`  
> 참고 문서: `docs/IMCPointer_analysis.md`, `docs/IMEJapanese_analysis_report.md`, `docs/IMEPali_개선사항.md`

---

## 📁 프로젝트 구조 요약

| 파일 | 역할 | 크기 |
|:---|:---|---:|
| `Program.cs` | 메인 폼 전체 (MainForm, 트레이, 렌더링, 타이머, 오버레이) | 1,318줄 |
| `ImeNativeCore.cs` | IME 상태 감지, 키보드 훅, 입력 처리 | 784줄 |
| `Config.cs` | 전체 설정(AppConfig, UiText, MozcConfig) | 228줄 |
| `UIComponents.cs` | 자판 배열창, 텍스트 오버레이, 한자 후보창 | 507줄 |
| `MozcGoogleDictionary.cs` | Mozc DB 로드, Google API 한자 변환 | 519줄 |
| `PointerGraphicsFactory.cs` | 커서(포인터) 비트맵 생성 | (별도) |
| `NativeMethods.cs` | Win32 P/Invoke 선언 | (별도) |
| `RegistryManager.cs` | Copilot 키맵핑 레지스트리 제어 | 121줄 |
| `KanjiConversion.cs` | Viterbi 기반 한자 변환 DP 알고리즘 | (별도) |
| `Keymaps.cs` | Pali/Engineer/Japanese 키맵 데이터 | 65KB |

> [!NOTE]
> IMEPointer는 IMEJapanese + IMEPali + IMCPointer를 통합한 올인원 앱입니다.
> 세 유사 앱의 기존 분석 보고서에서 발견된 문제들이 상당 부분 이 프로젝트에도 동일하게 존재합니다.

---

## 🔴 버그 / 잠재적 크래시

### 1. `_hangulStateCache`가 스레드 안전하지 않음

**위치**: [`ImeNativeCore.cs` L31](file:///d:/VSCODE/IMEPointer/ImeNativeCore.cs#L31)

```csharp
private static readonly Dictionary<IntPtr, bool> _hangulStateCache = new Dictionary<IntPtr, bool>();
```

> [!WARNING]
> `CheckHangulPublic`은 UI 스레드(타이머 틱)와 키보드 훅 콜백(비UI 스레드) 양쪽에서 호출됩니다.
> 일반 `Dictionary`는 스레드 안전하지 않으므로 **동시 읽기/쓰기 시 크래시** 위험이 있습니다.

**개선 방향**: `ConcurrentDictionary<IntPtr, bool>`으로 교체 (IMEJapanese #4와 동일 문제)

---

### 2. `_hangulStateCache` 전체 삭제 캐시 전략

**위치**: [`ImeNativeCore.cs` L110-L113](file:///d:/VSCODE/IMEPointer/ImeNativeCore.cs#L110-L113)

```csharp
if (_hangulStateCache.Count > MaxCacheSize)
{
    _hangulStateCache.Clear(); // 전체 삭제!
}
```

> [!WARNING]
> `MaxCacheSize(100)` 초과 시 **전체 삭제**되어, 다음 폴링 사이클에서 모든 창의 한글 상태를 재질의해야 합니다.
> 창이 많은 환경(멀티태스킹)에서 순간적인 응답 지연 발생 가능합니다.

**개선 방향**: LRU 방식 캐시(`ConcurrentDictionary` + `Queue` 조합)로 교체 (IMCPointer #1과 동일 문제)

---

### 3. `EvaluateTargetProcess`에서 `Process` 핸들 누수

**위치**: [`Program.cs` L857](file:///d:/VSCODE/IMEPointer/Program.cs#L857)

```csharp
string n = System.Diagnostics.Process.GetProcessById((int)pid).ProcessName;
```

> [!CAUTION]
> `Process.GetProcessById()`가 반환하는 객체를 `Dispose()`하지 않아 **OS 핸들 누수**가 발생합니다.
> 100ms마다 폴링하므로 누수가 지속적으로 쌓입니다.

**개선 방향**:
```csharp
using var proc = System.Diagnostics.Process.GetProcessById((int)pid);
string n = proc.ProcessName;
```

---

### 4. `MozcDictionary.LoadDictionary()`의 경쟁 조건 (Race Condition)

**위치**: [`MozcGoogleDictionary.cs` L59-L61](file:///d:/VSCODE/IMEPointer/MozcGoogleDictionary.cs#L59-L61)

```csharp
public static void LoadDictionary()
{
    if (IsLoaded) return;  // ← 잠금 없음!
    ...
    IsLoaded = true;
}
```

> [!WARNING]
> `IsLoaded` 체크와 실제 로드 사이에 **락이 없어** 동시에 두 번 호출될 경우 중복 실행 가능성이 있습니다.
> `SqliteConnection`이 이중 생성되거나 `_transitionMatrix`가 이중 초기화될 수 있습니다.

**개선 방향**: `lock`, `Lazy<T>`, 또는 `SemaphoreSlim(1, 1)` 패턴 적용 (IMEJapanese #3과 동일 문제)

---

### 5. `WndProc`에서 `Task.Delay` 남용

**위치**: [`Program.cs` L588](file:///d:/VSCODE/IMEPointer/Program.cs#L588)

```csharp
protected override void WndProc(ref Message m)
{
    if (m.Msg == WindowPosChangedMessage) 
        Task.Delay(200).ContinueWith(_ => this.BeginInvoke(...));
    base.WndProc(ref m);
}
```

> [!WARNING]
> `WM_WINDOWPOSCHANGED`는 창 이동/리사이즈마다 **매우 빈번하게** 발생합니다.
> `Task.Delay`를 매번 생성하면 ThreadPool 태스크가 폭증할 수 있습니다.
> `OnDisplaySettingsChanged`, `OnUserPreferenceChanged`에서도 동일 패턴 반복 (L618, L626).

**개선 방향**: 디바운싱 패턴 적용 (`System.Threading.Timer` 재사용)
```csharp
private System.Threading.Timer? _rebuildDebounceTimer;
// WndProc에서:
_rebuildDebounceTimer?.Change(200, Timeout.Infinite);
```

---

### 6. `async void` 트레이 메뉴 이벤트 핸들러의 예외 처리 미흡

**위치**: [`Program.cs` L441](file:///d:/VSCODE/IMEPointer/Program.cs#L441)

```csharp
_menuItemUseMozc = new ToolStripMenuItem("Mozc 오프라인 한자변환", null, async (s, e) =>
{
    using (var client = new System.Net.Http.HttpClient())
    { ... }
});
```

> [!WARNING]
> `async void` 핸들러에서 예외가 발생하면 **캐치 불가**하여 앱 크래시 위험이 있습니다.
> `HttpClient`를 매번 `new`로 생성하는 것은 소켓 고갈(Socket Exhaustion) 위험이 있습니다.

**개선 방향**:
- `HttpClient`를 `static readonly` 필드로 클래스 레벨에 선언
- `async void` → 별도 `async Task` 메서드로 분리 후 `_ = DownloadDictionaryAsync()` 형태로 호출

---

## 🟠 안정성 / 스레드 안전성

### 7. `volatile bool`의 경쟁 조건 위험 (ImeNativeCore)

**위치**: [`ImeNativeCore.cs`](file:///d:/VSCODE/IMEPointer/ImeNativeCore.cs) 내 `IsSending`, `IsReplacingSelection` 플래그

```csharp
public static volatile bool IsSending = false;
```

> [!NOTE]
> `volatile`은 단일 필드의 가시성만 보장하며, `Check-Then-Act` 패턴에서 **원자성을 보장하지 않습니다**.
> 두 스레드가 동시에 `if (!IsSending) { IsSending = true; ... }`를 통과할 수 있습니다.

**개선 방향**: `Interlocked.CompareExchange` 또는 `SemaphoreSlim` 적용 (IMEPali #1-2와 동일 문제)

---

### 8. `Task.Delay().ContinueWith()` 패턴의 일관성 부재

세 곳에서 같은 패턴이 반복되지만 오류 처리 방식이 제각각입니다.

| 위치 | 패턴 |
|:---|:---|
| `WndProc` (L588) | `Task.Delay(200).ContinueWith(...)` |
| `OnDisplaySettingsChanged` (L618) | `Task.Delay(400).ContinueWith(...)` |
| `OnUserPreferenceChanged` (L626) | `Task.Delay(600).ContinueWith(...)` |
| `EnforceCapsModeToTarget` (L710) | `Task.Delay(60).ContinueWith(...)` |

**개선 방향**: 헬퍼 메서드 또는 `async/await` 패턴으로 통일
```csharp
private async void RebuildAfterDelay(int ms)
{
    await Task.Delay(ms);
    RebuildAssetsWithRetry(0);
}
```

---

## 🟡 성능 개선

### 9. 100ms 폴링 vs 이벤트 기반 하이브리드 방식

현재 100ms 타이머 폴링으로 IME 상태, 포그라운드 창, 한글 모드를 매번 확인합니다.

매 틱마다 발생하는 Win32 API 호출:
- `GetForegroundWindow()`
- `GetWindowThreadProcessId()`
- `GetKeyboardLayout()`
- `ImmGetDefaultIMEWnd()` + `SendMessageTimeout()` (한글 모드 확인)
- `GetCursorPos()` (인디케이터)
- `GetClassName()` (작업표시줄/앱 구분)

**개선 방향**:
- `WM_INPUTLANGCHANGE` 훅으로 IME 언어 변경 즉각 감지
- 포커스 변경이 없을 때 한글 상태 캐시 적극 재사용 (현재 일부 구현되어 있으나 불완전)
- 폴링 간격을 200ms로 증가, 성능 향상

---

### 10. `_entryCache` 크기 제한 없는 무한 성장 위험

**위치**: [`MozcGoogleDictionary.cs` L24-L25](file:///d:/VSCODE/IMEPointer/MozcGoogleDictionary.cs#L24-L25)

```csharp
private static readonly ConcurrentDictionary<string, List<KanjiEntry>> _entryCache = new(...);
private const int MaxCacheSize = 5000;
```

`MaxCacheSize`가 선언되어 있지만 실제 **캐시 제거 로직이 적용되는지 확인 필요**합니다. 캐시 초과 시 LRU 방식의 제거가 이루어지지 않으면 메모리가 무한 증가할 수 있습니다.

---

### 11. `KeyboardLayoutForm` 매직 넘버 (ExStyle)

**위치**: [`UIComponents.cs` L27-L30](file:///d:/VSCODE/IMEPointer/UIComponents.cs#L27-L30)

```csharp
cp.Style  |= 0x00020000;  // 의미 불명
cp.Style  |= 0x00080000;
cp.ExStyle |= 0x00040000;
cp.ExStyle |= 0x08000000;
```

그리고 `MainForm.CreateParams` (L237):
```csharp
cp.ExStyle |= 0x00000080 | 0x00000020 | 0x00080000 | 0x08000000 | 0x00000008;
```

**개선 방향**: `NativeMethods.cs`에 상수 정의
```csharp
public const int WS_EX_TOOLWINDOW  = 0x00000080;
public const int WS_EX_NOACTIVATE  = 0x08000000;
public const int WS_EX_LAYERED     = 0x00080000;
public const int WS_EX_TRANSPARENT = 0x00000020;
```

---

## 🔵 코드 품질 / 유지보수성

### 12. 설정 영속성 없음 (가장 중요한 UX 문제)

**위치**: [`Config.cs` L117-L132](file:///d:/VSCODE/IMEPointer/Config.cs#L117-L132)

```csharp
public static int DefaultCapsMode = 1;           // 하드코딩
public static bool DefaultShowKeyboardLayout = true; // 하드코딩
public static int DefaultPointerMode = 2;         // 하드코딩
public static bool DefaultEnableMiniIndicator = true; // 하드코딩
```

사용자가 트레이 메뉴에서 설정을 변경해도 **앱 재시작 시 초기화**됩니다.

**개선 방향**: `RegistryManager`를 활용하여 설정 영속화 (이미 Copilot 키맵핑용 레지스트리 코드 존재)
```csharp
// HKCU\Software\IMEPointer 하위에 저장
Registry.CurrentUser.SetValue("PointerMode", AppConfig.DefaultPointerMode);
Registry.CurrentUser.SetValue("CapsMode", AppConfig.DefaultCapsMode);
```

---

### 13. `Program.cs` 단일 파일 비대화 (1,318줄)

현재 `Program.cs`에 아래 역할이 모두 혼재합니다:

| 클래스/기능 | 역할 |
|:---|:---|
| `MainForm` 진입점 | WinForms 앱 실행 |
| 트레이 메뉴 구성 | `BuildTrayMenu()`, `SyncXxxMenuChecks()` |
| 포인터 모드 제어 | `UpdatePointerMode()`, `ApplyVisualState()` |
| 상태 감지 루프 | `ProcessStateCheck()` (100ms 타이머) |
| IME 한글 상태 동기화 | `SyncSystemHangulState()` |
| 커서 에셋 빌드 | `RebuildStateAssets()`, `BuildTrayIcon()` |
| 오버레이 표시 | `ShowOverlay()`, `ExecuteShowOverlay()` |
| 미니 인디케이터 렌더링 | `RenderMiniIndicator()`, `UpdateLayeredIndicator()` |
| 자판 배열창 관리 | `RefreshKeyboardLayoutOverlay()` |

**개선 방향**: 최소한 아래 구조로 분리 권장
```
PointerManager.cs       ← 포인터 에셋 빌드 및 적용
StateTracker.cs         ← IME 상태 감지 타이머 루프
IndicatorRenderer.cs    ← 미니 인디케이터 렌더링
TrayMenuBuilder.cs      ← 트레이 메뉴 구성 로직
```

---

### 14. 예외 삼킴 (Silent Exception Swallowing) 다수

```csharp
catch { }  // Program.cs 여러 곳
```

발견된 위치: L57, L316, L357, L401, L863, L958, L1004, L1016, L1272 등

> [!NOTE]
> 예외를 조용히 무시하면 문제 발생 시 디버깅이 매우 어렵습니다.

**개선 방향**: 최소한 `Debug.WriteLine` 또는 기존 `Trace` 리스너에 기록
```csharp
catch (Exception ex)
{
    if (AppConfig.LogLevel >= 1) 
        Trace.WriteLine($"[IMEPointer] {nameof(EvaluateTargetProcess)}: {ex.Message}");
}
```

---

### 15. `RefreshKeyboardLayoutOverlay` 내 가상 키코드 매직 넘버

**위치**: [`Program.cs` L1084-L1085](file:///d:/VSCODE/IMEPointer/Program.cs#L1084-L1085)

```csharp
bool isPhyShift = (NativeMethods.GetKeyState(0x10) & 0x8000) != 0;
if (AppConfig.EnableCopilotMap && 
    ((NativeMethods.GetKeyState(0x5B) & 0x8000) != 0 || 
     (NativeMethods.GetKeyState(0x5C) & 0x8000) != 0)) 
    isPhyShift = false;
```

`0x10`(Shift), `0x5B`(LWin), `0x5C`(RWin) 등 가상 키코드가 숫자로만 사용됩니다.

**개선 방향**: `NativeMethods`에 이미 `VK_CAPITAL` 상수가 있으므로 나머지도 통일
```csharp
public const int VK_SHIFT = 0x10;
public const int VK_LWIN  = 0x5B;
public const int VK_RWIN  = 0x5C;
```

---

### 16. `JapaneseHangul1`과 `JapaneseHangul2`의 테마 중복

**위치**: [`Config.cs` L181-L182](file:///d:/VSCODE/IMEPointer/Config.cs#L181-L182)

```csharp
[ImeState.State.JapaneseHangul1] = new Theme { ..., TrayText = "J", ... },
[ImeState.State.JapaneseHangul2] = new Theme { ..., TrayText = "J", ... },
```

두 상태가 동일한 트레이 텍스트("J")를 사용하여 **시각적으로 구분이 불가**합니다.

**개선 방향**: 구분 가능한 텍스트 적용
```csharp
JapaneseHangul1 → TrayText = "J1"
JapaneseHangul2 → TrayText = "J2"  
JapaneseHangul3 → TrayText = "J3"
```

---

### 17. 버전 정보 표시 없음

트레이 메뉴 타이틀에 현재 앱 버전이 표시되지 않습니다.

**개선 방향**:
```csharp
// UiText에 추가
public static string VersionInfo =>
    $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}";
```
트레이 메뉴 타이틀: `"IMEPointer v1.x.x"` 형태로 표시

---

## 🎨 UX / 기능 개선

### 18. 키보드 배열창 위치/크기 영속성 없음

```csharp
private Point _lastKeyboardLayoutLocation = Point.Empty;
```

현재 창 위치는 세션 내에서만 기억되며, **앱 재시작 시 초기 위치**로 돌아옵니다.

**개선 방향**: 창 위치를 레지스트리에 저장/복원 (설정 영속성 #12와 연계)

---

### 19. 트레이 아이콘 ToolTipText 동적 미활용

현재 트레이 아이콘 툴팁이 IME 상태 설명 문자열로만 업데이트되나, **앱 시작 전까지 사전 로드 중 상태가 반영 불완전**합니다.

**개선 방향**: 사전 로드 중 → "IMEPointer: 사전 로드 중..." / 완료 후 → 현재 입력 상태로 동적 전환

---

### 20. 색상 커스터마이징 불가

5가지 상태 이상의 색상이 `Config.cs`에 하드코딩되어 있습니다.

**개선 방향**: 설정 파일 또는 색상 선택 다이얼로그 지원 (장기 로드맵)

---

## 📐 기술 난제: DPI 불일치 (포인터 크기)

**위치**: [`Program.cs` L973-L974](file:///d:/VSCODE/IMEPointer/Program.cs#L973-L974)

```csharp
int sysCursorWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXCURSOR);
_pointerPhysicalSize = sysCursorWidth > 0 ? sysCursorWidth : Math.Max(32, (int)Math.Round(32 * _currentDpiScale));
```

`SM_CXCURSOR`는 DPI 배율 **이전** 논리값이므로, DWM이 추가로 확대하는 크기와 맞지 않습니다.

### 권장 해결책 (IMCPointer 분석과 동일)

```csharp
// 방법: 렌더 크기를 32px 고정 → SetSystemCursor가 DPI 스케일링 자동 처리
int renderSize = 32;
// SetSystemCursor()가 등록 시 시스템 DPI 배율에 맞게 자동 확대
```

> [!TIP]
> 현재 `GetDpiForMonitor`로 DPI를 구한 후 `_currentDpiScale`을 계산하는 코드는 있지만,
> 커서 렌더 크기가 `SM_CXCURSOR`에 의존하는 부분이 일관성을 해칩니다.
> `_pointerPhysicalSize = 32`로 고정하고 `SetSystemCursor`에 스케일링을 맡기는 방식이 가장 안정적입니다.

---

## ✅ 개선 우선순위 요약

| 우선순위 | 항목 | 난이도 | 효과 |
|:---:|:---|:---:|:---:|
| 🔴 즉시 | `Process` 핸들 누수 수정 (#3) | 쉬움 | 안정성 |
| 🔴 즉시 | `_hangulStateCache` → `ConcurrentDictionary` (#1) | 쉬움 | 안정성 |
| 🔴 즉시 | `LoadDictionary` 중복 실행 방지 lock (#4) | 쉬움 | 안정성 |
| 🟠 단기 | `WndProc` 디바운싱 (#5) | 중간 | 안정성 |
| 🟠 단기 | `async void` HttpClient 개선 (#6) | 중간 | 안정성 |
| 🟡 중기 | 설정 영속성 추가 (#12) | 중간 | UX |
| 🟡 중기 | `JapaneseHangul` 테마 구분 (#16) | 쉬움 | UX |
| 🟡 중기 | 버전 정보 표시 (#17) | 쉬움 | UX |
| 🟡 중기 | 매직 넘버 상수화 (#11, #15) | 쉬움 | 유지보수성 |
| 🟡 중기 | 예외 로깅 개선 (#14) | 쉬움 | 디버깅 |
| 🟢 장기 | DPI 불일치 해결 (기술 난제) | 어려움 | 핵심 버그 |
| 🟢 장기 | `Program.cs` 파일 분리 리팩토링 (#13) | 높음 | 유지보수성 |
| 🔵 장기 | 이벤트 기반 IME 감지 (#9) | 어려움 | 성능 |
| 🔵 장기 | 색상 커스터마이징 (#20) | 어려움 | 기능 확장 |

---

## 🔗 유사 앱 비교 (공통 발생 문제)

| 문제 유형 | IMCPointer | IMEJapanese | IMEPali | IMEPointer |
|:---|:---:|:---:|:---:|:---:|
| `_hangulStateCache` 전체 삭제 | ✅ | ✅ | - | ✅ |
| `Process` 핸들 누수 | ✅ | - | - | ✅ |
| 설정 영속성 없음 | ✅ | ✅ | ✅ | ✅ |
| `async void` 예외 위험 | - | ✅ | ✅ | ✅ |
| Magic Number 남용 | ✅ | ✅ | ✅ | ✅ |
| 예외 삼킴 (catch {}) | - | - | ✅ | ✅ |
| 버전 정보 미표시 | ✅ | - | - | ✅ |
| `LoadDictionary` Race Condition | - | ✅ | - | ✅ |
| 폴링 CPU 낭비 | ✅ | ✅ | ✅ | ✅ |
