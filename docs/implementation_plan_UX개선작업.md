# IMEPointer 중기 UX 개선 구현 계획 (5건)

앞서 분석 보고서에서 도출된 **중기 UX 개선 항목 5가지**를 적용하기 위한 구현 계획입니다. 사용자 편의성 향상과 코드 유지보수성 개선을 목표로 합니다.

## 제안하는 변경 사항

### 1. 설정 영속성 구현 (#12)
사용자가 트레이 메뉴에서 변경한 설정(포인터 모드, 배열창 표시 여부 등)이 앱을 재시작해도 유지되도록 **레지스트리(Registry)**를 활용해 영속성을 부여합니다.

#### [MODIFY] [Config.cs](file:///d:/VSCODE/IMEPointer/Config.cs)
- `AppConfig` 클래스 내에 설정을 레지스트리에서 불러오는 `LoadFromRegistry()` 메서드와 변경 시 저장하는 `SaveToRegistry()` 메서드를 추가합니다.
- 저장 경로: `HKCU\Software\IMEPointer`

#### [MODIFY] [Program.cs](file:///d:/VSCODE/IMEPointer/Program.cs)
- 앱 시작 시(`MainForm` 생성자) `AppConfig.LoadFromRegistry()`를 호출합니다.
- 트레이 메뉴에서 설정이 변경될 때마다 `AppConfig.SaveToRegistry()`를 호출하여 상태를 즉각 반영합니다.

---

### 2. 버전 정보 트레이 메뉴 표시 (#17)
앱이 현재 어떤 버전인지 쉽게 확인할 수 있도록 트레이 메뉴 최상단 제목에 버전 정보를 추가합니다.

#### [MODIFY] [Config.cs](file:///d:/VSCODE/IMEPointer/Config.cs)
- `UiText` 클래스에 동적 버전 정보 속성 추가:
  `public static string VersionInfo => $"v{Assembly.GetExecutingAssembly().GetName().Version}";`

#### [MODIFY] [Program.cs](file:///d:/VSCODE/IMEPointer/Program.cs)
- `BuildTrayMenu()` 메서드 내 트레이 메뉴 타이틀 텍스트를 `UiText.AppName + " " + UiText.VersionInfo`로 변경합니다.

---

### 3. 일본어 테마 트레이 아이콘 구분 (#16)
현재 모두 'J'로 표시되어 구분하기 힘든 일본어 조합형/완성형 트레이 텍스트를 명확하게 분리합니다.

#### [MODIFY] [Config.cs](file:///d:/VSCODE/IMEPointer/Config.cs)
- `AppConfig.Themes` 딕셔너리 변경:
  - `JapaneseHangul1` ➔ `"J1"`
  - `JapaneseHangul2` ➔ `"J2"`
  - `JapaneseHangul3` ➔ `"J3"`

---

### 4. Magic Number 상수화 (#11, #15)
코드 내에 의미를 알 수 없는 하드코딩된 숫자(가상 키코드, 윈도우 스타일 속성)를 명명된 상수로 선언하여 가독성을 높입니다.

#### [MODIFY] [NativeMethods.cs](file:///d:/VSCODE/IMEPointer/NativeMethods.cs)
- 가상 키코드 상수 선언: `VK_SHIFT (0x10)`, `VK_LWIN (0x5B)`, `VK_RWIN (0x5C)`
- 윈도우 스타일 상수 선언: `WS_MINIMIZEBOX (0x00020000)`, `WS_SYSMENU (0x00080000)`, `WS_EX_APPWINDOW (0x00040000)`, `WS_EX_TOOLWINDOW (0x00000080)`, `WS_EX_TRANSPARENT (0x00000020)`, `WS_EX_LAYERED (0x00080000)`, `WS_EX_NOACTIVATE (0x08000000)`, `WS_EX_TOPMOST (0x00000008)`

#### [MODIFY] [UIComponents.cs](file:///d:/VSCODE/IMEPointer/UIComponents.cs)
- `KeyboardLayoutForm`의 `CreateParams`에 정의된 매직 넘버를 위 상수로 교체합니다.

#### [MODIFY] [Program.cs](file:///d:/VSCODE/IMEPointer/Program.cs)
- `MainForm`의 `CreateParams`에 정의된 매직 넘버를 위 상수로 교체합니다.
- `RefreshKeyboardLayoutOverlay()` 내 가상 키코드(`0x10`, `0x5B`, `0x5C`)를 상수로 교체합니다.

---

## 검증 계획

### 수동 검증
1. **설정 영속성**: 앱 실행 후 트레이 메뉴에서 포인터 모드 및 옵션을 변경하고, 앱을 재시작했을 때 변경된 설정이 그대로 유지되는지 확인합니다.
2. **UI 확인**:
   - 트레이 아이콘 제목에 버전 정보(`v1.x.x.x`)가 정상 출력되는지 확인.
   - 일본어 모드(J1, J2, J3) 전환 시 트레이 아이콘 문자가 제대로 변경되는지 확인.
3. **안정성**: 상수화로 인해 기존 자판 배열창 및 투명 오버레이 기능이 동작 불능 상태에 빠지지 않는지 확인합니다.
