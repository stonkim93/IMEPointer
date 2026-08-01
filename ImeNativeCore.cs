// ImeNativeCore.cs
#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace IMEPointer
{
    // =======================================================================================
    // [수정: 클래스 역할 및 캐시 관리 로직 최적화]
    // 5. 감지 및 입력 훅 모듈 (ImeState)
    // =======================================================================================
    /// <summary>
    /// 대상 창의 현재 입력 상태(IME 모드)를 감지하고 상태를 변경하는 모듈입니다.
    /// </summary>
    internal static class ImeState
    {
        public enum State
        {
            EnglishLower, EnglishUpper, Hangul, PaliUS, PaliHangul, JapaneseIME, JapaneseHangul1A, JapaneseHangul1B, JapaneseHangul2, Engineer
        }

        // [수정: 캐시 메모리 누수 방지] 핸들(IntPtr) 누적을 방지하기 위한 최대 캐시 크기 설정
        private const int MaxCacheSize = 100;
        private static readonly Dictionary<IntPtr, bool> _hangulStateCache = new Dictionary<IntPtr, bool>();

        /// <summary>
        /// 주어진 상태가 한글 입력 기반인지 확인합니다.
        /// </summary>
        public static bool IsHangul(State state) =>
            state == State.Hangul || state == State.PaliHangul || state == State.JapaneseHangul1A || state == State.JapaneseHangul1B || state == State.JapaneseHangul2 || state == State.Engineer;

        /// <summary>
        /// 현재 포커스된 창의 키보드 레이아웃과 IME 상태를 종합하여 현재 입력 상태를 판별합니다.
        /// </summary>
        public static State Detect(IntPtr foregroundHwnd,
            bool enablePali = false, bool enableJapanese1 = false, bool enableJapanese2 = false, bool enableJapanese3 = false, bool enableEngineer = false)
        {
            bool capsOn = (NativeMethods.GetKeyState(NativeMethods.VK_CAPITAL) & 0x0001) != 0;
            if (foregroundHwnd == IntPtr.Zero) return capsOn ? State.EnglishUpper : State.EnglishLower;

            uint threadId = NativeMethods.GetWindowThreadProcessId(foregroundHwnd, out _);
            long hklValue = NativeMethods.GetKeyboardLayout(threadId).ToInt64();
            ushort langId = (ushort)(hklValue & 0xFFFF);

            if (langId == 0x0409) return State.PaliUS;
            if (langId == 0x0411) return State.JapaneseIME;

            if (langId == 0x0412) // 한국어 레이아웃
            {
                bool isHangul = IsHangulModeSystemWide(foregroundHwnd);
                if (isHangul)
                {
                    if (capsOn)
                    {
                        if (enablePali) return State.PaliHangul;
                        if (enableEngineer) return State.Engineer;
                        if (enableJapanese1) return State.JapaneseHangul1A;
                        if (enableJapanese2) return State.JapaneseHangul1B;                        
                        if (enableJapanese3) return State.JapaneseHangul2;
                    }
                    return State.Hangul;
                }
                return capsOn ? State.EnglishUpper : State.EnglishLower;
            }

            return capsOn ? State.EnglishUpper : State.EnglishLower;
        }

        private static IntPtr GetTargetImeWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return IntPtr.Zero;
            uint threadId = NativeMethods.GetWindowThreadProcessId(hWnd, out _);
            IntPtr focusWnd = hWnd;

            NativeMethods.GUITHREADINFO gti = new() { cbSize = Marshal.SizeOf<NativeMethods.GUITHREADINFO>() };
            if (NativeMethods.GetGUIThreadInfo(threadId, ref gti))
            {
                if (gti.hwndFocus != IntPtr.Zero) focusWnd = gti.hwndFocus;
                else if (gti.hwndActive != IntPtr.Zero) focusWnd = gti.hwndActive;
            }

            IntPtr hIme = NativeMethods.ImmGetDefaultIMEWnd(focusWnd);
            return hIme != IntPtr.Zero ? hIme : NativeMethods.ImmGetDefaultIMEWnd(hWnd);
        }

        /// <summary>
        /// 시스템 전역적으로 현재 창이 한글 입력 모드인지 확인합니다.
        /// </summary>
        public static bool IsHangulModeSystemWide(IntPtr foregroundHwnd)
        {
            return CheckHangulPublic(foregroundHwnd);
        }

        public static bool CheckHangulPublic(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return false;

            if (_hangulStateCache.Count > MaxCacheSize)
            {
                _hangulStateCache.Clear();
            }

            IntPtr hImeWnd = GetTargetImeWindow(hWnd);
            if (hImeWnd != IntPtr.Zero)
            {
                IntPtr res = NativeMethods.SendMessageTimeout(hImeWnd, NativeMethods.WM_IME_CONTROL, (IntPtr)NativeMethods.IMC_GETCONVERSIONMODE, IntPtr.Zero, NativeMethods.SMTO_ABORTIFHUNG, 30, out IntPtr result);
                if (res != IntPtr.Zero)
                {
                    bool isHangul = ((uint)result.ToInt64() & NativeMethods.IME_CMODE_NATIVE) != 0;
                    _hangulStateCache[hWnd] = isHangul;
                    return isHangul;
                }
            }

            IntPtr hIMC = NativeMethods.ImmGetContext(hWnd);
            if (hIMC != IntPtr.Zero)
            {
                bool success = NativeMethods.ImmGetConversionStatus(hIMC, out uint conv, out _);
                NativeMethods.ImmReleaseContext(hWnd, hIMC);
                if (success)
                {
                    bool isHangul = (conv & NativeMethods.IME_CMODE_NATIVE) != 0;
                    _hangulStateCache[hWnd] = isHangul;
                    return isHangul;
                }
            }
            
            return _hangulStateCache.TryGetValue(hWnd, out bool cachedState) ? cachedState : false;
        }

        /// <summary>
        /// 대상 윈도우의 IME 한글/영문 상태를 강제로 설정합니다.
        /// </summary>
        public static void SetHangulState(IntPtr hWnd, bool setHangul)
        {
            IntPtr hImeWnd = GetTargetImeWindow(hWnd);
            if (hImeWnd != IntPtr.Zero)
            {
                NativeMethods.SendMessageTimeout(hImeWnd, NativeMethods.WM_IME_CONTROL, (IntPtr)NativeMethods.IMC_GETCONVERSIONMODE, IntPtr.Zero, NativeMethods.SMTO_ABORTIFHUNG, 20, out IntPtr result);
                uint mode = (uint)result.ToInt64();
                bool isHangul = (mode & NativeMethods.IME_CMODE_NATIVE) != 0;

                if (isHangul != setHangul)
                {
                    if (setHangul) mode |= NativeMethods.IME_CMODE_NATIVE;
                    else mode &= ~NativeMethods.IME_CMODE_NATIVE;
                    NativeMethods.SendMessageTimeout(hImeWnd, NativeMethods.WM_IME_CONTROL, (IntPtr)NativeMethods.IMC_SETCONVERSIONMODE, (IntPtr)mode, NativeMethods.SMTO_ABORTIFHUNG, 20, out _);
                    
                    _hangulStateCache[hWnd] = setHangul;
                }
            }
        }
    }

    // =======================================================================================
    // [수정: 변수명 및 흐름 명확화]
    // 전역 시스템 훅 모듈 통합 (GlobalInputHook)
    // =======================================================================================
    /// <summary>
    /// 키보드 및 마우스 입력을 시스템 전역에서 가로채고 처리합니다.
    /// </summary>
    internal static class GlobalInputHook
    {
        // [수정: keybd_event가 NativeMethods.cs로 이동됨]

        /// <summary>
        /// 훅 이벤트 발생 시점의 애플리케이션 상태 스냅샷입니다.
        /// </summary>
        internal readonly struct HookContextSnapshot
        {
            public readonly IntPtr ContextHwnd;
            public readonly ushort ContextLangId;
            public readonly bool IsHangulMode;
            public readonly IKeyProcessor? ActiveProcessor;
            public readonly bool IsPaliModeActive;
            public readonly bool IsEngineerModeActive;
            public readonly bool IsJapanese1ModeActive;
            public readonly bool IsJapanese2ModeActive;            
            public readonly bool IsJapanese3ModeActive;

            public HookContextSnapshot(
                IntPtr contextHwnd,
                ushort contextLangId,
                bool isHangulMode,
                IKeyProcessor? activeProcessor,
                bool isPaliModeActive,
                bool isEngineerModeActive,
                bool isJapanese1ModeActive,
                bool isJapanese2ModeActive,                
                bool isJapanese3ModeActive)
            {
                ContextHwnd = contextHwnd;
                ContextLangId = contextLangId;
                IsHangulMode = isHangulMode;
                ActiveProcessor = activeProcessor;
                IsPaliModeActive = isPaliModeActive;
                IsEngineerModeActive = isEngineerModeActive;
                IsJapanese1ModeActive = isJapanese1ModeActive;
                IsJapanese2ModeActive = isJapanese2ModeActive;                
                IsJapanese3ModeActive = isJapanese3ModeActive;
            }
        }

        public static bool IsEnabled { get; set; } = true;

        private static HookContextSnapshot _contextSnapshot = new(
            IntPtr.Zero, 0, false, null, false, false, false, false, false);

        public static bool IsPaliModeActive => _contextSnapshot.IsPaliModeActive;
        public static bool IsEngineerModeActive => _contextSnapshot.IsEngineerModeActive;
        public static bool IsJapanese1ModeActive => _contextSnapshot.IsJapanese1ModeActive;
        public static bool IsJapanese2ModeActive => _contextSnapshot.IsJapanese2ModeActive;
        public static bool IsJapanese3ModeActive => _contextSnapshot.IsJapanese3ModeActive;
        public static IKeyProcessor? ActiveProcessor => _contextSnapshot.ActiveProcessor;
        public static IntPtr ContextHwnd => _contextSnapshot.ContextHwnd;
        public static ushort ContextLangId => _contextSnapshot.ContextLangId;
        public static bool CachedIsHangulMode => _contextSnapshot.IsHangulMode;

        public static volatile bool IsSending = false;
        private static IntPtr _kbdHookId = IntPtr.Zero;
        private static IntPtr _mouseHookId = IntPtr.Zero;
        private static IntPtr _lastResolvedContextHwnd = IntPtr.Zero;

        public static unsafe void Install()
        {
            if (_kbdHookId != IntPtr.Zero && _mouseHookId != IntPtr.Zero) return;
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            var module = process.MainModule ?? throw new InvalidOperationException("MainModule을 가져올 수 없습니다.");
            IntPtr hMod = NativeMethods.GetModuleHandle(module.ModuleName);

            if (_kbdHookId == IntPtr.Zero)
            {
                delegate* unmanaged[Stdcall]<int, IntPtr, IntPtr, IntPtr> kbdCb = &KbdHookCallback;
                _kbdHookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, kbdCb, hMod, 0);
            }
            if (_mouseHookId == IntPtr.Zero)
            {
                delegate* unmanaged[Stdcall]<int, IntPtr, IntPtr, IntPtr> mouseCb = &MouseHookCallback;
                _mouseHookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, mouseCb, hMod, 0);
            }
        }

        public static void Uninstall()
        {
            if (_kbdHookId != IntPtr.Zero) { NativeMethods.UnhookWindowsHookEx(_kbdHookId); _kbdHookId = IntPtr.Zero; }
            if (_mouseHookId != IntPtr.Zero) { NativeMethods.UnhookWindowsHookEx(_mouseHookId); _mouseHookId = IntPtr.Zero; }
        }

        public static void UpdateContext(HookContextSnapshot snapshot)
        {
            _contextSnapshot = snapshot;
        }

        /// <summary>
        /// 지정된 횟수만큼 백스페이스를 전송한 후 새로운 텍스트를 입력합니다.
        /// </summary>
        public static void SendReplacement(int backCount, string text)
        {
            IsSending = true;

            if (AppConfig.EnableCopilotMap)
            {
                Thread.Sleep(50); 
                
                bool isShift = (NativeMethods.GetKeyState(0x10) & 0x8000) != 0;
                bool isLWin = (NativeMethods.GetKeyState(0x5B) & 0x8000) != 0;
                bool isRWin = (NativeMethods.GetKeyState(0x5C) & 0x8000) != 0;
                
                // [수정: 분리된 NativeMethods 사용]
                if (isShift) NativeMethods.keybd_event(0x10, 0, 0x0002, UIntPtr.Zero); // KEYEVENTF_KEYUP
                if (isLWin) NativeMethods.keybd_event(0x5B, 0, 0x0002, UIntPtr.Zero);
                if (isRWin) NativeMethods.keybd_event(0x5C, 0, 0x0002, UIntPtr.Zero);
            }

            for (int i = 0; i < backCount; i++) NativeMethods.SendBackspace();
            if (!string.IsNullOrEmpty(text)) NativeMethods.SendUnicodeString(text);
            IsSending = false;
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
        private static IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && wParam.ToInt32() == NativeMethods.WM_LBUTTONDOWN)
                {
                    ActiveProcessor?.OnMouseClick();
                }
            }
            catch { }
            return NativeMethods.CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
        }

        private static bool IsInterestedKeyboardMessage(int msg) =>
            msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN;

        private static bool IsHanjaOrRightCtrl(int vkCode) => vkCode == 0x19 || vkCode == 0xA3; // VK_HANJA, VK_RCONTROL

        private static bool HasBlockedModifierChord(bool allowCtrlForCurrentKey)
        {
            bool isCtrl = (NativeMethods.GetKeyState(0x11) & 0x8000) != 0;
            if (isCtrl && !allowCtrlForCurrentKey) return true;
            if ((NativeMethods.GetKeyState(0x12) & 0x8000) != 0) return true; // Alt

            bool isWin = (NativeMethods.GetKeyState(0x5B) & 0x8000) != 0
                || (NativeMethods.GetKeyState(0x5C) & 0x8000) != 0;

            if (AppConfig.EnableCopilotMap && isWin) 
            {
                isWin = false; 
            }

            return isWin;
        }

        private static IntPtr ResolveContextHwnd()
        {
            IntPtr hwnd = ContextHwnd;
            if (hwnd != IntPtr.Zero)
            {
                _lastResolvedContextHwnd = hwnd;
                return hwnd;
            }

            if (_lastResolvedContextHwnd != IntPtr.Zero)
                return _lastResolvedContextHwnd;

            hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd != IntPtr.Zero)
                _lastResolvedContextHwnd = hwnd;

            return hwnd;
        }

        private static IntPtr BypassKeyboardHook(int nCode, IntPtr wParam, IntPtr lParam) =>
            NativeMethods.CallNextHookEx(_kbdHookId, nCode, wParam, lParam);

        private static bool ShouldBypassHook(int nCode, IntPtr wParam)
        {
            if (nCode < 0 || IsSending || !IsEnabled)
                return true;

            int msg = wParam.ToInt32();
            return !IsInterestedKeyboardMessage(msg);
        }

        private static bool TryResolveKeyboardContext(int vkCode, out IntPtr hFore, out bool capsOn, out bool isHangulMode, out bool isHanjaOrRCtrl)
        {
            isHanjaOrRCtrl = IsHanjaOrRightCtrl(vkCode);

            if (HasBlockedModifierChord(isHanjaOrRCtrl))
            {
                hFore = IntPtr.Zero;
                capsOn = false;
                isHangulMode = false;
                return false;
            }

            hFore = ResolveContextHwnd();
            if (hFore == IntPtr.Zero)
            {
                capsOn = false;
                isHangulMode = false;
                return false;
            }

            capsOn = (NativeMethods.GetKeyState(NativeMethods.VK_CAPITAL) & 0x0001) != 0;
            isHangulMode = CachedIsHangulMode;
            return true;
        }

        private static IntPtr HandleHanjaKey(int nCode, IntPtr wParam, IntPtr lParam, IntPtr hFore, bool capsOn, bool isHangulMode)
        {
            if (isHangulMode & !capsOn) 
            { 
                return BypassKeyboardHook(nCode, wParam, lParam); 
            }

            if (!isHangulMode)
            {
                ImeState.SetHangulState(hFore, true);
                if (!capsOn) NativeMethods.SimulateCapsLock();
                MainForm.Instance?.ShowOverlay(UiText.HangulCapsMode);
                return (IntPtr)1; // 입력 가로챔
            }

            IKeyProcessor? hanjaProcessor = ActiveProcessor;
            if (hanjaProcessor != null && hanjaProcessor.ProcessHanjaKey(hFore, capsOn, isHangulMode))
            {
                MainForm.Instance?.RequestLayoutRefresh();
                return (IntPtr)1; // 입력 가로챔
            }

            return BypassKeyboardHook(nCode, wParam, lParam);
        }

        private static IntPtr HandleLanguageProcessorKey(int nCode, IntPtr wParam, IntPtr lParam, int vkCode, IntPtr hFore, bool capsOn, bool isHangulMode)
        {
            IKeyProcessor? keyProcessor = ActiveProcessor;
            if (keyProcessor == null || ContextLangId != 0x0412) // 0x0412: 한국어
                return BypassKeyboardHook(nCode, wParam, lParam);

            bool isShift = (NativeMethods.GetKeyState(0x10) & 0x8000) != 0;
            if (keyProcessor.ProcessKeyDown(vkCode, isShift, capsOn, hFore, isHangulMode))
                return (IntPtr)1; // 입력 가로챔

            return BypassKeyboardHook(nCode, wParam, lParam);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
        private static IntPtr KbdHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (ShouldBypassHook(nCode, wParam))
                return BypassKeyboardHook(nCode, wParam, lParam);

            try
            {
                int vkCode = Marshal.ReadInt32(lParam);

                if (!TryResolveKeyboardContext(vkCode, out IntPtr hFore, out bool capsOn, out bool isHangulMode, out bool isHanjaOrRCtrl))
                    return BypassKeyboardHook(nCode, wParam, lParam);

                if (isHanjaOrRCtrl)
                    return HandleHanjaKey(nCode, wParam, lParam, hFore, capsOn, isHangulMode);

                return HandleLanguageProcessorKey(nCode, wParam, lParam, vkCode, hFore, capsOn, isHangulMode);
            }
            catch { }

            return BypassKeyboardHook(nCode, wParam, lParam);
        }
    }

    // =======================================================================================
    // [수정: 기존에 있던 NativeMethods 클래스는 NativeMethods.cs 파일로 분리 및 이동되었습니다.]
    // =======================================================================================
}