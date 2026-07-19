// Lang.cs - IMEPointer
// Pali어 / 일본어1(조합형) / 일본어2(3Layer) / 특수기호(Engineer) 자판 매핑 및 처리.
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Forms;

namespace IMEPointer
{
    internal static class InputVk
    {
        public const int Shift = 0x10;
        public const int Ctrl = 0x11;
        public const int Right = 0x27;
        public const int Escape = 0x1B;
        public const int Backspace = 0x08;
        public const int vk_B = 0x42;
        public const int vk_C = 0x43;
        public const int vk_H = 0x48;
        public const int vk_J = 0x4A;
        public const int vk_K = 0x4B;
        public const int vk_L = 0x4C;
        public const int vk_M = 0x4D;
        public const int vk_N = 0x4E;
        public const int vk_P = 0x50;
        public const int vk_Y = 0x59;
        
        // 일본어 기호 매핑을 위한 가상 키 코드 상수 추가
        public const int OemYen = 0xDC;      // (\ |) → (¥ |)
        public const int OemColon = 0xBA;    // (; :) → (・ :)
        public const int OemComma = 0xBC;    // (, <) → (, 、)
        public const int OemPeriod = 0xBE;   // (. >) → (. 。)
        public const int OemSlash = 0xBF;    // (/ ?) → (/ ー)
    }

    #region [ 0. 유틸리티: Win32 API 기반 동적 문자 확인 (KeyboardUtils) ]
    // [이번 수정] 하드코딩 맵핑 없이 기호와 숫자에 대해 Shift 적용 여부를 동적으로 반환하는 클래스 구조 개선
    internal static class KeyboardUtils
    {
        // [이번 수정] 현재 포커스된 창의 키보드 레이아웃(HKL)을 고려하여 정확도를 높이기 위해 ToUnicodeEx 및 관련 API 도입
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte[] lpKeyState, [Out, MarshalAs(UnmanagedType.LPWStr, SizeConst = 64)] StringBuilder pwszBuff, int cchBuff, uint wFlags, IntPtr dwhkl);
        
        [DllImport("user32.dll")]
        private static extern uint MapVirtualKeyEx(uint uCode, uint uMapType, IntPtr dwhkl);

        [DllImport("user32.dll")]
        private static extern bool GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

        public static string? GetChar(int vkCode, bool isShift)
        {
            byte[] keyState = new byte[256];
            
            // [이번 수정] 현재 키보드 상태를 먼저 읽어온 후, 필요한 Shift 상태만 덮어씌움
            GetKeyboardState(keyState);

            if (isShift) 
            {
                keyState[InputVk.Shift] = 0x80;
                keyState[0xA0] = 0x80; // VK_LSHIFT
                keyState[0xA1] = 0x80; // VK_RSHIFT
            }
            else
            {
                keyState[InputVk.Shift] = 0;
                keyState[0xA0] = 0;
                keyState[0xA1] = 0;
            }

            // [이번 수정] 포커스된 대상 윈도우의 정확한 키보드 레이아웃(HKL) 확보
            IntPtr hWnd = GetForegroundWindow();
            uint threadId = GetWindowThreadProcessId(hWnd, IntPtr.Zero);
            IntPtr hkl = GetKeyboardLayout(threadId);

            uint scanCode = MapVirtualKeyEx((uint)vkCode, 0, hkl);
            StringBuilder sb = new StringBuilder(5);
            
            // [이번 수정] wFlags에 0을 전달하여 가상 Shift 상태가 문자열 변환에 정상 반영되도록 옵션 변경
            int result = ToUnicodeEx((uint)vkCode, scanCode, keyState, sb, sb.Capacity, 0, hkl);
            
            if (result > 0)
            {
                string ch = sb.ToString();
                
                // [이번 수정] 훅(Hook) 상태에서 API가 Shift를 무시하고 원래 숫자 등을 반환하는 현상 방지
                if (isShift && ch.Length == 1 && IsSymbolOrNumber(vkCode))
                {
                    string? shiftedFallback = GetStandardShiftedSymbol(vkCode);
                    // ToUnicodeEx 결과가 여전히 숫자라면 (즉, Shift가 적용되지 않았다면) 내부 매핑값 우선 사용
                    if (shiftedFallback != null && char.IsDigit(ch[0]))
                    {
                        return shiftedFallback;
                    }
                }
                return ch;
            }

            // [이번 수정] ToUnicodeEx 변환 실패 시 최후의 수단으로 표준 QWERTY 기호 매핑 반환
            if (isShift && IsSymbolOrNumber(vkCode))
            {
                return GetStandardShiftedSymbol(vkCode);
            }

            return null;
        }

        // [이번 수정] ProcessKey 딕셔너리에 일일이 기호를 추가하지 않도록, Shift 적용 시의 표준 기호를 반환하는 중앙 헬퍼 추가
        private static string? GetStandardShiftedSymbol(int vkCode)
        {
            return vkCode switch
            {
                0x31 => "!", 0x32 => "@", 0x33 => "#", 0x34 => "$", 0x35 => "%",
                0x36 => "^", 0x37 => "&", 0x38 => "*", 0x39 => "(", 0x30 => ")",
                0xC0 => "~", 0xBD => "_", 0xBB => "+", 0xDB => "{", 0xDD => "}",
                0xDC => "|", 0xBA => ":", 0xDE => "\"", 0xBC => "<", 0xBE => ">", 0xBF => "?",
                _ => null
            };
        }

        public static bool IsSymbolOrNumber(int vkCode)
        {
            return (vkCode >= 0x30 && vkCode <= 0x39) || // 숫자열 (0-9)
                   (vkCode >= 0xBA && vkCode <= 0xC0) || // ;, =, -, ., /, `
                   (vkCode >= 0xDB && vkCode <= 0xDE);   // [, \, ], '
        }

        // [이번 수정] 문자와 기호(숫자열 포함) 모두를 판별하기 위한 확장 메서드
        public static bool IsSymbolOrNumberOrLetter(int vkCode)
        {
            return IsSymbolOrNumber(vkCode) || (vkCode >= 0x41 && vkCode <= 0x5A);
        }

