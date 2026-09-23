# IMEPali 앱 분석 및 개선사항 보고서

> 분석 대상: `Program.cs` (1,242 Lines), `IMEPali.csproj`, `README.md`  
> 분석 일시: 2026-09-22

---

## 📊 현황 요약

| 항목 | 내용 |
|:---|:---|
| **아키텍처** | 단일 파일(`Program.cs`)에 9개 클래스 구성 |
| **주요 기술** | Win32 P/Invoke, 글로벌 키보드 훅, UI Automation, WinForms |
| **특이사항** | IMEPointer와 Mutex 공유 (동시 실행 방지) |
| **버전** | 1.0.1.0 |

---

## 🔴 1. 버그 / 안정성 문제

### 1-1. `_transformationChains["s"]` 이중 초기화 버그

```csharp
// Program.cs L97 — 정적 선언
{"s", new string?[]{"s", "ṣ", null, null, "ś", null, null}},

// Program.cs L105 — 정적 생성자에서 덮어쓰기
_transformationChains["s"] = new string?[] { "s", "ṣ", null, null, null, "ś", null };
```

- **문제**: `s`의 변환 체인이 선언부(인덱스 4)와 생성자(인덱스 5) 두 곳에서 **서로 다르게** 정의되어 있음. 선언부는 사실상 무시됨.
- **개선**: 정적 선언부에서 `"s"` 항목을 **아예 제거**하고 생성자에서만 정의하거나, 생성자의 오버라이드 라인을 제거하고 선언부에서 올바른 값(`null, null, null, null, null, "ś", null`)으로 정의해야 함.

### 1-2. `volatile bool`의 경쟁 조건 (Race Condition)

```csharp
// KeyboardHookManager.cs L205
public static volatile bool IsSendingInput = false;

// ClipboardUtility.cs L402
public static volatile bool IsProcessing = false;
```

- **문제**: `volatile`은 가시성만 보장하며, `Check-Then-Act` 패턴에서 **원자성을 보장하지 않음**. 두 스레드가 동시에 `if (!IsProcessing) { IsProcessing = true; ... }` 를 통과할 수 있음.
- **개선**: `Interlocked.CompareExchange` 또는 `lock` 패턴으로 교체.

```csharp
// 개선 예시 (ClipboardUtility)
private static int _isProcessing = 0;
if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) != 0) return;
try { /* ... */ }
finally { Interlocked.Exchange(ref _isProcessing, 0); }
```

### 1-3. `SendReplacementText`의 스레드 안전성

```csharp
// L334
IsSendingInput = true;
// ... 동기 작업 ...
IsSendingInput = false;
```

- **문제**: `SendString`은 `Task.Run` 비동기로 처리하지만, `SendReplacementText`는 **동기**로 처리하면서 `IsSendingInput = false`를 즉시 설정함. 비동기 `SendString`이 아직 실행 중일 때 동기 작업이 시작되면 플래그가 꼬일 수 있음.
- **개선**: 비동기 작업의 완료를 추적하는 `TaskCompletionSource` 또는 `SemaphoreSlim` 도입 검토.

### 1-4. `RestoreClipboardTextAsync`의 `async void` 패턴

```csharp
// L577
private static async void RestoreClipboardTextAsync(string? savedText)
```

- **문제**: `async void`는 예외가 발생하면 **캐치 불가**하여 앱 크래시 위험이 있음.
- **개선**: `async Task`로 변경 후 호출부에서 `_ = RestoreClipboardTextAsync(...)` 형태로 사용.

### 1-5. 클립보드 복원 지연 시간 하드코딩 (400ms)

```csharp
await Task.Delay(400); // L579
```

- **문제**: 시스템 부하에 따라 400ms가 부족할 수 있으며, 반대로 너무 길어 UX를 저하시킬 수 있음.
- **개선**: 클립보드 변경 감지 루프(`WaitForClipboard`) 방식으로 교체 검토.

---

## 🟡 2. 성능 문제

### 2-1. 트레이 아이콘 GDI 리소스 누수

```csharp
// L903~906
IntPtr hIcon = bmp.GetHicon();
Icon? previousIcon = _trayIcon.Icon;
_trayIcon.Icon = Icon.FromHandle(hIcon);
if (previousIcon != null) NativeMethods.DestroyIcon(previousIcon.Handle);
```

- **문제**: `Icon.FromHandle(hIcon)`로 생성한 아이콘의 **원본 `hIcon` 핸들**은 별도로 `DestroyIcon`을 호출해야 함. 현재 코드는 **이전 아이콘의 핸들**을 해제하지만, `FromHandle`로 생성된 새 아이콘의 `hIcon`은 해제하지 않아 **GDI 핸들 누수** 발생.
- **개선**:

```csharp
// 개선 예시
IntPtr hIcon = bmp.GetHicon();
Icon newIcon = Icon.FromHandle(hIcon);
Icon? old = _trayIcon.Icon;
_trayIcon.Icon = newIcon;
old?.Dispose();
NativeMethods.DestroyIcon(hIcon); // 원본 hIcon 반드시 해제
```

