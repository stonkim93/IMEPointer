<div align="center">

# 🌍 IMEPointer

### I'm e-Pointer that utilizes color pointers & multi-language keyboard layout (English, Korean, Pali, Japanese)

### 다국어 입력 모드를 지원하는 고성능 IME 상태 추적 유틸리티

![Platform](https://img.shields.io/badge/platform-Windows-0078D4?logo=windows11&logoColor=white)
![Framework](https://img.shields.io/badge/.NET-10.0--windows-512BD4?logo=dotnet&logoColor=white)
![Language](https://img.shields.io/badge/language-C%23-239120?logo=csharp&logoColor=white)
![License](https://img.shields.io/badge/license-MIT-yellow.svg)
![Status](https://img.shields.io/badge/status-Production--Ready-2E8B57)

<br>

## 💡 개발 동기 (Why IMEPointer?)

컴퓨터로 문서 작업을 할 때, 다양한 언어와 특수 기호를 다루는 현대의 학자와 개발자들을 위해 설계되었습니다.

#### 🎯 1. 마우스 포인터로 입력 상태를 한눈에 파악

> "문자 입력 상태에 따라 마우스 포인터의 색상이 변한다면, 컴퓨터 문서 작업에 얼마나 도움이 될까?"

- **문제**: 한글, 영어, 특수기호 모드를 번갈아 사용할 때 현재 모드를 놓치기 쉬움
- **해결**: 마우스 포인터와 트레이 아이콘의 색상으로 **즉시 입력 상태를 시각화**
- **효과**: 타이핑 오류 감소, 작업 효율 증대

#### 🎯 2. 한글/Caps 상태를 창의적으로 재활용

> "한글 입력 상태에서 Caps Lock이 무의미한데, 이걸 특정 언어의 문자 입력 모드로 활용할 수 있지 않을까?"

- **기존 문제**: Caps Lock은 영어에서만 의미 있음
- **새로운 활용**: 한글/Caps 상태에서 **공학용 특수기호, Pali어, 일본어** 등의 입력 모드로 재구성
- **효과**: 최소 키 입력으로 최대 다양한 모드 지원

#### 🎯 3. 초기 불교 문헌 연구 지원

> "기존 Pali어 자판이 있으나, Sanskrit까지 포함한 글자판이 있다면 초기불교 문헌 정리에 도움이 될 것 같다."

- **Pali어 + Sanskrit**: 모음 변수(ā, ī, ū, ṛ, ḷ) 및 특수 자음(ṃ, ṇ, ṭ, ḍ, ś, ṣ, ḥ) 완벽 지원
- **효과**: 팔리 경전 및 산스크리트 텍스트 직접 입력 가능
- **대상**: Pali 학자, 불교 연구자, 고전 문헌 전문가

#### 🎯 4. 한글의 글자 조합 원리를 일본어에 적용

> "한글의 자음+모음 글자 조합 원리를 일본어 문자에도 적용할 수 있지 않을까?"

- **한글 원리**: 자음(19개) + 모음(21개) → 무한한 글자 조합
- **일본어 응용**: 자음(consonant) + 모음(vowel) → 히라가나/가타카나 조합
- **결과**: **일본어1 (조합형)** 및 **일본어2 (3-Layer)** 입력 모드 개발
- **효과**: 문법에 정통하지 않은 사용자도 직관적인 입력 가능

</div>

<br>

## ✨ 주요 기능 (Key Features)

### 1️⃣ 9가지 입력 상태별 색상 테마 (9-State IME Theme)

입력 모드가 변경되면 마우스 포인터와 트레이 아이콘의 색깔이 **즉각적으로** 변합니다.

<div align="center">

| 입력 상태 | 포인터 색상 | 트레이 | 설명 |
|:---|:---|:---:|:---|
| **영어 소문자** | $\color{white}\Large\blacktriangle$ White | $\color{gray}\large\boldsymbol{e}$ | 기본 영어 입력 |
| **영어 대문자** | $\color{DeepSkyBlue}\Large\blacktriangle$ DeepSkyBlue | $\color{deepskyblue}\large\textbf{E}$ | Shift/Caps + 영어 |
| **한글** (기본) | $\color{red}\Large\blacktriangle$ Red | $\color{red}\large\textbf{K}$ | 한글 입력 (Caps Off) |
| **공학 특수기호** | $\color{orange}\Large\blacktriangle$ Orange | $\color{orange}\large\textbf{S}$ | **한글CAPS** + 그리스, 수학 기호 |
| **Pali/Sanskrit** | $\color{orange}\Large\blacktriangle$ Orange | $\color{orange}\large\textbf{P}$ | **한글CAPS** + 팔리/산스크리트 |
| **일본어1** (조합형) | $\color{lime}\Large\blacktriangle$ Lime | $\color{lime}\large\textbf{J}$ | **한글CAPS** + 자음/모음 조합 |
| **일본어2** (3-Layer) | $\color{lime}\Large\blacktriangle$ Lime | $\color{lime}\large\textbf{J}$ | **한글CAPS** + 레이어 기반 입력 |
| **Pali 외부키** | $\color{orange}\Large\blacktriangle$ Orange | $\color{orange}\large\textbf{p}$ | 외부 Pali IME 사용 |
| **Japanese IME** | $\color{lime}\Large\blacktriangle$ Lime | $\color{lime}\large\textbf{j}$ | 외부 일본어 IME 사용 |

</div>

### 2️⃣ 한글/CAPS 상태에서 4가지 입력 모드 선택

한글 입력 중 **Caps Lock**으로 4가지 특수 모드 전환:

- 🔷 **공학용 특수기호** (Engineer): 그리스 문자 + 수학/과학 기호
  - α β γ δ ε π θ (그리스)
  - ∫ ∂ ∇ ± × √ ∞ (수학/미적분)
  - ° °C Ω Σ Δ (과학/단위)

- 🟠 **Pali/Sanskrit** (SE): 팔리어 + 산스크리트어 입력
  - 기본 모음: a, i, u, ṛ, ḷ (소문자 지원)
  - 자음과 모음의 특수형 (long vowels, diacritics)
  - 학술 기호: † * ※

- 🟢 **일본어1** (조합형): 자음+모음 자동 조합
  - 자음(consonant): Q W E R T...
  - 모음(vowel): A S D F G...
  - 결과: 히라가나 직접 생성

- 🟢 **일본어2** (3-Layer): 레이어 기반 다중 글자
  - Layer1: 기본 히라가나
  - Layer2: Shift + 시프트판
  - Layer3: 한자키 + 추가 문자

### 3️⃣ 엑셀과 아래한글에서 포인터 하단에 작은원(mini indicator) 표시

- 마우스 포인터를 자체적으로 관리하는 앱에서는 포인터 우측 하단에 **'작은 원'**을 생성하고 입력 상태에 따라 색상을 변경합니다.

  ⚠️ Microsoft Excel (`excel.exe`)의 셀 위에서는 포인터가 흰색 십자가 형태로 바뀐다.
  
  ⚠️ 한글과컴퓨터 아래한글 (`hwp.exe`)의 텍스트 입력창 안에서는 포인터가 검은색 I자로 바뀐다.

- 시스템 트레이 아이콘을 **우클릭** (또는 좌클릭)하면:
  1. 현재 입력 상태 표시
  2. 포인터 종류 선택 메뉴
  3. 입력 모드 선택 메뉴
  4. 엑셀/한글 작은원 표시 활성화 옵션
  5. 키보드 배열창 On/Off
  6. 입력 문자 표시창 On/Off
  7. 종료(Exit) 버튼

### 4️⃣ 키보드 배열 시각화

- 선택한 입력 모드의 키보드 배열을 **실시간으로 표시**
- 각 모드별 배치 확인 및 학습에 최적화

### 5️⃣ 입력 문자 표시창

- 현재 **한글/CAPS 모드**에서 입력한 문자를 화면에 표시
- 입력 확인 및 학습 보조 기능

<br>

## 💡 사용팁 (Tips)

### 1️⃣ 공학용 특수기호 입력하기

1. **한글 입력 모드**에서 시작
2. **Caps Lock** 눌러 "한글CAPS 공학용 특수기호" 모드로 전환 (트레이에서 "S" 표시 확인)
3. 키보드 배열창에서 배치 확인
4. 그리스 문자, 수학 기호, 과학 기호 입력

**예시 입력**:
- α (Q행 A) + β (Z행 B) + γ (Q행 R)
- ∫ (Z행 X) + ∂ (Q행 W) + ∇ (Q행 E)
- Σ (A행 S) + √ (Z행 Z)

### 2️⃣ Pali어 / Sanskrit 키보드 설치 및 사용

#### 📌 Pali 외부 IME 사용 (고급)

* Windows US+Pali(Unicode) 키보드 설치 : `https://www.tipitaka.org/keyboard.html`

* 한국어(MS IME) ↔ Pali 빠른 전환 : $\color{lime}\textbf{Ctrl}$ + $\color{lime}\textbf{Shift}$

* 자판 목록에서 순환 선택 : $\color{deepskyblue}\textbf{WIN}$ + $\color{deepskyblue}\textbf{Space}$

* Pali 문자 입력 : $\color{red}\textbf{한/영키}$ (Right Alt) + ($\color{red}\textbf{A, S, D, R, T, Y, U, I, G, H, L, M, N}$)

#### 📌 Pali/Sanskrit 한글CAPS 모드 사용 (추천)

1. 한글 입력 모드 선택
2. **Caps Lock**을 눌러 "한글CAPS Pali어" 모드로 전환 (트레이에서 "P" 표시)
3. 키보드 배열창에서 **Pali/Sanskrit 배치** 확인
4. 각 키를 눌러 팔리/산스크리트 문자 입력

**PE 전환 단축키** (설정 시):
- **한자키** (또는 우측 Alt): Pali외부키 ↔ 공학용 ↔ Pali_한글 ↔ 일본어 순환

### 3️⃣ 일본어 입력하기

#### 일본어1 (조합형)

1. **한글 입력 모드** → **Caps Lock** → "한글CAPS 일본어1_조합형" 모드 (트레이 "J")
2. 자음과 모음을 조합하여 히라가나 생성
3. 예: D(し) + A(い) → しい

#### 일본어2 (3-Layer)

1. **한글 입력 모드** → **Caps Lock** → "한글CAPS 일본어2_3Layer" 모드
2. **Layer1**: 기본 히라가나 입력
3. **Layer2**: Shift + 키 → 추가 문자
4. **Layer3**: 한자키 + 키 → 최상층 문자

**HK/YN 전환**:
- **한자키** + **H**: 히라가나(H) ↔ 가타카나(K) 전환
- **한자키** + **Y**: 일본어 입력 모드 순환

### 4️⃣ 아래한글에서 윈도우 MS IME 사용하기

> 📌 [TIP]
> 한글과컴퓨터의 자체 입력기 대신 Microsoft IME를 사용하도록 전환하면, 아래한글에서도 IMEPointer가 입력 상태를 정확히 표시합니다.

* 아래한글 실행 후 상단 메뉴에서 `도구 ➔ 글자판 ➔ 글자판 바꾸기` 클릭 (단축키: <kbd>Alt</kbd> + <kbd>F2</kbd>)
* **글자판 바꾸기** 창에서 현재 글자판을 **한국어** 대신 $\color{lime}\textbf{윈도우\ 입력기}$로 변경
* **글자판 자동 변경** 해제하여 항상 윈도우 설정을 따르도록 저장
* 트레이 아이콘을 클릭하여 **엑셀/한글 작은원 표시**가 체크되면, 입력 상태를 시각적으로 구분하기 쉬움

### 5️⃣ 윈도우 시작 프로그램에 추가하기

* 윈도우 실행창(run)을 띄운다 : <kbd>WIN</kbd> + <kbd>R</kbd>
* 윈도우 시작프로그램 폴더를 연다 : `shell:startup`
* IMEPointer.exe 바로가기 파일을 생성하여 시작프로그램 폴더에 붙여넣는다
* IMEPointer 실행 후 숨겨진 아이콘 박스에 포함된 경우, 작업표시줄로 끄집어내어 MS IME 옆에 놓으면 시각적으로 도움이 된다

<br>

## 🏃 초보 개발자를 위한 정보

### ⚙️ 요구 사항

| 항목 | 내용 |
|:---|:---|
| 🖥️ **OS** | Windows 10 / Windows 11 (64-bit) |
| 🧩 **Runtime** | [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) 이상 |
| ⌨️ **Language** | C# 12 / 13 |
| 🛠️ **IDE** | Visual Studio 2022 / 2026 |

### 1️⃣ 레포지토리 클론

```bash
git clone https://github.com/stonkim93/IMEPointer.git
```

### 2️⃣ 빌드 & 배포판 만들기

Visual Studio에서 `IMEPointer.csproj`를 열고 빌드합니다.

#### 📌 조건부 컴파일 (Optional Features)

`.csproj` 파일의 `<DefineConstants>`를 수정하여 포함할 기능을 선택할 수 있습니다:

```xml
<PropertyGroup>
  <DefineConstants>
    ENABLE_CAPS_ENGINEER;        <!-- 공학용 특수기호 -->
    ENABLE_CAPS_PALI;             <!-- Pali/Sanskrit -->
    ENABLE_CAPS_JAPANESE1;         <!-- 일본어1 (조합형) -->
    ENABLE_CAPS_JAPANESE2;         <!-- 일본어2 (3-Layer) -->
    ENABLE_KEYBOARD_LAYOUT         <!-- 키보드 배열창 -->
  </DefineConstants>
</PropertyGroup>
```

#### 프레임워크 의존형 (소용량)

```bash
dotnet publish -c Release --self-contained false /p:PublishSingleFile=true
```

#### 런타임 포함형 (대용량)

```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

### 3️⃣ 2가지 실행 파일 다운로드

오른쪽의 **[Releases]** 탭에서 최신 버전의 `.zip` 파일을 다운로드 하고 압축을 해제합니다.

| 파일명 | 용도 | 파일 크기 |
|:---|:---|:---:|
| `IMEPointer.zip` | .NET10 기설치 PC | Small (~0.3 MB) |
| `IMEPointer_with_dotnet10.zip` | .NET10 미설치 PC | Big (~44 MB) |

### 4️⃣ 실행하기

`IMEPointer.exe`를 실행하면 시스템 트레이에서 즉시 작동합니다.

> 📌 [IMPORTANT]
> 중복 실행 방지(`Mutex`)가 내장되어 있어 안전하게 백그라운드에서 상주합니다.

<br>

## ⚡ 기술적 특징 및 최적화 (Technical Highlights)

> 📌 [NOTE]
> 이 앱은 백그라운드에서 365일 실행되어도 시스템에 전혀 무리를 주지 않도록, 초경량·고성능을 목표로 가혹하게 최적화되었습니다.

### 1️⃣ 다중 입력 상태 관리 (Multi-State IME Engine)

* **9가지 입력 상태 추적**: 기존의 5가지 상태(영어/한글/Pali)에서 9가지로 확장
  - 기본 상태: 영어 소/대문자, 한글, Pali 외부IME, 일본어 IME
  - 한글CAPS 모드: 공학용, Pali/Sanskrit, 일본어1, 일본어2

* **상태 전환 엔진**: 언어 변경, Caps Lock, 한자키 입력을 감지하여 자동 상태 전환

* **컨텍스트 동기화**: 창 전환 시에도 입력 상태를 정확히 유지

### 2️⃣ 다국어 기호 입력 시스템 (Multilingual Keyboard Mapping)

* **공학용 그리스 + 수학 기호**: 
  - 그리스 문자 14개 (α β γ δ ε ζ η θ λ μ ν ρ σ ω)
  - 대문자 그리스 6개 (Δ Σ Ω π)
  - 수학 기호 20개 (∫ ∂ ∇ ± × ÷ √ ∞ ≈ ≠ ≤ ≥ 등)
  - 과학 단위 (° °C K Ω)

* **Pali/Sanskrit 글자판**:
  - 기본 문자 13개 + 특수음 13개
  - 모음 변수 (ā ī ū ṛ ḷ)
  - 자음 특수형 (ṃ ṇ ṭ ḍ ś ṣ ḥ)
  - 학술 기호 († ※)

* **일본어 조합형**:
  - 자음 행(15개) + 모음 5개 = 75개 히라가나 자동 생성

* **일본어 3-Layer**:
  - 기본층: 기본 히라가나
  - Shift층: 추가 변형
  - 한자키층: 가타카나 및 최상층 문자

### 3️⃣ 해상도 및 배율에 반응하는 반응형 시각화 (Dynamic Scaling)

* **마우스 포인터 크기 동기화**: 사용자가 윈도우 설정에서 포인터 크기를 변경하거나 모니터 DPI를 변경하면 즉시 감지

* **고해상도 렌더링**: 커서가 커져도 이미지를 단순 확대하지 않고, 변경된 배율에 맞춰 **기하학적 형태와 외곽선 두께를 고해상도로 다시 계산**하여 항상 선명한 커서 제공

* **작은 원 위치 동기화**: 엑셀/한글의 작은 원도 포인터 크기에 비례하여 정확히 이동

### 4️⃣ 문맥을 놓치지 않는 스마트 입력 감지 (Smart Context Tracking)

* **3중 감지 엔진**:
  - ① 하드웨어 키보드 신호 직접 가로채기 (GlobalKeyboardHook)
  - ② 입력창에 직접 상태 질의하기 (IME Query API)
  - ③ 윈도우 레지스트리 상태 확인 (Registry Monitoring)
  - 이를 통해 메모장, 엑셀, 게임, 보안 프로그램 등 어떤 환경에서도 정확한 상태 감지

* **창 포커스 추적**: 바탕화면이나 작업표시줄을 클릭했다가 돌아올 때 **이전 창과 언어 상태를 기억**하고 원래대로 복구

* **특수앱 최적화**: Excel, 아래한글, 게임 등 각 앱의 특성에 맞춘 별도의 감지 로직

### 5️⃣ 메모리 낭비 제로, 극한의 성능 최적화 (Zero GC & Resource Management)

* **마우스 끊김 원천 차단 (Zero GC)**:
  - 30ms마다 마우스를 감지하면서도 임시 공간(Stack)만 사용하고 즉시 비워버리는 특수 설계
  - 가비지 컬렉터가 개입할 여지를 없애 **마우스가 단 1ms도 끊기지 않음**

* **컬러 포인터 캐싱**: 자주 사용하는 컬러 포인터를 메모리에 캐시하여 반복 생성 방지

* **완벽한 자원 관리**: 색상이 바뀔 때마다 생성되는 비트맵을 사용 직후 즉시 파괴(`DeleteObject`)하여 메모리 누수 차단

### 6️⃣ 외부 충돌 및 오류에 대비한 철벽 안전망 (Bulletproof Safety)

* **Thread-Safe 설계**: 듀얼 모니터 연결, 해상도 변경 시 발생하는 레이스 컨디션 차단

* **강제 종료 시 자동 복구**: 예기치 못한 에러나 업데이트로 프로그램이 강제 종료되더라도 **윈도우 원래의 하얀색 마우스 커서로 자동 복구**

* **예외 처리**: 모든 주요 진입점에 try-catch 및 finally 블록으로 리소스 누수 방지

### 7️⃣ 한글CAPS 모드 상태 관리 (HangulCaps State Machine)

* **Lang.cs의 상태 머신**:
  - 한글 입력 + Caps Lock → 4가지 모드 순환
  - 각 모드별 고유한 키보드 매핑 (PaliMap, EngineerMap, JapaneseMap1, JapaneseMap2)
  - 한자키(또는 PE 글자)로 모드 간 직접 전환 가능

* **조건부 컴파일**: 필요한 기능만 선택적으로 빌드하여 파일 크기 최소화

* **키보드 배열 실시간 표시**: 현재 모드의 키보드 배치를 화면에 표시하여 학습 최적화

<br>

## 🔄 IMECursor에서 IMEPointer로의 진화

| 특성 | IMECursor | IMEPointer |
|:---|:---|:---|
| **입력 상태** | 5가지 | **9가지** |
| **한글CAPS 모드** | 미지원 | **4가지 입력 모드** |
| **포인터 종류** | 2가지 | **3가지** |
| **Pali 지원** | ✅ 외부 IME만 | **✅ 외부 + 한글CAPS** |
| **공학용 기호** | ❌ | **✅ 그리스 + 수학** |
| **일본어** | ❌ | **✅ 2가지 모드** |
| **키보드 배열창** | ❌ | **✅** |
| **입력 문자 표시** | ❌ | **✅** |
| **코드 최적화** | 완료 | **초극한** |

<br>

## 📜 라이선스 (License)

이 프로젝트는 **MIT License**에 따라 자유롭게 수정 및 배포할 수 있습니다.

Made with ❤️ for multilingual writers, scholars, engineers, and Pāḷi researchers

<br>

---

## 🎓 학술적 영감

이 프로젝트는 다음의 학문 분야에서 영감을 받았습니다:

* **불교학**: 팔리 경전(Pali Canon) 및 산스크리트 문헌 연구
* **언어학**: 다국어 입력 시스템 및 한글 자음/모음 조합 원리
* **컴퓨터공학**: 극한 성능 최적화 및 윈도우 IME 저수준 프로그래밍

**문의 및 기여**: GitHub Issues를 통해 버그 리포트, 기능 제안, 풀 리퀘스트를 환영합니다!

<br>

❤️🌍✨⚡🚀💡🎯🆕🖥️💻⌨️🔤🎨🧩🐛🔹📐📝✅🏆