        // 글로벌 훅용: 한글, 영어 대/소문자 입력모드에서 Key2 상태일 때 Shift 강제 인식 처리
        public static bool HandleGlobalKey2Mode(int vkCode, bool isShift)
        {
            // [이번 수정] IsSymbolOrNumberOrLetter를 사용하여 문자와 기호 모두 판별
            if (AppConfig.IsOverlayKey2Mode && IsSymbolOrNumberOrLetter(vkCode))
            {
                string? ch = GetChar(vkCode, true);
                if (!string.IsNullOrEmpty(ch))
                {
                    GlobalInputHook.IsSending = true; 
                    NativeMethods.SendUnicodeString(ch); 
                    GlobalInputHook.IsSending = false; 
                    return true;
                }
            }
            return false;
        }
    }
    #endregion

    #region [ 1. 인터페이스 및 팩토리 (Interfaces & Factories) ]
    /// <summary>
    /// 각 언어별 키보드 입력 처리를 담당하는 공통 인터페이스입니다.
    /// </summary>
    internal interface IKeyProcessor
    {
        bool IsVirtualShift { get; }
        int CurrentLayer { get; }
        
        /// <summary>
        /// 키보드 다운 이벤트를 가로채어 각 언어에 맞게 처리합니다.
        /// </summary>
        bool ProcessKeyDown(int vkCode, bool isShift, bool capsOn, IntPtr hFore, bool isHangulMode);
        
        /// <summary>
        /// 한자 키 입력을 통한 레이어 전환 등의 특수 동작을 처리합니다.
        /// </summary>
        bool ProcessHanjaKey(IntPtr hFore, bool capsOn, bool isHangulMode);
        void OnMouseClick();
        void ToggleVirtualShift();
    }

    /// 개별 자판 프로세서 인스턴스를 관리하고 반환하는 팩토리 클래스입니다.
    /// 런타임 재할당을 방지하기 위해 readonly로 고정되어 있습니다.
    internal static class KeyProcessorFactory
    {
        public static readonly IKeyProcessor Engineer = new EngineerProcessor(); 
        public static readonly IKeyProcessor Pali = new PaliProcessor();
        public static readonly IKeyProcessor Japanese1 = new Japanese1Processor();
        public static readonly IKeyProcessor Japanese2 = new Japanese2Processor();
    }
    
    #endregion

    #region [ 2. 유틸리티: 텍스트 선택 및 클립보드 제어 (UI Automation & Clipboard) ]
    
    internal static class OverlayHelper
    {
        public static void ClearOverlay()
        {
            try
            {
                MainForm.Instance?.ClearOverlay();
            }
            catch (Exception) { /* Overlay form already disposed or unavailable; safe to ignore. */ }
        }
    }

    internal static class TextSelectionUtils
    {
        private const uint ClipboardUnicodeTextFormat = 13;
        private const int ClipboardOpenRetryCount = 3;
        private const int ClipboardOpenRetryDelayMs = 10;
        private const int CopyPollingRetryCount = 20;
        private const int CopyPollingDelayMs = 20;
        private const int ClipboardRestoreDelayMs = 400;
        private const int SelectionCancelDelayMs = 20;

        public static volatile bool IsConverting = false;

        public static void RunOnSTA(Action action)
        {
            Thread thread = new Thread(() => {
                try 
                { 
                    action(); 
                }
                // [이번 수정: 백그라운드 스레드에서 발생하는 처리되지 않은 예외로 인한 앱 비정상 종료를 원천 방지합니다.]
                catch (Exception) { }
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        public static void TransformAndReplaceText(
            string lastOutputChar,
            Func<string, string> transformFunc,
            Action<string> setLastOutputChar,
            Action? modeSwitchAction = null)
        {
            if (!string.IsNullOrEmpty(lastOutputChar))
            {
                string toggled = transformFunc(lastOutputChar);
                if (toggled != lastOutputChar)
                {
                    MainForm.Instance?.ShowOverlay($"{lastOutputChar[0]}→{toggled[0]}");
                    setLastOutputChar(toggled);
                    GlobalInputHook.SendReplacement(1, toggled);
                    return;
                }
                modeSwitchAction?.Invoke();
                return;
            }

            if (IsConverting) return;
            IsConverting = true;
            RunOnSTA(() =>
            {
                try
                {
                    string? selected = ReadSelectedText();
                    if (!string.IsNullOrEmpty(selected))
                    {
                        string toggled = transformFunc(selected);
                        if (toggled != selected)
                        {
                            MainForm.Instance?.ShowOverlay($"{selected[0]}→{toggled[0]}");
                            setLastOutputChar("");
                            GlobalInputHook.SendReplacement(0, toggled);
                            return;
                        }
                        else if (modeSwitchAction == null)
                        {
                            CancelSelection();
                        }
                    }
                    
                    // [이번 수정: selected가 비어있고(선택한 글자가 없고), modeSwitchAction이 존재하면(HK 등) 호출하여 모드를 전환합니다.
                    // YN이나 PE 전환키처럼 modeSwitchAction이 null인 경우 호출되지 않아 아무런 반응(에러 포함)을 보이지 않습니다.]
                    modeSwitchAction?.Invoke();
                }
                catch (Exception) { /* 예외 발생 시 비정상 종료 방지 */ }
                finally { IsConverting = false; }
            });
        }

        public static string? ReadSelectedText()
        {
            try
            {
                IsConverting = true;
                
                try
                {
                    var focusedElement = AutomationElement.FocusedElement;
                    if (focusedElement != null && focusedElement.TryGetCurrentPattern(TextPattern.Pattern, out object patternObj))
                    {
                        var selections = ((TextPattern)patternObj).GetSelection();
                        if (selections != null && selections.Length > 0)
                        {
                            string text = selections[0].GetText(-1).Trim('\r', '\n', '\t', ' ', '\0');
                            if (text.Length > 0) return text;
                        }
                    }
                }
                catch { }

                bool shiftHeld = (NativeMethods.GetKeyState(InputVk.Shift) & 0x8000) != 0;
                string? saved = GetTextWin32();
                try
                {
                    ClearWin32();
                    Thread.Sleep(CopyPollingDelayMs);
                    SendCtrlC(shiftHeld);

                    string? copied = null;
                    for (int i = 0; i < CopyPollingRetryCount; i++)
                    {
                        Thread.Sleep(CopyPollingDelayMs);
                        copied = GetTextWin32();
                        if (!string.IsNullOrEmpty(copied)) break;
                    }

                    RestoreClipboardAsync(saved);

                    if (!string.IsNullOrEmpty(copied))
                    {
                        string cleanCopied = copied.Trim('\r', '\n', '\t', ' ', '\0');
                        if (cleanCopied.Length > 0) return cleanCopied;
                    }
                    return null;
                }
                // [이번 수정: ExternalException 외의 예상치 못한 모든 접근 에러를 포괄적으로 무시하여 안전성 확보]
                catch (Exception) { return null; } 
            }
            finally { IsConverting = false; }
        }

        private static void RestoreClipboardAsync(string? savedText)
        {
            Task.Run(() =>
            {
                Thread.Sleep(ClipboardRestoreDelayMs);
                RunOnSTA(() => {
                    try {
                        // [이번 수정: savedText가 빈 문자열("")일 때 SetText 호출 시 발생하는 ArgumentNullException을 방지하여 강제 종료를 막습니다.]
                        if (!string.IsNullOrEmpty(savedText)) Clipboard.SetText(savedText);
                        else Clipboard.Clear();
                    } catch (Exception) { } // 모든 클립보드 예외 무시
                });
            });
        }

        public static void CancelSelection()
        {
            try { bool shiftHeld = (NativeMethods.GetKeyState(InputVk.Shift) & 0x8000) != 0; SendRight(shiftHeld); Thread.Sleep(SelectionCancelDelayMs); }
            catch (Exception) { }
        }

        private static void SendRight(bool shiftHeld)
        {
            var inputs = new List<NativeMethods.INPUT>();
            if (shiftHeld) inputs.Add(MakeKeyUp(InputVk.Shift));
            inputs.Add(MakeKeyDown(InputVk.Right)); inputs.Add(MakeKeyUp(InputVk.Right));
            if (shiftHeld) inputs.Add(MakeKeyDown(InputVk.Shift));
            SendInputsSafe(inputs);
        }

        private static void SendCtrlC(bool shiftHeld)
        {
            var inputs = new List<NativeMethods.INPUT>();
            if (shiftHeld) inputs.Add(MakeKeyUp(InputVk.Shift));
            inputs.Add(MakeKeyDown(InputVk.Ctrl)); inputs.Add(MakeKeyDown(InputVk.vk_C));
            inputs.Add(MakeKeyUp(InputVk.vk_C)); inputs.Add(MakeKeyUp(InputVk.Ctrl));
            if (shiftHeld) inputs.Add(MakeKeyDown(InputVk.Shift));
            SendInputsSafe(inputs);
        }

        private static void SendInputsSafe(List<NativeMethods.INPUT> inputs)
        {
            GlobalInputHook.IsSending = true; 
            NativeMethods.SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<NativeMethods.INPUT>());
            GlobalInputHook.IsSending = false; 
        }

        private static NativeMethods.INPUT MakeKeyDown(ushort vk) => new NativeMethods.INPUT { type = NativeMethods.INPUT_KEYBOARD, U = new NativeMethods.InputUnion { ki = new NativeMethods.KEYBDINPUT { wVk = vk } } };
        private static NativeMethods.INPUT MakeKeyUp(ushort vk) => new NativeMethods.INPUT { type = NativeMethods.INPUT_KEYBOARD, U = new NativeMethods.InputUnion { ki = new NativeMethods.KEYBDINPUT { wVk = vk, dwFlags = NativeMethods.KEYEVENTF_KEYUP } } };

        public static string? GetTextWin32()
        {
            try
            {
                if (!NativeMethods.IsClipboardFormatAvailable(ClipboardUnicodeTextFormat)) return null;
                bool opened = false;
                for (int i = 0; i < ClipboardOpenRetryCount; i++) { Thread.Sleep(ClipboardOpenRetryDelayMs); if (NativeMethods.OpenClipboard(IntPtr.Zero)) { opened = true; break; } }
                if (!opened) return null;
                
                string? result = null;
                IntPtr hGlobal = NativeMethods.GetClipboardData(ClipboardUnicodeTextFormat);
                if (hGlobal != IntPtr.Zero)
                {
                    IntPtr ptr = NativeMethods.GlobalLock(hGlobal);
                    if (ptr != IntPtr.Zero)
                    {
                        result = Marshal.PtrToStringUni(ptr);
                        NativeMethods.GlobalUnlock(hGlobal);
                    }
                }
                NativeMethods.CloseClipboard();
                return result;
            }
            catch (Exception) { return null; }
        }

        public static bool ClearWin32()
        {
            for (int i = 0; i < ClipboardOpenRetryCount; i++)
            {
                try { if (NativeMethods.OpenClipboard(IntPtr.Zero)) { NativeMethods.EmptyClipboard(); NativeMethods.CloseClipboard(); return true; } } catch (Exception) { }
                Thread.Sleep(ClipboardOpenRetryDelayMs);
            }
            return false;
        }
    }
    
    #endregion

    #region [ 3. 공용 데이터: 일본어 변환 체인 (Japanese Shared) ]
    /// <summary>
    /// 일본어 입력 처리에 공통으로 사용되는 가나 변환 맵 및 로직입니다.
    /// </summary>    
    internal static class JapaneseShared
    {
        public static readonly Dictionary<string, string> HiraToKata = new()
        {
            {"あ","ア"},{"い","イ"},{"う","ウ"},{"え","エ"},{"お","オ"},
            {"か","カ"},{"き","キ"},{"く","ク"},{"け","ケ"},{"こ","コ"},
            {"さ","サ"},{"し","シ"},{"す","ス"},{"せ","セ"},{"そ","ソ"},
            {"た","タ"},{"ち","チ"},{"つ","ツ"},{"て","テ"},{"と","ト"},
            {"な","ナ"},{"に","ニ"},{"ぬ","ヌ"},{"ね","ネ"},{"の","ノ"},
            {"は","ハ"},{"ひ","ヒ"},{"ふ","フ"},{"へ","ヘ"},{"ほ","ホ"},
            {"ま","マ"},{"み","ミ"},{"む","ム"},{"め","メ"},{"も","モ"},
            {"ら","ラ"},{"り","リ"},{"る","ル"},{"れ","レ"},{"ろ","ロ"},
            {"や","ヤ"},{"ゆ","ユ"},{"よ","ヨ"},{"わ","ワ"},{"を","ヲ"},
            {"が","ガ"},{"ぎ","ギ"},{"ぐ","グ"},{"げ","ゲ"},{"ご","ゴ"},
            {"ざ","ザ"},{"じ","ジ"},{"ず","ズ"},{"ぜ","ゼ"},{"ぞ","ゾ"},
            {"だ","ダ"},{"ぢ","ヂ"},{"づ","ヅ"},{"で","デ"},{"ど","ド"},
            {"ば","バ"},{"び","ビ"},{"ぶ","ブ"},{"べ","ベ"},{"ぼ","ボ"},
            {"ぱ","パ"},{"ぴ","ピ"},{"ぷ","プ"},{"ぺ","ペ"},{"ぽ","ポ"},
            {"ぁ","ァ"},{"ぃ","ィ"},{"ぅ","ゥ"},{"ぇ","ェ"},{"ぉ","ォ"},
            {"ゃ","ャ"},{"ゅ","ュ"},{"ょ","ョ"},{"ゎ","ヮ"},{"っ","ッ"},
            {"ん","ン"},{"ゔ","ヴ"}
        };

        public static readonly Dictionary<string, string> KataToHira = HiraToKata.ToDictionary(kv => kv.Value, kv => kv.Key);

        public static readonly Dictionary<string, string> TransformMap = new()
        {
            // 히라가나
            {"あ","ぁ"},{"ぁ","あ"},{"い","ぃ"},{"ぃ","い"},{"え","ぇ"},{"ぇ","え"},{"お","ぉ"},{"ぉ","お"},{"う","ゔ"},{"ゔ","ぅ"},{"ぅ","う"},
            {"や","ゃ"},{"ゃ","や"},{"ゆ","ゅ"},{"ゅ","ゆ"},{"よ","ょ"},{"ょ","よ"},{"わ","ゎ"},{"ゎ","わ"},
            {"か","が"},{"が","か"},{"き","ぎ"},{"ぎ","き"},{"く","ぐ"},{"ぐ","く"},{"け","げ"},{"げ","け"},{"こ","ご"},{"ご","こ"},
            {"さ","ざ"},{"ざ","さ"},{"し","じ"},{"じ","し"},{"す","ず"},{"ず","す"},{"せ","ぜ"},{"ぜ","せ"},{"そ","ぞ"},{"ぞ","そ"},
            {"た","だ"},{"だ","た"},{"ち","ぢ"},{"ぢ","ち"},{"て","で"},{"で","て"},{"つ","づ"},{"づ","っ"},{"っ","つ"},{"と","ど"},{"ど","と"},
            {"は","ば"},{"ば","ぱ"},{"ぱ","は"},{"ひ","び"},{"び","ぴ"},{"ぴ","ひ"},{"ふ","ぶ"},{"ぶ","ぷ"},{"ぷ","ふ"},{"へ","べ"},{"べ","ぺ"},{"ぺ","へ"},{"ほ","ぼ"},{"ぼ","ぽ"},{"ぽ","ほ"},
            // 가타카나
            {"ア","ァ"},{"ァ","ア"},{"イ","ィ"},{"ィ","イ"},{"エ","ェ"},{"ェ","エ"},{"オ","ォ"},{"ォ","オ"},{"ウ","ヴ"},{"ヴ","ゥ"},{"ゥ","ウ"},
            {"ヤ","ャ"},{"ャ","ヤ"},{"ユ","ュ"},{"ュ","ユ"},{"ヨ","ョ"},{"ョ","ヨ"},{"ワ","ヮ"},{"ヮ","ワ"},
            {"カ","ガ"},{"ガ","カ"},{"キ","ギ"},{"ギ","キ"},{"ク","グ"},{"グ","ク"},{"ケ","ゲ"},{"ゲ","ケ"},{"コ","ゴ"},{"ゴ","コ"},
            {"サ","ザ"},{"ザ","サ"},{"シ","ジ"},{"ジ","シ"},{"ス","ズ"},{"ズ","ス"},{"セ","ゼ"},{"ゼ","セ"},{"ソ","ゾ"},{"ゾ","ソ"},
            {"タ","ダ"},{"ダ","タ"},{"チ","ヂ"},{"ヂ","チ"},{"テ","デ"},{"デ","テ"},{"ツ","ヅ"},{"ヅ","ッ"},{"ッ","ツ"},{"ト","ド"},{"ド","ト"},
            {"ハ","バ"},{"バ","パ"},{"パ","ハ"},{"ヒ","ビ"},{"ビ","ピ"},{"ピ","ヒ"},{"フ","ブ"},{"ブ","プ"},{"プ","フ"},{"ヘ","ベ"},{"ベ","ペ"},{"ペ","ヘ"},{"ホ","ボ"},{"ボ","ポ"},{"ポ","ホ"},
        };

        // [수정사항 2] 변수명 명확화: _ynChainHira -> _yoonChainHiragana
        private static readonly Dictionary<string, string?[]> _yoonChainHiragana = new()
        {
            // 큰글자(모음, 청음)
            {"あ", new string?[]{"あ",null,null,"ぁ"}}, {"い", new string?[]{"い",null,null,"ぃ"}}, {"う", new string?[]{"う","ゔ",null,"ぅ"}}, {"え", new string?[]{"え",null,null,"ぇ"}}, {"お", new string?[]{"お",null,null,"ぉ"}},
            {"や", new string?[]{"や",null,null,"ゃ"}}, {"ゆ", new string?[]{"ゆ",null,null,"ゅ"}}, {"よ", new string?[]{"よ",null,null,"ょ"}}, {"わ", new string?[]{"わ",null,null,"ゎ"}},
            {"か", new string?[]{"か","が",null,null}}, {"き", new string?[]{"き","ぎ",null,null}}, {"く", new string?[]{"く","ぐ",null,null}}, {"け", new string?[]{"け","げ",null,null}}, {"こ", new string?[]{"こ","ご",null,null}},
            {"さ", new string?[]{"さ","ざ",null,null}}, {"し", new string?[]{"し","じ",null,null}}, {"す", new string?[]{"す","ず",null,null}}, {"せ", new string?[]{"せ","ぜ",null,null}}, {"そ", new string?[]{"そ","ぞ",null,null}},
            {"た", new string?[]{"た","だ",null,null}}, {"ち", new string?[]{"ち","ぢ",null,null}}, {"て", new string?[]{"て","で",null,null}}, {"つ", new string?[]{"つ","づ",null,"っ"}}, {"と", new string?[]{"と","ど",null,null}},
            {"は", new string?[]{"は","ば","ぱ",null}}, {"ひ", new string?[]{"ひ","び","ぴ",null}}, {"ふ", new string?[]{"ふ","ぶ","ぷ",null}}, {"へ", new string?[]{"へ","べ","ぺ",null}}, {"ほ", new string?[]{"ほ","ぼ","ぽ",null}},
            // 큰글자(탁음, 반탁음, V음)
            {"が", new string?[]{"か","が",null,null}}, {"ぎ", new string?[]{"き","ぎ",null,null}}, {"ぐ", new string?[]{"く","ぐ",null,null}}, {"げ", new string?[]{"け","げ",null,null}}, {"ご", new string?[]{"こ","ご",null,null}},
            {"ざ", new string?[]{"さ","ざ",null,null}}, {"じ", new string?[]{"し","じ",null,null}}, {"ず", new string?[]{"す","ず",null,null}}, {"ぜ", new string?[]{"せ","ぜ",null,null}}, {"ぞ", new string?[]{"そ","ぞ",null,null}},
            {"だ", new string?[]{"た","だ",null,null}}, {"ぢ", new string?[]{"ち","ぢ",null,null}}, {"づ", new string?[]{"つ","づ",null,"っ"}}, {"で", new string?[]{"て","で",null,null}}, {"ど", new string?[]{"と","ど",null,null}},
            {"ば", new string?[]{"は","ば","ぱ",null}}, {"び", new string?[]{"ひ","び","ぴ",null}}, {"ぶ", new string?[]{"ふ","ぶ","ぷ",null}}, {"べ", new string?[]{"へ","べ","ぺ",null}}, {"ぼ", new string?[]{"ほ","ぼ","ぽ",null}},
            {"ぱ", new string?[]{"は","ば","ぱ",null}}, {"ぴ", new string?[]{"ひ","び","ぴ",null}}, {"ぷ", new string?[]{"ふ","ぶ","ぷ",null}}, {"ぺ", new string?[]{"へ","べ","ぺ",null}}, {"ぽ", new string?[]{"ほ","ぼ","ぽ",null}},
            {"ゔ", new string?[]{"う","ゔ",null,"ぅ"}},
            // 작은글자(모음, 요음, 촉음)
            {"ぁ", new string?[]{"あ",null,null,"ぁ"}}, {"ぃ", new string?[]{"い",null,null,"ぃ"}}, {"ぅ", new string?[]{"う","ゔ",null,"ぅ"}}, {"ぇ", new string?[]{"え",null,null,"ぇ"}}, {"ぉ", new string?[]{"お",null,null,"ぉ"}},
            {"ゃ", new string?[]{"や",null,null,"ゃ"}}, {"ゅ", new string?[]{"ゆ",null,null,"ゅ"}}, {"ょ", new string?[]{"よ",null,null,"ょ"}}, {"ゎ", new string?[]{"わ",null,null,"ゎ"}}, 
            {"っ", new string?[]{"つ","づ",null,"っ"}},
        };

        private static readonly Dictionary<string, string?[]> _yoonChainKatakana = new();

        static JapaneseShared()
        {
            foreach (var kv in _yoonChainHiragana)
            {
                if (!HiraToKata.TryGetValue(kv.Key, out string? kataKey)) continue;
                string?[] hiraChain = kv.Value;
                string?[] kataChain = new string?[4];
                for (int i = 0; i < 4; i++)
                    kataChain[i] = hiraChain[i] == null ? null : (HiraToKata.TryGetValue(hiraChain[i]!, out string? k) ? k : hiraChain[i]);
                _yoonChainKatakana[kataKey] = kataChain;
            }
        }

        private static int GetYoonCategory(string ch)
        {
            if (string.IsNullOrEmpty(ch)) return -1;
            if (_yoonChainHiragana.TryGetValue(ch, out string?[]? hChain))
                for (int i = 0; i < 4; i++) if (hChain[i] == ch) return i;
            if (_yoonChainKatakana.TryGetValue(ch, out string?[]? kChain))
                for (int i = 0; i < 4; i++) if (kChain[i] == ch) return i;
            return -1;
        }

        private static string GetNextYoonChar(string ch)
        {
            if (string.IsNullOrEmpty(ch)) return ch;
            string?[]? chain = _yoonChainHiragana.ContainsKey(ch) ? _yoonChainHiragana[ch] : (_yoonChainKatakana.ContainsKey(ch) ? _yoonChainKatakana[ch] : null);
            if (chain == null) return ch;

            int curCat = Array.IndexOf(chain, ch);
            if (curCat < 0) return ch;

            for (int step = 1; step <= 4; step++)
            {
                int nextCat = (curCat + step) % 4;
                if (chain[nextCat] != null) return chain[nextCat]!;
            }
            return ch;
        }

        private static string ConvertYoonToCategory(string ch, int toCat)
        {
            if (string.IsNullOrEmpty(ch)) return ch;
            string?[]? chain = _yoonChainHiragana.ContainsKey(ch) ? _yoonChainHiragana[ch] : (_yoonChainKatakana.ContainsKey(ch) ? _yoonChainKatakana[ch] : null);
            if (chain != null && toCat >= 0 && toCat < 4 && chain[toCat] != null) return chain[toCat]!;
            return ch;
        }

        // [수정사항 2] 메서드명 명확화: ApplyHkMulti -> ApplyHiraganaKatakanaTransformation
        public static string ApplyHiraganaKatakanaTransformation(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (text.Length == 1) return HiraToKata.ContainsKey(text) ? HiraToKata[text] : (KataToHira.ContainsKey(text) ? KataToHira[text] : text);

            bool isTargetKata = HiraToKata.ContainsKey(text[0].ToString());
            bool isTargetHira = KataToHira.ContainsKey(text[0].ToString());
            if (!isTargetKata && !isTargetHira) return text;

            StringBuilder sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                string cs = c.ToString();
                sb.Append(isTargetKata ? (HiraToKata.ContainsKey(cs) ? HiraToKata[cs] : cs) : (KataToHira.ContainsKey(cs) ? KataToHira[cs] : cs));
            }
            return sb.ToString();
        }

        // [수정사항 2] 메서드명 명확화: ApplyYnMulti -> ApplyYoonTransformation
        public static string ApplyYoonTransformation(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (text.Length == 1)
            {
                if ("んをンヲ".Contains(text)) return text;
                return GetNextYoonChar(text);
            }

            string first = text[0].ToString();
            if ("んをンヲ".Contains(first)) return text;

            int fromCat = GetYoonCategory(first);
            if (fromCat < 0) return text;

            string firstConverted = GetNextYoonChar(first);
            int toCat = GetYoonCategory(firstConverted);
            if (firstConverted == first) return text;

            StringBuilder sb = new StringBuilder(text.Length);
            sb.Append(firstConverted);
            
            for (int i = 1; i < text.Length; i++)
            {
                string c = text[i].ToString();
                if ("んをンヲ".Contains(c)) { sb.Append(c); continue; }

                int cCat = GetYoonCategory(c);
                if (cCat < 0) { sb.Append(c); continue; }

                sb.Append((cCat == fromCat) ? ConvertYoonToCategory(c, toCat) : c);
            }
            return sb.ToString();
        }
    }
    #endregion

    #region [ 4. 언어 프로세서: Pali어 ]
    internal class PaliProcessor : IKeyProcessor
    {
        private bool _isVirtualShift = false;
        public bool IsVirtualShift => _isVirtualShift;
        public int CurrentLayer => 1; 

        public void ToggleVirtualShift() => _isVirtualShift = !_isVirtualShift;

        public bool ProcessHanjaKey(IntPtr hFore, bool capsOn, bool isHangulMode)
        {
            if (isHangulMode && capsOn) { 
                ImeState.SetHangulState(hFore, false); 
                NativeMethods.SimulateCapsLock(); 
                MainForm.Instance?.ShowOverlay("영어 소문자 모드"); 
                return true; 
            }
            return false;
        }

        public bool ProcessKeyDown(int vkCode, bool isShift, bool capsOn, IntPtr hFore, bool isHangulMode)
        {
            // [이번 수정] Key2 상태일 때 Shift 강제 적용
            if (AppConfig.IsOverlayKey2Mode) isShift = true;

            if (!capsOn || !isHangulMode) return false;
            if (vkCode is >= 0x21 and <= 0x28) { if (!isShift) PaliMap.SetLastOutputChar(""); return false; }
            if (vkCode == InputVk.vk_P) { PaliMap.HandlePaliTransformation(); return true; }
            if (TextSelectionUtils.IsConverting) return true;

            string? keyResult = PaliMap.ProcessKey(vkCode, isShift ^ _isVirtualShift);
            
            // [이번 수정] 맵핑되지 않은 기호/숫자열 뿐만 아니라 문자도 Key2 등으로 Shift된 경우 동적 맵핑 활용
            if (keyResult == null && isShift && KeyboardUtils.IsSymbolOrNumberOrLetter(vkCode))
            {
                keyResult = KeyboardUtils.GetChar(vkCode, true);
            }

            if (keyResult == null) return (vkCode is >= 0x41 and <= 0x5A or >= 0x30 and <= 0x39);
            
            if (keyResult.Length > 0)
            {
                GlobalInputHook.IsSending = true; 
                NativeMethods.SendUnicodeString(keyResult); 
                GlobalInputHook.IsSending = false; 
            }
            return true;
        }

        public void OnMouseClick() => PaliMap.SetLastOutputChar("");
    }

    internal static class PaliMap
    {
        private static string _lastOutputChar = "";
        
        // 1. 단어 형태 변환 규칙 (TransformationRules)
        private static readonly Dictionary<string, string> TransformationRules = new()
        {
            {"a","ā"}, {"ā","a"}, {"A","Ā"}, {"Ā","A"}, {"s","ṣ"}, {"ṣ","ś"}, {"ś","s"}, {"S","Ṣ"}, {"Ṣ","Ś"}, {"Ś","S"},
            {"d","ḍ"}, {"ḍ","d"}, {"D","Ḍ"}, {"Ḍ","D"}, {"r","ṛ"}, {"ṛ","ṝ"}, {"ṝ","r"}, {"R","Ṛ"}, {"Ṛ","Ṝ"}, {"Ṝ","R"},
            {"t","ṭ"}, {"ṭ","t"}, {"T","Ṭ"}, {"Ṭ","T"}, {"u","ū"}, {"ū","u"}, {"U","Ū"}, {"Ū","U"},
            {"h","ḥ"}, {"ḥ","h"}, {"H","Ḥ"}, {"Ḥ","H"}, {"i","ī"}, {"ī","i"}, {"I","Ī"}, {"Ī","I"},
            {"l","ḷ"}, {"ḷ","ḹ"}, {"ḹ","l"}, {"L","Ḷ"}, {"Ḷ","Ḹ"}, {"Ḹ","L"}, {"m","ṃ"}, {"ṃ","m"}, {"M","Ṃ"}, {"Ṃ","M"},
            {"n","ṇ"}, {"ṇ","ṅ"}, {"ṅ","ñ"}, {"ñ","n"}, {"N","Ṇ"}, {"Ṇ","Ṅ"}, {"Ṅ","Ñ"}, {"Ñ","N"}
        };

        // 2. 다중 문자 일괄 전환을 위한 카테고리 매핑 (Japanese YN 전환과 동일한 로직)
        // 인덱스: 0:None, 1:Dot below, 2:Macron, 3:Dot below+Macron, 4:Dot above, 5:Accent, 6:Tilde
        private static readonly Dictionary<string, string?[]> _paliChains = new()
        {
            {"a", new string?[]{"a", null, "ā", null, null, null, null}},
            {"d", new string?[]{"d", "ḍ", null, null, null, null, null}},
            {"h", new string?[]{"h", "ḥ", null, null, null, null, null}},
            {"i", new string?[]{"i", null, "ī", null, null, null, null}},
            {"l", new string?[]{"l", "ḷ", null, "ḹ", null, null, null}},
            {"m", new string?[]{"m", "ṃ", null, null, null, null, null}},
            {"n", new string?[]{"n", "ṇ", null, null, "ṅ", null, "ñ"}},
            {"t", new string?[]{"t", "ṭ", null, null, null, null, null}},
            {"u", new string?[]{"u", null, "ū", null, null, null, null}},
            {"r", new string?[]{"r", "ṛ", null, "ṝ", null, null, null}},
            {"s", new string?[]{"s", "ṣ", null, null, null, "ś", null}},
            {"A", new string?[]{"A", null, "Ā", null, null, null, null}},
            {"D", new string?[]{"D", "Ḍ", null, null, null, null, null}},
            {"H", new string?[]{"H", "Ḥ", null, null, null, null, null}},
            {"I", new string?[]{"I", null, "Ī", null, null, null, null}},
            {"L", new string?[]{"L", "Ḷ", null, "Ḹ", null, null, null}},
            {"M", new string?[]{"M", "Ṃ", null, null, null, null, null}},
            {"N", new string?[]{"N", "Ṇ", null, null, "Ṅ", null, "Ñ"}},
            {"T", new string?[]{"T", "Ṭ", null, null, null, null, null}},
            {"U", new string?[]{"U", null, "Ū", null, null, null, null}},
            {"R", new string?[]{"R", "Ṛ", null, "Ṝ", null, null, null}},
            {"S", new string?[]{"S", "Ṣ", null, null, null, "Ś", null}},
        };

        private static readonly Dictionary<string, int> _paliCategoryMap = new();
        private static readonly Dictionary<string, string?[]> _paliReverseChainMap = new();

        static PaliMap()
        {
            foreach (var kv in _paliChains)
            {
                string?[] chain = kv.Value;
                for (int i = 0; i < 7; i++)
                {
                    if (chain[i] != null)
                    {
                        _paliCategoryMap[chain[i]!] = i;
                        _paliReverseChainMap[chain[i]!] = chain;
                    }
                }
            }
        }

        // 3. 키보드 자판 레이아웃 매핑
        public static readonly IReadOnlyDictionary<int, (string Lower, string Upper)> Map = new Dictionary<int, (string, string)>
        {
            { 0x31, ("①", "¹") }, { 0x32, ("②", "²") }, { 0x33, ("③", "³") }, { 0x34, ("④", "⁴") }, { 0x35, ("⑤", "†") },
            { 0x36, ("⑥", "‡") }, { 0x37, ("⑦", "§") }, { 0x38, ("⑧", "*") }, { 0x39, ("⑨", "(") }, { 0x30, ("⑩", ")") },
            { 0x51, ("→", "←") }, { 0x57, ("ś", "Ś") }, { 0x45, ("ṝ", "Ṝ") }, { 0x52, ("ṛ", "Ṛ") }, { 0x54, ("ṭ", "Ṭ") },
            { 0x59, ("※", "√") }, { 0x55, ("ū", "Ū") }, { 0x49, ("ī", "Ī") }, { 0x4F, ("ḹ", "Ḹ") }, { 0x41, ("ā", "Ā") },
            { 0x53, ("ṣ", "Ṣ") }, { 0x44, ("ḍ", "Ḍ") }, { 0x46, ("\u2026", "–") }, { 0x47, ("○", "◎") }, { 0x48, ("ḥ", "Ḥ") },
            { 0x4A, ("ñ", "Ñ") }, { 0x4B, ("·", "•") }, { 0x4C, ("ḷ", "Ḷ") }, { 0xBA, (";", ":") }, { 0x5A, ("\u300C", "\u3010") }, 
            { 0x58, ("\u300D", "\u3011") }, { 0x43, ("\u300E", "\u300A") }, { 0x56, ("\u300F", "\u300B") }, { 0x42, ("ṅ", "Ṅ") }, 
            { 0x4E, ("ṇ", "Ṇ") }, { 0x4D, ("ṃ", "Ṃ") }, { 0xBC, (",", "<") }, { 0xBE, (".", ">") }, { 0xBF, ("/", "?") }
        };

        public static void SetLastOutputChar(string ch) => _lastOutputChar = ch;

        public static string? ProcessKey(int vkCode, bool isShift)
        {
            if (Map.TryGetValue(vkCode, out var val))
            {
                _lastOutputChar = isShift ? val.Upper : val.Lower; 
                MainForm.Instance?.ShowOverlay(_lastOutputChar);
                return _lastOutputChar;
            }
            _lastOutputChar = ""; return null;
        }

        // [수정사항 1] 공용 헬퍼를 사용하도록 로직 단순화
        public static void HandlePaliTransformation()
        {
            TextSelectionUtils.TransformAndReplaceText(
                _lastOutputChar, 
                ApplyPaliTransformation, 
                SetLastOutputChar
            );
        }

        private static string ApplyPaliTransformation(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            // 한 글자일 경우 O(1) Dictionary로 빠른 전환
            if (text.Length == 1) 
                return TransformationRules.TryGetValue(text, out string? res) ? res : text;

            string first = text[0].ToString();
            
            // 첫 번째 글자의 원본 카테고리와 타겟 변환 정보 가져오기
            if (!_paliCategoryMap.TryGetValue(first, out int fromCat)) return text;
            if (!TransformationRules.TryGetValue(first, out string? firstConverted)) return text;
            if (!_paliCategoryMap.TryGetValue(firstConverted, out int toCat)) return text; 

            StringBuilder sb = new StringBuilder(text.Length);
            sb.Append(firstConverted);
            
            for (int i = 1; i < text.Length; i++)
            {
                string c = text[i].ToString();
                
                // 나머지 글자가 첫 번째 글자와 동일한 유형(Category)인지 확인 후 일괄 전환
                if (_paliCategoryMap.TryGetValue(c, out int cCat) && cCat == fromCat)
                {
                    if (_paliReverseChainMap.TryGetValue(c, out string?[]? chain) && chain[toCat] != null)
                    {
                        sb.Append(chain[toCat]);
                    }
                    else
                    {
                        sb.Append(c);   // 타겟 카테고리에 해당하는 글자가 없으면 원본 유지
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
    
    #endregion

    #region [ 5. 언어 프로세서: 공학용 특수기호 (Engineer) ]
    internal class EngineerProcessor : IKeyProcessor
    {
        private bool _isVirtualShift = false;
        public bool IsVirtualShift => _isVirtualShift;
        public int CurrentLayer => 1;
        public void ToggleVirtualShift() => _isVirtualShift = !_isVirtualShift;

        public bool ProcessHanjaKey(IntPtr hFore, bool capsOn, bool isHangulMode)
        {
            if (isHangulMode && capsOn) { 
                ImeState.SetHangulState(hFore, false); 
                NativeMethods.SimulateCapsLock(); 
                MainForm.Instance?.ShowOverlay("영어 소문자 모드");
                return true; 
            }
            return false;
        }

        public bool ProcessKeyDown(int vkCode, bool isShift, bool capsOn, IntPtr hFore, bool isHangulMode)
        {
            // [이번 수정] Key2 상태일 때 Shift 강제 적용
            if (AppConfig.IsOverlayKey2Mode) isShift = true;

            if (!capsOn || !isHangulMode) return false;
            if (vkCode is >= 0x21 and <= 0x28) return false;
            if (TextSelectionUtils.IsConverting) return true;

            if (EngineerMap.Map.TryGetValue(vkCode, out var item))
            {
                string targetStr = (isShift ^ _isVirtualShift) ? item.Shift : item.Normal;
                GlobalInputHook.IsSending = true; NativeMethods.SendUnicodeString(targetStr); GlobalInputHook.IsSending = false; 
                MainForm.Instance?.ShowOverlay(targetStr);
                return true;
            }
            
            // [이번 수정] 맵핑되지 않은 기호 및 문자 강제 Shift 처리
            if (isShift && KeyboardUtils.IsSymbolOrNumberOrLetter(vkCode))
            {
                string? ch = KeyboardUtils.GetChar(vkCode, true);
                if (!string.IsNullOrEmpty(ch))
                {
                    GlobalInputHook.IsSending = true; NativeMethods.SendUnicodeString(ch); GlobalInputHook.IsSending = false;
                    MainForm.Instance?.ShowOverlay(ch);
                    return true;
                }
            }
            
            return (vkCode is >= 0x41 and <= 0x5A or >= 0x30 and <= 0x39);
        }

        public void OnMouseClick() { }
    }

    internal static class EngineerMap
    {
        // =========================================================================
        // [ 사용자 커스텀 설정 영역: 공학용 특수기호 매핑 (EngineerMap) ]
        // =========================================================================
        // FEA(유한요소해석) 모델링, Abaqus 사용자 서브루틴(UHARD, USDFLD 등) 작성, 
        // 또는 금속 야금학(25Cr1Mo1V 강종 등)의 재료 물성치 입력 시 자주 사용하는 
        // 그리스 문자(응력 σ, 변형률 ε 등) 및 특수 기호를 작업 환경에 맞게 매핑할 수 있습니다.
        // 첫 번째 값: 일반 입력(Normal), 두 번째 값: Shift 입력(Shift)        
        public static readonly IReadOnlyDictionary<int, (string Normal, string Shift)> Map = new Dictionary<int, (string, string)>
        {
            { 0x31, ("ⓐ", "↕") }, { 0x32, ("ⓑ", "↔") }, { 0x33, ("ⓒ", "↓") }, { 0x34, ("ⓓ", "↑") }, { 0x35, ("ⓔ", "←") },
            { 0x36, ("ⓕ", "→") }, { 0x37, ("ⓖ", "∴") }, { 0x38, ("ⓗ", "⊂") }, { 0x39, ("ⓘ", "∈") }, { 0x30, ("ⓙ", "∩") },
            { 0x51, ("∞", "⊥") }, { 0x57, ("∝", "≠") }, { 0x45, ("ε", "≒") }, { 0x52, ("ρ", "√") }, { 0x54, ("τ", "±") },
            { 0x59, ("υ", "×") }, { 0x55, ("θ", "∙") }, { 0x49, ("π", "∫") }, { 0x4F, ("∂", "∬") }, { 0x50, ("∇", "∮") },
            { 0x41, ("α", "Θ") }, { 0x53, ("σ", "Σ") }, { 0x44, ("δ", "Δ") }, { 0x46, ("φ", "Φ") }, { 0x47, ("γ", "Γ") },
            { 0x48, ("η", "℄") }, { 0x4A, ("ξ", "°") }, { 0x4B, ("κ", "≤") }, { 0x4C, ("λ", "≥") }, { 0x5A, ("ζ", "Ξ") },
            { 0x58, ("χ", "Λ") }, { 0x43, ("ψ", "Ψ") }, { 0x56, ("ω", "Ω") }, { 0x42, ("β", "Π") }, { 0x4E, ("ν", "℃") }, { 0x4D, ("μ", "℉") }
        };
    }
    
    #endregion

    #region [ 6. 언어 프로세서: 일본어1, 일본어2 (Japanese 1 & 2) ]
    
    internal class Japanese1Processor : IKeyProcessor
    {
        public bool IsVirtualShift => Japanese1Map.IsKatakana;
        public int CurrentLayer => Japanese1Map.CurrentLayer;
        public void ToggleVirtualShift() => Japanese1Map.TogglePendingHiraKataModeOnly();
        public bool ProcessHanjaKey(IntPtr hFore, bool capsOn, bool isHangulMode)
        {
            if (isHangulMode && capsOn) { Japanese1Map.ToggleLayer(); return true; } 
            return false;
        }

        public bool ProcessKeyDown(int vkCode, bool isShift, bool capsOn, IntPtr hFore, bool isHangulMode)
        {
            // [이번 수정] Key2 상태일 때 Shift 강제 적용
            if (AppConfig.IsOverlayKey2Mode) isShift = true;

            // 모음 키를 y에서 m으로 교체하여 입력 판정 범위 갱신
            bool isVowelKey = vkCode is InputVk.vk_H or InputVk.vk_J or InputVk.vk_K or InputVk.vk_L or InputVk.vk_M;
            if (Japanese1Map.IsWaitingVowel && !isVowelKey)
            {
                if (vkCode == InputVk.vk_B) { Japanese1Map.TogglePendingHiraKata(); return true; }
                // 조합 대기 중 YN 글자 전환 키를 기존 P에서 N으로 교체
                if (vkCode == InputVk.vk_N) { Japanese1Map.TogglePendingYn(); return true; }

                string pending = Japanese1Map.PendingChar;
                
                Japanese1Map.Reset();   // 오버레이 타이머 리셋 및 화면 소거를 포함한 초기화
                
                if (vkCode == InputVk.Escape || vkCode == InputVk.Backspace) return true;

                if (pending.Length > 0) // 그 외 기타키 입력 시에는 대기하던 문자를 확정 송출
                {
                    GlobalInputHook.IsSending = true; NativeMethods.SendUnicodeString(pending); GlobalInputHook.IsSending = false; 
                }

                if (capsOn && isHangulMode)
                {
                    string? result = Japanese1Map.ProcessKey(vkCode, isShift);
                    if (result != null)
                    {
                        if (result.Length > 0) { 
                            GlobalInputHook.IsSending = true; NativeMethods.SendUnicodeString(result); GlobalInputHook.IsSending = false; 
                        }                        
                        return true;
                    }
                }
                return false;
            }

            // 방향키 등 처리 (최적화)
            if (vkCode is >= 0x21 and <= 0x28) { if (!isShift) Japanese1Map.SetLastOutputChar(""); return false; }
            if (vkCode == InputVk.vk_B && capsOn && isHangulMode) { Japanese1Map.HandleHiraganaKatakanaTransformation(); return true; }
            // 문자열 선택 상태 YN 글자 전환 단축키를 P에서 N으로 교체
            if (vkCode == InputVk.vk_N && capsOn && isHangulMode) { Japanese1Map.HandleYoonTransformation(); return true; }
            if (!capsOn || !isHangulMode) return false;
            if (TextSelectionUtils.IsConverting) return true;

            string? keyResult = Japanese1Map.ProcessKey(vkCode, isShift);
            if (keyResult == null) { Japanese1Map.SetLastOutputChar(""); return false; }

            if (keyResult.Length > 0)
            {
                GlobalInputHook.IsSending = true; 
                NativeMethods.SendUnicodeString(keyResult); 
                GlobalInputHook.IsSending = false; 
            }
            return true;
        }

        // 마우스 클릭으로 이탈 시에도 조합모드 리셋 및 오버레이 지우기 발동
        public void OnMouseClick() 
        {
            if (Japanese1Map.IsWaitingVowel) Japanese1Map.Reset();
            Japanese1Map.SetLastOutputChar("");
        }    
    }

    internal static class Japanese1Map
    {
        private const int VK_Q = 0x51, VK_W = 0x57, VK_E = 0x45, VK_R = 0x52, VK_A = 0x41, VK_S = 0x53, VK_D = 0x44, VK_F = 0x46;
        private const int VK_Z = 0x5A, VK_X = 0x58, VK_C = 0x43, VK_V = 0x56, VK_H = 0x48, VK_J = 0x4A, VK_K = 0x4B, VK_L = 0x4C;
        private const int VK_Y = 0x59, VK_T = 0x54, VK_U = 0x55, VK_I = 0x49, VK_O = 0x4F, VK_G = 0x47, VK_N = 0x4E, VK_M = 0x4D, VK_B = 0x42, VK_P = 0x50;

        private static readonly HashSet<int> _consonantKeys = new() { VK_Q, VK_W, VK_E, VK_R, VK_A, VK_S, VK_D, VK_F, VK_Z, VK_X, VK_C, VK_V };
        // 오른손 모음 배치를 H, J, K, L, M 구조로 갱신
        private static readonly HashSet<int> _vowelKeys = new() { VK_H, VK_J, VK_K, VK_L, VK_M };

        // Layer 1과 Layer 2 자음 재배치 대응을 위해 다차원 튜플 구조 맵 구축 (Layer 구분 제거)
        private static readonly Dictionary<(int Con, int Vow), (string Hira, string Kata)> _combineMap = new()
        {
            // B Family (Q)
            { (VK_Q, VK_H), ("ば","バ") }, { (VK_Q, VK_J), ("び","ビ") }, { (VK_Q, VK_K), ("ぶ","ブ") }, { (VK_Q, VK_M), ("べ","ベ") }, { (VK_Q, VK_L), ("ぼ","ボ") },
            // Z Family (W)
            { (VK_W, VK_H), ("ざ","ザ") }, { (VK_W, VK_J), ("じ","ジ") }, { (VK_W, VK_K), ("ず","ズ") }, { (VK_W, VK_M), ("ぜ","ゼ") }, { (VK_W, VK_L), ("ぞ","ゾ") },
            // G Family (E)
            { (VK_E, VK_H), ("が","ガ") }, { (VK_E, VK_J), ("ぎ","ギ") }, { (VK_E, VK_K), ("ぐ","グ") }, { (VK_E, VK_M), ("げ","ゲ") }, { (VK_E, VK_L), ("ご","ゴ") },
            // D Family (R)
            { (VK_R, VK_H), ("だ","ダ") }, { (VK_R, VK_J), ("ぢ","ヂ") }, { (VK_R, VK_K), ("づ","ヅ") }, { (VK_R, VK_M), ("で","デ") }, { (VK_R, VK_L), ("ど","ド") },
            // H Family (A)
            { (VK_A, VK_H), ("は","ハ") }, { (VK_A, VK_J), ("ひ","ヒ") }, { (VK_A, VK_K), ("ふ","フ") }, { (VK_A, VK_M), ("へ","ヘ") }, { (VK_A, VK_L), ("ほ","ホ") },
            // S Family (S)
            { (VK_S, VK_H), ("さ","サ") }, { (VK_S, VK_J), ("し","シ") }, { (VK_S, VK_K), ("す","ス") }, { (VK_S, VK_M), ("せ","セ") }, { (VK_S, VK_L), ("そ","ソ") },
            // K Family (D)
            { (VK_D, VK_H), ("か","カ") }, { (VK_D, VK_J), ("き","キ") }, { (VK_D, VK_K), ("く","ク") }, { (VK_D, VK_M), ("け","ケ") }, { (VK_D, VK_L), ("こ","コ") },
            // T Family (F)
            { (VK_F, VK_H), ("た","タ") }, { (VK_F, VK_J), ("ち","チ") }, { (VK_F, VK_K), ("つ","ツ") }, { (VK_F, VK_M), ("て","テ") }, { (VK_F, VK_L), ("と","ト") },
            // P Family (Z)
            { (VK_Z, VK_H), ("ぱ","パ") }, { (VK_Z, VK_J), ("ぴ","ピ") }, { (VK_Z, VK_K), ("ぷ","プ") }, { (VK_Z, VK_M), ("ぺ","ペ") }, { (VK_Z, VK_L), ("ぽ","ポ") },
            // M Family (X)
            { (VK_X, VK_H), ("ま","マ") }, { (VK_X, VK_J), ("み","ミ") }, { (VK_X, VK_K), ("む","ム") }, { (VK_X, VK_M), ("め","メ") }, { (VK_X, VK_L), ("も","モ") },
            // R Family (C)
            { (VK_C, VK_H), ("ら","ラ") }, { (VK_C, VK_J), ("り","リ") }, { (VK_C, VK_K), ("る","ル") }, { (VK_C, VK_M), ("れ","レ") }, { (VK_C, VK_L), ("ろ","ロ") },
            // N Family (V)
            { (VK_V, VK_H), ("な","ナ") }, { (VK_V, VK_J), ("に","ニ") }, { (VK_V, VK_K), ("ぬ","ヌ") }, { (VK_V, VK_M), ("ね","ネ") }, { (VK_V, VK_L), ("の","ノ") },
        };

        // 단독 입력 문자 및 모음 배열 갱신 (오른손 전용 레이아웃 분리)
        private static readonly Dictionary<int, (string Hira, string Kata)> _soloMap = new()
        {
            { VK_T, ("っ","ッ") }, { VK_G, ("ん","ン") },
            { VK_Y, ("わ","ワ") }, { VK_U, ("を","ヲ") }, { VK_I, ("よ","ヨ") }, { VK_O, ("ゆ","ユ") }, { VK_P, ("や","ヤ") },
            { VK_H, ("あ","ア") }, { VK_J, ("い","イ") }, { VK_K, ("う","ウ") }, { VK_M, ("え","エ") }, { VK_L, ("お","オ") }
        };

        // 자음 키별 대표자음 화면 표시 이름 갱신
        private static readonly Dictionary<int, (string Hira, string Kata)> _previewMapL1 = new()
        {
            { VK_Q, ("ば","バ") }, { VK_W, ("ざ","ザ") }, { VK_E, ("が","ガ") }, { VK_R, ("だ","ダ") }, 
            { VK_A, ("は","ハ") }, { VK_S, ("さ","サ") }, { VK_D, ("か","カ") }, { VK_F, ("た","タ") }, 
            { VK_Z, ("ぱ","パ") }, { VK_X, ("ま","マ") }, { VK_C, ("ら","ラ") }, { VK_V, ("な","ナ") },
        };

        private static readonly Dictionary<int, (string Hira, string Kata)> _previewMapL2 = new()
        {
            { VK_Q, ("ば","バ") }, { VK_W, ("じ","ジ") }, { VK_E, ("が","ガ") }, { VK_R, ("で","デ") },
            { VK_A, ("は","ハ") }, { VK_S, ("し","シ") }, { VK_D, ("か","カ") }, { VK_F, ("て","テ") }, 
            { VK_Z, ("ぱ","パ") }, { VK_X, ("も","モ") }, { VK_C, ("る","ル") }, { VK_V, ("の","ノ") },
        };

        private static bool _isKatakana = false;
        private static bool _waitingVowel = false;
        private static int _pendingConsonant = 0;
        private static string _pendingChar = "";
        private static string _lastOutputChar = "";
        private static int _ynToggleCount = 0;

        public static int CurrentLayer { get; private set; } = 1;
        public static bool IsWaitingVowel => _waitingVowel;
        public static string PendingChar => _pendingChar;
        public static bool IsKatakana => _isKatakana;

        public static void Reset() 
        { 
            _waitingVowel = false; 
            _pendingConsonant = 0; 
            _pendingChar = ""; 
            _lastOutputChar = ""; 
            _ynToggleCount = 0; 
            
            OverlayHelper.ClearOverlay();   // 조합 취소/종료 시 잔상으로 남아있는 오버레이 강제 제거
        }
             
	    public static void SetLastOutputChar(string ch) => _lastOutputChar = ch;
	
	    public static void TogglePendingHiraKataModeOnly() => _isKatakana = !_isKatakana;
	    
        public static void ToggleLayer() 
        { 
            CurrentLayer = CurrentLayer == 1 ? 2 : 1; 
            MainForm.Instance?.ShowOverlay($"Layer{CurrentLayer}"); 
        }

        public static void TogglePendingHiraKata()
        {
            if (!_waitingVowel) return;
            _isKatakana = !_isKatakana;
            string preview = GetPreview(_pendingConsonant);
            for (int i = 0; i < _ynToggleCount; i++) if (JapaneseShared.TransformMap.TryGetValue(preview, out string? toggled)) preview = toggled;
            _pendingChar = preview; 
	            
            MainForm.Instance?.ShowOverlay(_pendingChar, 0);    // 타이머 없이 유지
        }
	
        public static void TogglePendingYn()
        {
            if (!_waitingVowel) return; _ynToggleCount++;
            if (JapaneseShared.TransformMap.TryGetValue(_pendingChar, out string? toggled)) _pendingChar = toggled;
            
            MainForm.Instance?.ShowOverlay(_pendingChar, 0);    // 타이머 없이 유지
        }
	
        // 공용 헬퍼를 사용하도록 로직 단순화
        public static void HandleHiraganaKatakanaTransformation()
        {
            TextSelectionUtils.TransformAndReplaceText(
                _lastOutputChar,
                JapaneseShared.ApplyHiraganaKatakanaTransformation,
                SetLastOutputChar,
                () => {
                    _isKatakana = !_isKatakana; 
                    _lastOutputChar = ""; 
                    MainForm.Instance?.ShowOverlay(_isKatakana ? "Katakana" : "Hiragana");
                }
            );
        }
	
        // 공용 헬퍼를 사용하도록 로직 단순화
        public static void HandleYoonTransformation()
	    {
            TextSelectionUtils.TransformAndReplaceText(
                _lastOutputChar,
                JapaneseShared.ApplyYoonTransformation,
                SetLastOutputChar
            );
        }
	
        public static string? ProcessKey(int vkCode, bool isShift)
        {
            // 가타카나 모드일 때 기호가 shift 누른 상태로 입력되도록 useKatakana 판별을 최상단으로 이동
            bool useKatakana = isShift ^ _isKatakana;

            // - 일본어1 특수기호 및 숫자열 포함 모든 기호에 대해 가타카나 모드 시 Shift 값 적용
            switch (vkCode)
            {
                case InputVk.OemYen: { string ch = useKatakana ? "|" : "¥"; MainForm.Instance?.ShowOverlay(ch); _lastOutputChar = ch; return ch; }
                case InputVk.OemColon: { string ch = useKatakana ? ":" : "・"; MainForm.Instance?.ShowOverlay(ch); _lastOutputChar = ch; return ch; }
                case InputVk.OemComma: { string ch = useKatakana ? "、" : ","; MainForm.Instance?.ShowOverlay(ch); _lastOutputChar = ch; return ch; }
                case InputVk.OemPeriod: { string ch = useKatakana ? "。" : "."; MainForm.Instance?.ShowOverlay(ch); _lastOutputChar = ch; return ch; }
                case InputVk.OemSlash: { string ch = useKatakana ? "ー" : "/"; MainForm.Instance?.ShowOverlay(ch); _lastOutputChar = ch; return ch; }
            }

            // [이번 수정] 하드코딩된 기호 맵핑을 제거하고 동적으로 Shift를 변환하는 코드로 교체
            if (KeyboardUtils.IsSymbolOrNumber(vkCode))
            {
                string? ch = KeyboardUtils.GetChar(vkCode, useKatakana);
                if (!string.IsNullOrEmpty(ch))
                {
                    MainForm.Instance?.ShowOverlay(ch);
                    _lastOutputChar = ch;
                    return ch;
                }
            }

            // YN 기동 키가 N으로 변경됨에 따라 분기 차단 조건 동기화
            if (vkCode == VK_B || vkCode == VK_N) return null;

            if (_waitingVowel)
            {
                if (_vowelKeys.Contains(vkCode))
                {
                    // CurrentLayer 정보 제거 및 튜플 단일화
                    var key = (_pendingConsonant, vkCode);
                    if (_combineMap.TryGetValue(key, out var combined))
                    {
                        string result = _isKatakana ? combined.Kata : combined.Hira;
                        for (int i = 0; i < _ynToggleCount; i++) if (JapaneseShared.TransformMap.TryGetValue(result, out string? toggled)) result = toggled;

                        string currentPending = _pendingChar;
                        string previewVow = vkCode switch { VK_H => _isKatakana ? "ア" : "あ", VK_J => _isKatakana ? "イ" : "い", VK_K => _isKatakana ? "ウ" : "う", VK_M => _isKatakana ? "エ" : "え", VK_L => _isKatakana ? "オ" : "お", _ => "?" };
                        
                        MainForm.Instance?.ShowOverlay($"{currentPending}+{previewVow}={result}");

                        // 타이머가 있는 ShowOverlay 호출 후, Reset()이 아닌 수동 변수 초기화를 통해 표시창이 정상적으로 유지되도록 함
                        _waitingVowel = false; _pendingConsonant = 0; _pendingChar = ""; _ynToggleCount = 0; _lastOutputChar = result; return result;
                    }
                }
            }
	
            // 조합 자음 입력 시작
            if (_consonantKeys.Contains(vkCode))
            {
                _waitingVowel = true; _pendingConsonant = vkCode; _isKatakana = useKatakana; _ynToggleCount = 0; _pendingChar = GetPreview(vkCode);
                
                MainForm.Instance?.ShowOverlay(_pendingChar, 0);    // 대기 중에는 타이머 없이 띄움
                return "";
            }
	
            if (_soloMap.TryGetValue(vkCode, out var solo))
            {
                string ch = useKatakana ? solo.Kata : solo.Hira;
                MainForm.Instance?.ShowOverlay(ch); 
                _lastOutputChar = ch; return ch;
            }
	
            _lastOutputChar = ""; return null;
        }
	
        private static string GetPreview(int vkCode)
        {
            var map = CurrentLayer == 1 ? _previewMapL1 : _previewMapL2;
            if (map.TryGetValue(vkCode, out var p)) return _isKatakana ? p.Kata : p.Hira;
            return "?";
        }
    }

    internal static class Japanese2Map
    {
        private static bool _isKatakana = false;
        private static string _lastOutputChar = "";
        public static int CurrentLayer { get; private set; } = 1;

        public static bool IsKatakana => _isKatakana;

        public static void SetLastOutputChar(string ch) => _lastOutputChar = ch;
        public static void ToggleLayer() 
        { 
            CurrentLayer = CurrentLayer == 3 ? 1 : CurrentLayer + 1; 
            MainForm.Instance?.ShowOverlay($"Layer{CurrentLayer}"); 
        }

        public static void TogglePendingHiraKataModeOnly() => _isKatakana = !_isKatakana;

        // 공용 헬퍼를 사용하도록 로직 단순화
        public static void HandleHiraganaKatakanaTransformation()
        {
            TextSelectionUtils.TransformAndReplaceText(
                _lastOutputChar,
                JapaneseShared.ApplyHiraganaKatakanaTransformation,
                SetLastOutputChar,
                () => {
                    _isKatakana = !_isKatakana; 
                    _lastOutputChar = ""; 
                    MainForm.Instance?.ShowOverlay(_isKatakana ? "Katakana" : "Hiragana");
                }
            );
        }

        // 공용 헬퍼를 사용하도록 로직 단순화
        public static void HandleYoonTransformation()
        {
            TextSelectionUtils.TransformAndReplaceText(
                _lastOutputChar,
                JapaneseShared.ApplyYoonTransformation,
                SetLastOutputChar
            );
        }

        // 일본어2(3Layer) 전체 자판 요구 명세 전면 교체
        public static string? ProcessKey(int vkCode, bool isShift)
        {
            // 가타카나 모드일 때 기호가 shift 누른 상태로 입력되도록 useKatakana를 상단으로 이동
            bool useKatakana = isShift ^ _isKatakana;

            // - 일본어2 특수기호 및 숫자열 포함 모든 기호 매핑에 가타카나 모드시 shift 값 적용
            switch (vkCode)
            {
                case InputVk.OemYen: { string ch_jpy = useKatakana ? "|" : "¥"; MainForm.Instance?.ShowOverlay(ch_jpy); _lastOutputChar = ch_jpy; return ch_jpy; }
                case InputVk.OemColon: { string ch_jpy = useKatakana ? ":" : "・"; MainForm.Instance?.ShowOverlay(ch_jpy); _lastOutputChar = ch_jpy; return ch_jpy; }
                case InputVk.OemComma: { string ch_jpy = useKatakana ? "、" : ","; MainForm.Instance?.ShowOverlay(ch_jpy); _lastOutputChar = ch_jpy; return ch_jpy; }
                case InputVk.OemPeriod: { string ch_jpy = useKatakana ? "。" : "."; MainForm.Instance?.ShowOverlay(ch_jpy); _lastOutputChar = ch_jpy; return ch_jpy; }
                case InputVk.OemSlash: { string ch_jpy = useKatakana ? "ー" : "/"; MainForm.Instance?.ShowOverlay(ch_jpy); _lastOutputChar = ch_jpy; return ch_jpy; }
          }

            // [이번 수정] 하드코딩된 기호 맵핑을 제거하고 동적 반환 유틸리티 적용
            if (KeyboardUtils.IsSymbolOrNumber(vkCode))
            {
                string? ch_jpy = KeyboardUtils.GetChar(vkCode, useKatakana);
                if (!string.IsNullOrEmpty(ch_jpy))
                {
                    MainForm.Instance?.ShowOverlay(ch_jpy);
                    _lastOutputChar = ch_jpy;
                    return ch_jpy;
                }
            }

            string? ch = null;

            if (CurrentLayer == 1)
            {
                // Layer1 키배열 전체 재할당 (요청된 1-1, 1-2, 1-3 레이아웃)
                ch = vkCode switch
                {
                    // Layer 1-1 (q~p)
                    0x51 => useKatakana ? "レ" : "れ", 0x57 => useKatakana ? "ロ" : "ろ", 0x45 => useKatakana ? "ル" : "る", 0x52 => useKatakana ? "リ" : "り", 0x54 => useKatakana ? "ラ" : "ら", 
                    0x59 => useKatakana ? "ハ" : "は", 0x55 => useKatakana ? "ヒ" : "ひ", 0x49 => useKatakana ? "フ" : "ふ", 0x4F => useKatakana ? "ホ" : "ほ", 0x50 => useKatakana ? "ヘ" : "へ", 
                    // Layer 1-2 (a~l)
                    0x41 => useKatakana ? "ネ" : "ね", 0x53 => useKatakana ? "ノ" : "の", 0x44 => useKatakana ? "ヌ" : "ぬ", 0x46 => useKatakana ? "ニ" : "に", 0x47 => useKatakana ? "ナ" : "な",
                    0x48 => useKatakana ? "ア" : "あ", 0x4A => useKatakana ? "イ" : "い", 0x4B => useKatakana ? "ウ" : "う", 0x4C => useKatakana ? "オ" : "お", 
                    // Layer 1-3 (z~m)
                    0x5A => useKatakana ? "メ" : "め", 0x58 => useKatakana ? "モ" : "も", 0x43 => useKatakana ? "ム" : "む", 0x56 => useKatakana ? "ミ" : "み", 0x42 => useKatakana ? "マ" : "ま",
                    0x4E => useKatakana ? "ン" : "ん", 0x4D => useKatakana ? "エ" : "え", 
                    _ => null
                };
            }
            else if (CurrentLayer == 2)
            {
                // Layer2 키배열 전체 재할당 (요청된 2-1, 2-2, 2-3 레이아웃)
                ch = vkCode switch
                {
                    // Layer 2-1 (q~p)
                    0x51 => useKatakana ? "ケ" : "け", 0x57 => useKatakana ? "コ" : "こ", 0x45 => useKatakana ? "ク" : "く", 0x52 => useKatakana ? "キ" : "き", 0x54 => useKatakana ? "カ" : "か",
                    0x59 => useKatakana ? "パ" : "ぱ", 0x55 => useKatakana ? "ピ" : "ぴ", 0x49 => useKatakana ? "プ" : "ぷ", 0x4F => useKatakana ? "ポ" : "ぽ", 0x50 => useKatakana ? "ペ" : "ぺ", 
                    // Layer 2-2 (a~l)
                    0x41 => useKatakana ? "テ" : "て", 0x53 => useKatakana ? "ト" : "と", 0x44 => useKatakana ? "ツ" : "つ", 0x46 => useKatakana ? "チ" : "ち", 0x47 => useKatakana ? "タ" : "た",
                    0x48 => useKatakana ? "ッ" : "っ", 0x4A => useKatakana ? "ヨ" : "よ", 0x4B => useKatakana ? "ユ" : "ゆ", 0x4C => useKatakana ? "ヤ" : "や", 
                    // Layer 2-3 (z~m)
                    0x5A => useKatakana ? "セ" : "せ", 0x58 => useKatakana ? "ソ" : "そ", 0x43 => useKatakana ? "ス" : "す", 0x56 => useKatakana ? "シ" : "し", 0x42 => useKatakana ? "サ" : "さ",
                    0x4E => useKatakana ? "ヲ" : "を", 0x4D => useKatakana ? "ワ" : "わ", 
                    _ => null
                };
            }
            else if (CurrentLayer == 3)
            {
                // Layer3 키배열 전체 재할당 (요청된 3-1, 3-2, 3-3 레이아웃)
                ch = vkCode switch
                {
                    // Layer 3-1 (q~p)
                    0x51 => useKatakana ? "ゲ" : "げ", 0x57 => useKatakana ? "ゴ" : "ご", 0x45 => useKatakana ? "グ" : "ぐ", 0x52 => useKatakana ? "ギ" : "ぎ", 0x54 => useKatakana ? "ガ" : "が", 
                    0x59 => useKatakana ? "バ" : "ば", 0x55 => useKatakana ? "ビ" : "び", 0x49 => useKatakana ? "ブ" : "ぶ", 0x4F => useKatakana ? "ボ" : "ぼ", 0x50 => useKatakana ? "ベ" : "べ", 
                    // Layer 3-2 (a~l)
                    0x41 => useKatakana ? "デ" : "で", 0x53 => useKatakana ? "ド" : "ど", 0x44 => useKatakana ? "ヅ" : "づ", 0x46 => useKatakana ? "ヂ" : "ぢ", 0x47 => useKatakana ? "ダ" : "だ",
                    0x48 => useKatakana ? "ヴ" : "ゔ", 0x4A => useKatakana ? "ョ" : "ょ", 0x4B => useKatakana ? "ュ" : "ゅ", 0x4C => useKatakana ? "ャ" : "ゃ", 
                    // Layer 3-3 (z~b) - n과 m은 단축키 처리기에서 별도 가로챔
                    0x5A => useKatakana ? "ゼ" : "ぜ", 0x58 => useKatakana ? "ゾ" : "ぞ", 0x43 => useKatakana ? "ズ" : "ず", 0x56 => useKatakana ? "ジ" : "じ", 0x42 => useKatakana ? "ザ" : "ざ", 
                    _ => null
                };
            }

            if (ch != null) 
            { 
                MainForm.Instance?.ShowOverlay(ch); 
                _lastOutputChar = ch; return ch; 
            }
            return null;
        }
    }

    internal class Japanese2Processor : IKeyProcessor
    {
        public bool IsVirtualShift => Japanese2Map.IsKatakana;
        public int CurrentLayer => Japanese2Map.CurrentLayer;

        public void ToggleVirtualShift() => Japanese2Map.TogglePendingHiraKataModeOnly();

        public bool ProcessHanjaKey(IntPtr hFore, bool capsOn, bool isHangulMode)
        {
            if (isHangulMode && capsOn) { Japanese2Map.ToggleLayer(); return true; }
            return false;
        }

        public bool ProcessKeyDown(int vkCode, bool isShift, bool capsOn, IntPtr hFore, bool isHangulMode)
        {
            // [이번 수정] Key2 상태일 때 Shift 강제 적용
            if (AppConfig.IsOverlayKey2Mode) isShift = true;

            if (vkCode is >= 0x21 and <= 0x28) { if (!isShift) Japanese2Map.SetLastOutputChar(""); return false; }

            if (Japanese2Map.CurrentLayer == 3)
            {
                if (vkCode == InputVk.vk_N) { if (!capsOn || !isHangulMode) return false; Japanese2Map.HandleHiraganaKatakanaTransformation(); return true; }
                if (vkCode == InputVk.vk_M) { if (!capsOn || !isHangulMode) return false; Japanese2Map.HandleYoonTransformation(); return true; }
            }

            if (!capsOn || !isHangulMode) return false;
            if (TextSelectionUtils.IsConverting) return true;

            string? keyResult = Japanese2Map.ProcessKey(vkCode, isShift);
            if (keyResult == null) { Japanese2Map.SetLastOutputChar(""); return false; }
            
            if (keyResult.Length > 0)
            {
                GlobalInputHook.IsSending = true; 
                NativeMethods.SendUnicodeString(keyResult); 
                GlobalInputHook.IsSending = false; 
            }
            return true;
        }

        public void OnMouseClick() => Japanese2Map.SetLastOutputChar("");
    }
    #endregion
}