### 2-2. 폴링 타이머 간격 최적화

```csharp
public static readonly int TrayUpdateIntervalMs = 100; // AppConfig L33
```

- **문제**: 100ms마다 `GetForegroundWindow`, `GetGUIThreadInfo`, `ImmGetDefaultIMEWnd`, `SendMessageTimeout`을 호출하는 것은 **불필요한 CPU 낭비**.
- **개선**: `WH_CALLWNDPROC`/`WM_INPUTLANGCHANGE` 훅으로 IME 상태 변경 이벤트 방식으로 전환하거나, 간격을 500ms로 늘리는 것을 검토.

### 2-3. 클립보드 텍스트 읽기 루프의 Busy-Wait

```csharp
for (int i = 0; i < 20; i++)
{
    Thread.Sleep(20); // 최대 400ms 대기
    copiedText = GetClipboardText();
    if (!string.IsNullOrEmpty(copiedText)) break;
}
```

- **문제**: 최악의 경우 400ms를 `Thread.Sleep`으로 낭비. UI 스레드 블록 위험.
- **개선**: `AddClipboardFormatListener` Win32 API를 사용한 이벤트 기반 감지로 전환.

---

## 🔵 3. 코드 품질 / 유지보수성

### 3-1. 단일 파일에 과도한 책임 집중 (1,242 Lines)

- **문제**: `Program.cs` 하나에 9개 클래스 (`AppConfig`, `Program`, `PaliMap`, `KeyboardHookManager`, `ClipboardUtility`, `RegistryHelper`, `TrayMainForm`, `KeyboardLayoutForm`, `TextOverlayForm`, `NativeMethods`)가 모두 포함.
- **개선**: 각 클래스를 독립 파일로 분리. 최소한 아래 구조를 권장:

```
IMEPali/
├── Core/
│   ├── AppConfig.cs
│   ├── PaliMap.cs
│   └── KeyboardHookManager.cs
├── Utilities/
│   ├── ClipboardUtility.cs
│   └── RegistryHelper.cs
├── UI/
│   ├── TrayMainForm.cs
│   ├── KeyboardLayoutForm.cs
│   └── TextOverlayForm.cs
├── Native/
│   └── NativeMethods.cs
└── Program.cs
```

### 3-2. 예외 삼킴 (Silent Exception Swallowing)

```csharp
catch { } // 여러 곳에서 발견
catch (Exception) { }
```

- **문제**: `HookCallback` (L325), `ReadSelectedText` (L479), `RenderImage` (L1078) 등 **10여 곳**에서 예외를 완전히 무시. 디버깅이 매우 어려움.
- **개선**: 최소한 `Debug.WriteLine` 또는 경량 로거(예: `Serilog` 파일 로그, 또는 `EventLog`) 적용.

```csharp
catch (Exception ex)
{
    Debug.WriteLine($"[IMEPali] {nameof(ReadSelectedText)} 오류: {ex.Message}");
}
```

### 3-3. 매직 넘버 / 가상 키코드 상수화 미비

```csharp
// 여러 곳에서 발견
virtualKeyCode == 0x14  // Caps Lock
virtualKeyCode == 0x5B  // LWin
virtualKeyCode == 0x10  // Shift
0xA3                    // RCtrl
0x19                    // Hanja
```

- **개선**: `NativeMethods` 또는 별도 `VirtualKeys` 정적 클래스에 상수로 정의.

```csharp
internal static class VK
{
    public const int SHIFT   = 0x10;
    public const int CAPITAL = 0x14; // Caps Lock
    public const int HANJA   = 0x19;
    public const int LWIN    = 0x5B;
    public const int RCONTROL = 0xA3;
}
```

### 3-4. `TrayMainForm`의 트레이 메뉴 항목 검색이 취약

```csharp
// L962~964
if (item.Text == "Pali어 키보드 배열창") ((ToolStripMenuItem)item).Checked = false;
```

- **문제**: 하드코딩된 문자열로 메뉴 항목을 찾음. 텍스트 변경 시 버그 발생.
- **개선**: `keyboardLayoutMenu` 변수를 `TrayMainForm`의 **필드**로 승격시켜 직접 참조.

### 3-5. README의 `7️⃣` 번호 중복

```markdown
### 7️⃣ 윈도우 시작 프로그램에 추가하기  ← L251
### 7️⃣ 한글자음+한자키 특수기호 입력하기  ← L262
```

- **문제**: `7️⃣` 번호가 두 번 사용됨. 두 번째는 `8️⃣`이 되어야 함.

---

## 🟢 4. UX / 사용성 개선

### 4-1. 설정 영속성 없음 (앱 재시작 시 초기화)

```csharp
public static bool ShowKeyboardLayout = true;  // AppConfig
public static bool ShowTextOverlay = true;
```

- **문제**: 사용자가 설정을 변경해도 앱 재시작 시 **기본값으로 초기화**.
- **개선**: `System.Configuration` 또는 `RegistryHelper`를 활용하여 설정을 영속화.

