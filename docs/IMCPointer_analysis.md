# 🔍 IMCPointer 앱 분석 보고서

> 분석 일시: 2026-09-22  
> 대상 파일: `Program.cs`, `ImeNativeCore.cs`, `NativeMethods.cs`, `IMCPointer.csproj`

---

## 📁 프로젝트 구조 요약

| 파일 | 역할 | 크기 |
|:---|:---|---:|
| `Program.cs` | 메인 로직 전체 (AppConfig, UI, 렌더링, 타이머) | 1038줄 |
| `ImeNativeCore.cs` | IME 상태 감지 및 설정 | 141줄 |
| `NativeMethods.cs` | Win32 API P/Invoke 선언 | 90줄 |
| `IMCPointer.csproj` | 빌드 설정 (Store / 일반 토글) | 54줄 |

---

## 🐛 버그 / 잠재적 문제점

### 1. `_hangulStateCache` 캐시 전략 취약

**위치**: [`ImeNativeCore.cs` L85-L88](file:///d:/VSCODE/IMCPointer/ImeNativeCore.cs#L85-L88)

```csharp
// 현재 코드
if (_hangulStateCache.Count > MaxCacheSize)
{
    _hangulStateCache.Clear(); // 전체 삭제!
}
```

> [!WARNING]
> `MaxCacheSize(100)` 초과 시 **전체 삭제**되므로, 다음 폴링 싸이클에서 모든 윈도우의 상태를 다시 질의해야 합니다. 창이 많은 환경(멀티태스킹)에서 순간적인 응답 지연이 발생할 수 있습니다.

**개선 방향**: LRU(Least Recently Used) 방식의 캐시로 교체하거나, `ConcurrentDictionary`로 스레드 안전성도 함께 확보

---

### 2. 포인터 크기 불일치 문제 (README에서 본인이 언급한 기술 난제)

**위치**: [`Program.cs` L762-L764](file:///d:/VSCODE/IMCPointer/Program.cs#L762-L764)

```csharp
IntPtr hArrowNew = PointerGraphicsFactory.CreateColoredSystemPointer(
    NativeMethods.OCR_NORMAL, t.PointerColor, _pointerPhysicalSize);
```

`_pointerPhysicalSize`를 `SM_CXCURSOR` 기준으로 계산하지만, 실제 Windows가 표시하는 커서 크기는 **DPI 배율에 따라 시스템이 내부 스케일링**을 추가로 적용합니다. 즉, `SM_CXCURSOR`는 논리 크기이고, 실제 물리적 렌더 크기는 다를 수 있습니다.

**개선 방향**: `GetSystemMetrics(SM_CXCURSOR)` 대신 현재 활성 모니터의 DPI를 기반으로 `32 * (dpi / 96)` 방식으로 렌더 크기를 별도 계산하거나, `SHGetSystemMetrics` API 활용

---

### 3. `WndProc`에서 `Task.Delay` 남용

**위치**: [`Program.cs` L573](file:///d:/VSCODE/IMCPointer/Program.cs#L573)

```csharp
protected override void WndProc(ref Message m)
{
    if (m.Msg == WindowPosChangedMessage)
        Task.Delay(200).ContinueWith(_ => this.BeginInvoke(...));
    base.WndProc(ref m);
}
```

> [!WARNING]
> `WM_WINDOWPOSCHANGED(0x0047)`는 **매우 빈번하게** 발생하는 메시지입니다 (창 이동, 리사이즈마다). `Task.Delay`를 매번 생성하면 ThreadPool 태스크가 폭증할 수 있습니다.

**개선 방향**: 디바운싱(`System.Threading.Timer` 재사용 또는 `volatile` 타임스탬프 비교)으로 마지막 이벤트만 처리

---

### 4. `EvaluateTargetProcess`에서 예외 무시

**위치**: [`Program.cs` L939](file:///d:/VSCODE/IMCPointer/Program.cs#L939)

```csharp
try { string n = Process.GetProcessById((int)pid).ProcessName; ... }
catch { } return false;
```

> [!NOTE]
> `Process` 객체를 `using`으로 감싸지 않아 **핸들 누수** 가능성이 있습니다. `Process.GetProcessById()`는 OS 핸들을 열기 때문에 반드시 `Dispose()` 해야 합니다.

**개선 방향**:
```csharp
using var proc = Process.GetProcessById((int)pid);
string n = proc.ProcessName;
```

---

### 5. `ImeState.Detect()`의 언어 감지 로직 취약

**위치**: [`ImeNativeCore.cs` L40-L41](file:///d:/VSCODE/IMCPointer/ImeNativeCore.cs#L40-L41)

```csharp
if (langId == 0x0409) return State.PaliUS;  // 영어 US = Pali로 간주
if (langId == 0x0411) return State.JapaneseIME;
```

> [!CAUTION]
> `langId == 0x0409`(영어 US)이면 무조건 `PaliUS`를 반환합니다. 즉, **일반 영어 사용자도 Pali 모드**로 감지됩니다. Pali IME가 실제로 설치/활성화된 경우에만 PaliUS 처리가 되어야 합니다.

**개선 방향**: Pali 레이아웃의 고유 HKL(Keyboard Layout) GUID 또는 레지스트리 설치 여부 확인 후 분기

---

## 🏗️ 코드 품질 개선

### 6. `Program.cs` 단일 파일 비대화 문제

현재 1038줄짜리 `Program.cs` 하나에 다음 클래스들이 모두 존재합니다:

| 클래스 | 역할 |
|:---|:---|
| `AppConfig` | 설정값 |
| `UiText` | 문자열 리소스 |
| `PointerGraphicsFactory` | 그래픽 생성 |
| `MainForm` | 폼 + 타이머 + 트레이 + 인디케이터 |
| `Program` | 진입점 |

**개선 방향**: 각 클래스를 별도 파일로 분리
```
AppConfig.cs
UiText.cs
PointerGraphicsFactory.cs
MiniIndicatorRenderer.cs  ← MainForm에서 인디케이터 로직 추출
MainForm.cs
Program.cs
```

---

### 7. `MainForm` 의존성 과다 (God Object 패턴)

`MainForm`이 **상태 추적, 트레이 UI 구성, 포인터 적용, 인디케이터 렌더링, 시스템 이벤트 처리**를 모두 담당합니다. 단일 책임 원칙(SRP)에 위배됩니다.

**개선 방향**: 
- `PointerManager` 클래스: 포인터 적용 및 에셋 관리
- `MiniIndicator` 클래스: 레이어드 윈도우 인디케이터 전담
- `StateTracker` 클래스: IME 상태 감지 로직

---

### 8. Magic Number 남용

**위치**: [`Program.cs` L488](file:///d:/VSCODE/IMCPointer/Program.cs#L488)

```csharp
cp.ExStyle |= 0x00000080 | 0x00000020 | 0x00080000 | 0x08000000 | 0x00000008;
```

> [!NOTE]
> `WS_EX_TOOLWINDOW`, `WS_EX_NOACTIVATE` 등 Named Constant가 있음에도 숫자 리터럴을 직접 사용합니다.

**개선 방향**: `NativeMethods.cs`에 상수 정의 또는 `[Flags] enum WindowStyleEx`로 관리

---

### 9. 설정 영속성 없음 (세션 간 설정 유지 불가)

현재 `AppConfig`의 `DefaultPointerMode = 2`, `DefaultEnableMiniIndicator = true` 등 설정값이 **하드코딩**되어 있어, 사용자가 메뉴에서 변경해도 **재실행 시 초기화**됩니다.

**개선 방향**: `System.Text.Json` + `%AppData%\IMCPointer\settings.json` 또는 레지스트리(`HKCU\Software\IMCPointer`)에 사용자 설정 저장

---

## ⚡ 성능 최적화

### 10. 100ms 폴링 vs 이벤트 기반 하이브리드 방식

현재 모든 IME 상태 감지는 100ms 타이머 폴링입니다. IME 상태 변경은 `WM_IME_NOTIFY`, `WM_INPUTLANGCHANGE` 등 **실시간 메시지로 감지 가능**합니다.

**개선 방향**: 
- 기본은 `WM_INPUTLANGCHANGE` 후킹으로 즉각 반응
- 폴링은 300ms로 늘려 CPU 사용량 감소
- Caps Lock은 `RegisterHotKey` 또는 Raw Input으로 감지

---

### 11. `RenderPointerToArgbBitmap`에서 DC 누수 위험

**위치**: [`Program.cs` L221-L284](file:///d:/VSCODE/IMCPointer/Program.cs#L221-L284)

중간에 `return null` 경로에서 `hdcMem`, `hdcScreen`, `hDib`가 해제되지 않는 예외 경로가 존재합니다.

**개선 방향**: `try/finally` 블록으로 모든 경로에서 해제 보장

---

## 🎨 UX / 기능 개선

### 12. 트레이 메뉴 항목 설정이 코드 내 하드코딩

```csharp
// AppConfig에서
public static bool ShowPointerWinDefault = true;
public static bool ShowPointerWinColor = true;
public static bool ShowPointerNewColor = true;
```

이 값들을 `false`로 바꿔 특정 메뉴를 숨길 수 있지만, **빌드 없이는 변경 불가**입니다.

**개선 방향**: 위 설정 영속성 제안(#9)과 연계하여 런타임 설정으로 전환

---

### 13. 버전 정보 표시 없음

트레이 메뉴 또는 툴팁에 현재 앱 버전이 표시되지 않습니다.

**개선 방향**: 
```csharp
// UiText에 추가
public static string VersionInfo => 
    $"v{Assembly.GetExecutingAssembly().GetName().Version}";
```
트레이 메뉴 타이틀 항목에 `IMCPointer v1.0.0` 형태로 표시

---

### 14. 테마 색상 커스터마이징 불가

5가지 상태(영소/영대/한글/Pali/Japanese)의 색상이 코드에 하드코딩되어 있습니다.

**개선 방향**: 설정 파일에 색상 저장, 색상 선택 다이얼로그 제공 (향후 로드맵)

---

### 15. 단일 언어(한국어) UI 하드코딩

`UiText` 클래스에 한국어 문자열이 직접 하드코딩되어 있습니다. 영어권 사용자에게 배포할 경우 불편합니다.

**개선 방향**: `Resources.resx` 기반 다국어 지원 (최소 한국어/영어 2개)

---

## 📐 기술 난제 해결 힌트 (DPI 불일치)

README에서 본인이 언급한 **"고해상도 유지 시 크기 불일치"** 문제에 대한 분석:

### 근본 원인

Windows의 커서 크기 결정 흐름:
```
사용자 설정 (접근성) → SM_CXCURSOR (논리값) → DWM이 DPI 배율 추가 적용 → 실제 화면 표시 크기
```

`SM_CXCURSOR`는 DPI 배율 **이전** 값이므로, 이미 DWM이 확대한 크기와 맞지 않습니다.

### 해결 방향

```csharp
// 방법 1: SystemParametersInfoForDpi 사용 (Windows 10 1607+)
[DllImport("user32.dll")]
static extern bool SystemParametersInfoForDpi(uint uiAction, uint uiParam, 
    IntPtr pvParam, uint fWinIni, uint dpi);

// SPI_GETCURSORS = 0x0006 사용하여 DPI-aware 커서 크기 직접 조회
```

```csharp
// 방법 2: 렌더 크기를 논리 크기로 고정하고 SetSystemCursor에 맡기기
// Windows가 SetSystemCursor로 등록된 커서를 표시할 때
// 시스템 DPI 배율을 자동 적용하므로, 렌더 크기 = 32px (비배율)로 고정
int renderSize = 32; // DPI 배율 없이 고정
// → SetSystemCursor가 나머지 스케일링 처리
```

> [!TIP]
> **방법 2**가 구현이 단순하고 효과적입니다. `SetSystemCursor()`는 내부적으로 DPI 스케일링을 처리하므로, 32px 기준으로 그린 커서를 넘겨주면 시스템이 배율에 맞게 확대합니다.

---

## ✅ 개선 우선순위 요약

| 우선순위 | 항목 | 난이도 | 효과 |
|:---:|:---|:---:|:---:|
| 🔴 즉시 | `Process` 핸들 누수 (#4) | 쉬움 | 안정성 |
| 🔴 즉시 | PaliUS 감지 오류 (#5) | 중간 | 정확성 |
| 🟡 단기 | 설정 영속성 추가 (#9) | 중간 | UX |
| 🟡 단기 | 버전 정보 표시 (#13) | 쉬움 | UX |
| 🟡 단기 | `WndProc` 디바운싱 (#3) | 중간 | 안정성 |
| 🟢 중기 | DPI 불일치 해결 (#2 + 힌트) | 어려움 | 핵심 버그 |
| 🟢 중기 | 파일 분리 리팩토링 (#6, #7) | 중간 | 유지보수성 |
| 🔵 장기 | 이벤트 기반 감지 (#10) | 어려움 | 성능 |
| 🔵 장기 | 색상 커스터마이징 (#14) | 어려움 | 기능 확장 |