```csharp
// HKCU\Software\IMEPali 하위에 저장
Registry.CurrentUser.SetValue("ShowKeyboardLayout", AppConfig.ShowKeyboardLayout);
```

### 4-2. 오버레이 창 위치 부정확

```csharp
_overlayForm.Display(text, true, 22f, dynamicWidth, 52, caretLocation.X, caretLocation.Y + 40);
```

- **문제**: 캐럿 위치를 못 찾으면 `Point.Empty (0, 0)` 즉, **화면 좌상단**에 오버레이가 표시됨.
- **개선**: `Point.Empty`인 경우 마우스 위치 또는 포커스 창 중앙으로 폴백 처리.

### 4-3. 오버레이 표시 시간 하드코딩

```csharp
_visibilityTimer = new System.Windows.Forms.Timer { Interval = 1500 }; // TextOverlayForm
```

- **개선**: `AppConfig`에 `OverlayDisplayMs` 설정값으로 노출하여 사용자가 트레이 메뉴에서 조정 가능하도록.

### 4-4. 키보드 배열창 위치/크기 영속성 없음

- **문제**: 창을 이동해도 재시작 시 초기 위치로 돌아옴.
- **개선**: 창 위치/크기를 레지스트리에 저장 후 복원.

### 4-5. 트레이 아이콘 `ToolTipText` 미활용

```csharp
_trayIcon = new NotifyIcon { ..., Text = "IMEPali - Pali Input System" };
```

- **개선**: 현재 입력 상태(한글/영어)를 `Text`에 동적으로 반영 (예: `"IMEPali — 한글 입력 중"` / `"IMEPali — Pali 입력 가능"`).

---

## 🟣 5. 기능 확장 제안

### 5-1. README의 기술적 난제 해결: Caps Lock On 시 대문자 Pali 자동 입력

```
⚠️ Caps Lock On시 Shift 없이 해당 기호 입력 가능하도록 하고 싶다. (README L355)
```

현재 코드:
```csharp
bool isCapsOn = (NativeMethods.GetKeyState(0x14) & 0x0001) != 0;
bool isUpperCase = isShiftDown ^ isCapsOn; // L289
```

- 이미 XOR 로직으로 `Caps Lock ON + Shift 없음 = 대문자` 처리를 하고 있음.
- **실제 문제**: `SendUnicodeString`이 KEYBD_EVENT로 유니코드를 전송할 때 `Shift` 상태와 무관하게 전송되므로 **이론상 이미 동작**해야 함.
- **디버깅 포인트**: `GetPaliCharacter` 호출 전 `isUpperCase` 값이 올바른지 로그로 확인 필요.

### 5-2. 설정 화면 (트레이 우클릭 > 설정) 추가

| 설정 항목 | 타입 |
|:---|:---|
| 오버레이 표시 시간 (ms) | Slider / NumericUpDown |
| 키보드 배열창 불투명도 | Slider |
| 시작 프로그램 자동 등록 | Checkbox |
| 입력 로그 파일 저장 | Checkbox |

### 5-3. 다국어 문자 지원 확장

- 현재: Pali + Sanskrit (14개 특수문자)
- 제안: 그리스어 (θ, φ, ψ 등), 히브리어 자모 등 동양 고전 언어 문자 추가 옵션 (플러그인 방식).

### 5-4. 로깅 시스템 도입

```csharp
// 권장 구조
catch (Exception ex)
{
    Logger.Write(LogLevel.Warning, ex.Message);
}
```

- 경량 파일 로거(예: `Serilog`, 또는 자체 구현)를 `AppData\Local\IMEPali\logs\` 에 저장.
- 트레이 메뉴에 "로그 폴더 열기" 항목 추가.

---

## ✅ 개선 우선순위 요약

| 순위 | 항목 | 난이도 | 영향도 |
|:---:|:---|:---:|:---:|
| ⭐⭐⭐ | GDI 핸들 누수 수정 (2-1) | 낮음 | 높음 |
| ⭐⭐⭐ | `_transformationChains["s"]` 이중 초기화 버그 수정 (1-1) | 낮음 | 높음 |
| ⭐⭐⭐ | 설정 영속성 추가 (4-1) | 중간 | 높음 |
| ⭐⭐ | `volatile` → `Interlocked` 경쟁 조건 수정 (1-2) | 중간 | 중간 |
| ⭐⭐ | `async void` → `async Task` 수정 (1-4) | 낮음 | 중간 |
| ⭐⭐ | 예외 로깅 추가 (3-2) | 낮음 | 중간 |
| ⭐⭐ | 매직 넘버 상수화 (3-3) | 낮음 | 낮음 |
| ⭐ | 파일 분리 리팩토링 (3-1) | 높음 | 낮음 |
| ⭐ | 폴링 → 이벤트 기반 전환 (2-2) | 높음 | 중간 |
| ⭐ | 트레이 메뉴 텍스트 하드코딩 제거 (3-4) | 낮음 | 낮음 |
