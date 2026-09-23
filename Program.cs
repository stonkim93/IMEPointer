// Program.cs - IMEPointer
#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
//using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Runtime.CompilerServices.DisableRuntimeMarshalling]
[assembly: System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]

namespace IMEPointer
{
    public enum OverlayPositionMode
    {
        CharInput,
        CharToggle,
        SelectionToggle,
        ModeSwitch
    }

    #region [ 진입점 (Main) ]
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            using Mutex mutexPointer = new Mutex(true, @"Global\IMEPointer_SingleInstance", out bool isPointerFirst);
            using Mutex mutexPali = new Mutex(true, @"Global\IMEPali_SingleInstance", out bool isPaliFirst);
            using Mutex mutexJapanese = new Mutex(true, @"Global\IMEJapanese_SingleInstance", out bool isJapaneseFirst);

            if (!isPointerFirst || !isPaliFirst || !isJapaneseFirst)
            {
                MessageBox.Show("IMEPointer 앱이 이미 실행 중입니다.", "IMEPointer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            // [최적화 1] 불필요하게 중첩된 try-catch 블록 제거 및 정리
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IMEPointer.log");
                var listener = new TextWriterTraceListener(logPath);
                Trace.Listeners.Add(listener);
                Trace.AutoFlush = true;
            }
            catch { }

            try
            {
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                try
                {
                    Task.Run(() =>
                    {
                        MozcDictionary.LoadDictionary();
                        MozcDictionary.PrintStatistics();
                    });
                }
                catch (Exception ex)
                {
                    if (AppConfig.LogLevel >= 1) System.Diagnostics.Debug.WriteLine($"Failed to start MozcDictionary loader task: {ex.Message}");
                }

                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                if (AppConfig.LogLevel >= 1) Trace.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Fatal Error: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"{UiText.FatalErrorPrefix}{ex.Message}", UiText.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
    #endregion




    #region [ 메인 폼 (MainForm) 및 트레이 제어 ]
    internal class MainForm : Form
    {
        public static MainForm? Instance { get; private set; }
        public static IntPtr LastValidHwnd { get; private set; } = IntPtr.Zero;
        public static IntPtr LastValidFocusHwnd { get; private set; } = IntPtr.Zero;

        private const int HiddenFormSize = 16;
        private const int HiddenFormLocation = -100;
        private const int HiddenLayeredWindowLocation = -10000;  // 인디케이터 레이어 창을 화면 밖으로 숨길 때 사용
        private const int WindowPosChangedMessage = 0x001A;
        private const int TrayContextMenuForegroundDelayRetryMs = 60;
        private const int RebuildRetryAfterWindowPosChangedMs = 800;
        private const int RebuildRetryAfterScaleChangeMs = 1500;
        private const int DisplaySettingsChangedDelayMs = 400;
        private const int UserPreferenceChangedDelayMs = 600;
        private const float PointerDiagonalFactor = 0.7071f;
        private const float IBeamIndicatorYOffsetFactor = 0.65f;
        private const float IndicatorBottomMargin = 4f;
        private static readonly RectangleF TrayIconTextRectLower = new RectangleF(-2.0f, -5.0f, 36f, 36f);
        private static readonly RectangleF TrayIconTextRectUpper = new RectangleF(-2.0f, -3.5f, 36f, 36f);

        private readonly Dictionary<ImeState.State, StateAssets> _assetCache = new();
        private IntPtr _hWinEventHookForeground;
        private IntPtr _hWinEventHookFocus;
        private NativeMethods.WinEventDelegate _winEventProc = null!;
        private readonly NotifyIcon _sysTrayIcon;
        private readonly ContextMenuStrip _trayContextMenu;
        private readonly ToolStripMenuItem _menuItemStatus;
        private bool _isTextOverlayEnabled = AppConfig.DefaultShowTextOverlay;

        internal enum PointerMode { WinDefault = 0, WinColor = 1, NewColor = 2 }
        internal enum CapsMode { WinDefault = 0, Engineer = 1, Pali = 2, Japanese1 = 3, Japanese2 = 4, Japanese3 = 5 }

        private PointerMode _activePointerMode = (PointerMode)AppConfig.DefaultPointerMode;
        private CapsMode _activeCapsMode = (CapsMode)AppConfig.DefaultCapsMode;
        private bool _isMiniIndicatorEnabled = AppConfig.DefaultEnableMiniIndicator;
        private bool _isKeyboardLayoutOverlayEnabled = AppConfig.DefaultShowKeyboardLayout;

        private ToolStripMenuItem _menuItemPointerWinDefault = null!;
        private ToolStripMenuItem _menuItemPointerWinColor = null!;
        private ToolStripMenuItem _menuItemPointerNewColor = null!;
        private ToolStripMenuItem _menuItemCapsWinDefault = null!;
        private ToolStripMenuItem _menuItemCapsEngineer = null!;
        private ToolStripMenuItem _menuItemCapsPali = null!;
        private ToolStripMenuItem _menuItemCapsJapanese1 = null!;
        private ToolStripMenuItem _menuItemCapsJapanese2 = null!;
        private ToolStripMenuItem _menuItemCapsJapanese3 = null!;
        private ToolStripMenuItem _menuItemToggleIndicator = null!;

        private ToolStripMenuItem _menuItemUseMozc = null!;
        private ToolStripMenuItem _menuItemUseGoogleApi = null!;

        private ToolStripMenuItem _menuItemToggleKeyboardLayout = null!;
        private ToolStripMenuItem _menuItemToggleTextOverlay = null!;
        private ToolStripMenuItem _menuItemToggleCopilotMap = null!;

        private bool _isShiftVisualInverted = false;
        private bool _lastHangulSyncState = false;
        private bool _isCurrentProcessTarget = false;   // 현재 포그라운드 창이 타겟 앱인지 여부
        private KeyboardLayoutForm? _frmKeyboardLayout;
        private TextOverlayForm? _frmTextOverlay;
        private Point _lastKeyboardLayoutLocation = Point.Empty;

        private ImeState.State _previousImeState = (ImeState.State)(-1);
        private Color _currentIndicatorColor = Color.White;
        private Color _lastRenderedIndicatorColor = Color.Empty;
        private IntPtr _lastForegroundHwnd = IntPtr.Zero;
        private IntPtr _currentContextHwnd = IntPtr.Zero;
        private IntPtr _lastPolledHwnd = IntPtr.Zero;

        // 그래픽 자원
        private IntPtr _dcIndicatorScreen = IntPtr.Zero;
        private IntPtr _dcIndicatorMem = IntPtr.Zero;
        private IntPtr _hBmpIndicator = IntPtr.Zero;
        private IntPtr _hBmpIndicatorOld = IntPtr.Zero;
        private bool _isIndicatorRendered = false;
        private bool _isPointerInIBeamCell = false;
        private int _lastIndicatorX = int.MinValue;
        private int _lastIndicatorY = int.MinValue;

        private float _currentDpiScale = 1.0f;
        private float _physIndicatorOffsetX = 0f;
        private int _indicatorCanvasSize = 16;
        private int _pointerPhysicalSize = 32;

        private IntPtr _lastAppliedArrowHandle = IntPtr.Zero;
        private static readonly unsafe int s_bmiSize = sizeof(NativeMethods.BITMAPINFO);
        private static readonly uint s_currentProcessId = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;

        private readonly struct ActiveInputModeContext
        {
            public readonly bool IsPaliModeActive;
            public readonly bool IsEngineerModeActive;
            public readonly bool IsJapanese1ModeActive;
            public readonly bool IsJapanese2ModeActive;
            public readonly bool IsJapanese3ModeActive;
            public readonly IKeyProcessor? ActiveProcessor;

            public ActiveInputModeContext(bool pali, bool eng, bool j1, bool j2, bool j3, IKeyProcessor? proc)
            {
                IsPaliModeActive = pali; IsEngineerModeActive = eng;
                IsJapanese1ModeActive = j1; IsJapanese2ModeActive = j2; IsJapanese3ModeActive = j3; ActiveProcessor = proc;
            }
        }

        private readonly struct CapsModeStateMapping
        {
            public readonly CapsMode Mode;
            public readonly ImeState.State ActiveState;
            public readonly IKeyProcessor Processor;

            public CapsModeStateMapping(CapsMode m, ImeState.State s, IKeyProcessor p)
            {
                Mode = m; ActiveState = s; Processor = p;
            }
        }

        private class StateAssets : IDisposable
        {
            public IntPtr ArrowNewPtr = IntPtr.Zero;
            public IntPtr IBeamNewPtr = IntPtr.Zero;
            public IntPtr ArrowWinPtr = IntPtr.Zero;
            public IntPtr IBeamWinPtr = IntPtr.Zero;
            public IntPtr IBeamCompareHandleNew = IntPtr.Zero;
            public IntPtr IBeamCompareHandleWin = IntPtr.Zero;
            public Icon? TrayIcon;
            public Color DotColor;
            public string Description = "";

            public void Dispose()
            {
                if (ArrowNewPtr != IntPtr.Zero) NativeMethods.DestroyCursor(ArrowNewPtr);
                if (IBeamNewPtr != IntPtr.Zero) NativeMethods.DestroyCursor(IBeamNewPtr);
                if (ArrowWinPtr != IntPtr.Zero) NativeMethods.DestroyCursor(ArrowWinPtr);
                if (IBeamWinPtr != IntPtr.Zero) NativeMethods.DestroyCursor(IBeamWinPtr);
                if (IBeamCompareHandleNew != IntPtr.Zero) NativeMethods.DestroyCursor(IBeamCompareHandleNew);
                if (IBeamCompareHandleWin != IntPtr.Zero) NativeMethods.DestroyCursor(IBeamCompareHandleWin);
                TrayIcon?.Dispose();
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOPMOST;
                return cp;
            }
        }

        private static readonly CapsModeStateMapping[] _capsModeMaps = {
            new(CapsMode.Engineer, ImeState.State.Engineer, KeyProcessorFactory.Engineer),
            new(CapsMode.Pali, ImeState.State.PaliHangul, KeyProcessorFactory.Pali),
            new(CapsMode.Japanese1, ImeState.State.JapaneseHangul1, KeyProcessorFactory.Japanese1),
            new(CapsMode.Japanese2, ImeState.State.JapaneseHangul2, KeyProcessorFactory.Japanese2),
            new(CapsMode.Japanese3, ImeState.State.JapaneseHangul3, KeyProcessorFactory.Japanese3)
        };
        
        public MainForm()
        {
            Instance = this;
            AppConfig.LoadFromRegistry();
            this.Size = new Size(HiddenFormSize, HiddenFormSize);
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(HiddenFormLocation, HiddenFormLocation);

            _trayContextMenu = new ContextMenuStrip();
            _menuItemStatus = new ToolStripMenuItem(UiText.StatusChecking) { Enabled = false };

            BuildTrayMenu();

            _sysTrayIcon = new NotifyIcon { Text = UiText.AppName, ContextMenuStrip = _trayContextMenu, Visible = true };
            _sysTrayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    _trayContextMenu.Show(Cursor.Position);
                }
            };

            GlobalInputHook.Install();

            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

            RebuildStateAssets();

            MozcDictionary.DictionaryLoaded += OnMozcDictionaryLoaded;
            if (MozcDictionary.IsLoaded)
            {
                UpdateDictionaryStatusUi();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_hWinEventHookForeground != IntPtr.Zero) NativeMethods.UnhookWinEvent(_hWinEventHookForeground);
            if (_hWinEventHookFocus != IntPtr.Zero) NativeMethods.UnhookWinEvent(_hWinEventHookFocus);
            GlobalInputHook.Uninstall();
            MozcDictionary.Dispose();
            base.OnFormClosing(e);
        }

        private void OnMozcDictionaryLoaded()
        {
            UpdateDictionaryStatusUi();
        }

        // [최적화 2] UI 스레드 접근 패턴을 InvokeRequired를 사용해 일관성 있고 깔끔하게 수정
        private void UpdateDictionaryStatusUi()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(UpdateDictionaryStatusUi));
                return;
            }

            try
            {
                _menuItemStatus.Text = "사전 로드 완료";
                _sysTrayIcon.Text = UiText.TrayTooltip("사전 로드 완료");
            }
            catch { }
        }

        private static Rectangle ResolveCaretRectangle()
        {
            IntPtr hFore = NativeMethods.GetForegroundWindow();
            uint tid = NativeMethods.GetWindowThreadProcessId(hFore, out _);
            NativeMethods.GUITHREADINFO gti = new() { cbSize = Marshal.SizeOf<NativeMethods.GUITHREADINFO>() };

            if (NativeMethods.GetGUIThreadInfo(tid, ref gti) && gti.hwndCaret != IntPtr.Zero)
            {
                NativeMethods.POINT ptTopLeft = new() { X = gti.rectLeft, Y = gti.rectTop };
                NativeMethods.ClientToScreen(gti.hwndCaret, ref ptTopLeft);
                NativeMethods.POINT ptBottomRight = new() { X = gti.rectRight, Y = gti.rectBottom };
                NativeMethods.ClientToScreen(gti.hwndCaret, ref ptBottomRight);

                int width = Math.Max(1, ptBottomRight.X - ptTopLeft.X);
                int height = Math.Max(1, ptBottomRight.Y - ptTopLeft.Y);
                if (height < 5) height = 24;

                return new Rectangle(ptTopLeft.X, ptTopLeft.Y, width, height);
            }

            try
            {
                var focusedElement = System.Windows.Automation.AutomationElement.FocusedElement;
                if (focusedElement != null && focusedElement.TryGetCurrentPattern(System.Windows.Automation.TextPattern.Pattern, out object patternObj))
                {
                    var textPattern = (System.Windows.Automation.TextPattern)patternObj;
                    var selections = textPattern.GetSelection();
                    
                    if (selections.Length > 0)
                    {
                        var rects = selections[0].GetBoundingRectangles();
                        if (rects.Length > 0)
                        {
                            return new Rectangle((int)rects[0].X, (int)rects[0].Y, (int)rects[0].Width, (int)rects[0].Height);
                        }
                    }
                }
            }
            catch { }

            if (NativeMethods.GetCursorPos(out NativeMethods.POINT mPt)) return new Rectangle(mPt.X, mPt.Y, 1, 24);
            return new Rectangle(0, 0, 1, 24);
        }

        // [최적화 3] 중복 코드를 제거하기 위한 제네릭 코어 헬퍼 메소드 구현
        private void ShowKanjiCandidateCore<T>(List<T> candidates, string originalText, bool isReplacingSelection, Action<Rectangle, List<T>, Action<T?>> showOverlay, Func<T, string?> getResultString)
        {
            if (candidates == null || candidates.Count == 0) return;

            try
            {
                this.BeginInvoke(new Action(() =>
                {
                    IntPtr targetHwnd = LastValidFocusHwnd != IntPtr.Zero ? LastValidFocusHwnd : (LastValidHwnd != IntPtr.Zero ? LastValidHwnd : NativeMethods.GetForegroundWindow());
                    bool wasHangul = ImeState.CheckHangulPublic(targetHwnd);
                    Rectangle targetRect = ResolveCaretRectangle();

                    showOverlay(targetRect, candidates, (selected) =>
                    {
                        string? resultStr = selected != null ? getResultString(selected) : null;

                        if (string.IsNullOrEmpty(resultStr)) 
                        {
                            if (wasHangul && targetHwnd != IntPtr.Zero)
                            {
                                ImeState.SetHangulState(targetHwnd, true);
                            }
                            return;
                        }

                        System.Threading.Tasks.Task.Run(() =>
                        {
                            GlobalInputHook.CommitKanjiConversion(originalText, resultStr, isReplacingSelection);

                            if (wasHangul && targetHwnd != IntPtr.Zero)
                            {
                                ImeState.SetHangulState(targetHwnd, true);
                            }
                        });
                    });
                }));
            }
            catch { }
        }

        internal void ShowKanjiCandidateAsync(List<MozcDictionary.KanjiEntry> candidates, string originalText, bool isReplacingSelection = false)
        {
            ShowKanjiCandidateCore(candidates, originalText, isReplacingSelection, KanjiCandidateOverlay.ShowOverlay, item => item?.Kanji);
        }

        internal void ShowKanjiCandidateAsync(List<string> replacements, string originalText, bool isReplacingSelection = false)
        {
            ShowKanjiCandidateCore(replacements, originalText, isReplacingSelection, KanjiCandidateOverlay.ShowOverlay, item => item);
        }

        private void BuildTrayMenu()
        {
            var titleMenuItem = new ToolStripMenuItem($"{UiText.AppName} {UiText.VersionInfo}", null, (s, e) =>
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = UiText.GithubUrl, UseShellExecute = true }); }
                catch (Exception ex) { MessageBox.Show($"웹페이지를 열 수 없습니다.\n{ex.Message}", UiText.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error); }
            });
            titleMenuItem.Font = new Font(titleMenuItem.Font, FontStyle.Bold);
            _trayContextMenu.Items.Add(titleMenuItem);
            _trayContextMenu.Items.Add(_menuItemStatus);
            _trayContextMenu.Items.Add(new ToolStripSeparator());

            _menuItemPointerWinDefault = AddMenuToggle("WIN Default Pointer", AppConfig.ShowPointerWinDefault, (s, e) => UpdatePointerMode(PointerMode.WinDefault));
            _menuItemPointerWinColor   = AddMenuToggle("WIN Color Pointer",   AppConfig.ShowPointerWinColor,   (s, e) => UpdatePointerMode(PointerMode.WinColor));
            _menuItemPointerNewColor   = AddMenuToggle("NEW Color Pointer",   AppConfig.ShowPointerNewColor,   (s, e) => UpdatePointerMode(PointerMode.NewColor));
            SyncPointerMenuChecks();
            _trayContextMenu.Items.Add(new ToolStripSeparator());

            _menuItemCapsWinDefault = AddMenuToggle("한글_Default", AppConfig.ShowCapsHangul, (s, e) => UpdateCapsMode(CapsMode.WinDefault));
            SyncCapsMenuChecks();
            _menuItemCapsEngineer = AddMenuToggle("특수기호_공학용", AppConfig.ShowCapsEngineer, (s, e) => UpdateCapsMode(CapsMode.Engineer));
            _menuItemCapsPali = AddMenuToggle("Pali_Sanskrit", AppConfig.ShowCapsPali, (s, e) => UpdateCapsMode(CapsMode.Pali));
            _menuItemCapsJapanese1 = AddMenuToggle("일본어1_조합형_대표자음", AppConfig.ShowCapsJapanese1, (s, e) => UpdateCapsMode(CapsMode.Japanese1));
            _menuItemCapsJapanese2 = AddMenuToggle("일본어2_조합형_최빈자음", AppConfig.ShowCapsJapanese2, (s, e) => UpdateCapsMode(CapsMode.Japanese2));
            _menuItemCapsJapanese3 = AddMenuToggle("일본어3_완성형_3Layer", AppConfig.ShowCapsJapanese3, (s, e) => UpdateCapsMode(CapsMode.Japanese3));
            AddMenuSeparatorIf(AppConfig.ShowCapsJapanese1 || AppConfig.ShowCapsJapanese2 || AppConfig.ShowCapsJapanese3 || AppConfig.ShowCapsEngineer || AppConfig.ShowCapsPali);

            _menuItemUseMozc = new ToolStripMenuItem("Mozc 오프라인 한자변환", null, async (s, e) =>
            {
                string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mozc_dict_connect.db");

                if (!File.Exists(dbPath))
                {
                    DialogResult result = MessageBox.Show(
                        "오프라인 사전을 사용하려면 'mozc_dict_connect.db' 파일이 필요합니다.\n온라인으로 사전DB 파일을 다운로드 하시겠습니까?",
                        "사전 다운로드",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        try
                        {
                            _menuItemStatus.Text = "현재 상태: 사전 다운로드 중...";
                            string zipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mozc_dict_connect.zip");

                            using (var client = new System.Net.Http.HttpClient())
                            {
                                var response = await client.GetAsync("https://github.com/stonkim93/IMEJapanese/releases/download/IMEJapanese/mozc_dict_connect.zip");
                                response.EnsureSuccessStatusCode();
                                using (var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                                {
                                    await response.Content.CopyToAsync(fs);
                                }
                            }

                            _menuItemStatus.Text = "현재 상태: 사전 압축 해제 중...";

                            ZipFile.ExtractToDirectory(zipPath, AppDomain.CurrentDomain.BaseDirectory, true);

                            if (File.Exists(zipPath))
                            {
                                File.Delete(zipPath);
                            }

                            _menuItemStatus.Text = "현재 상태: 사전 다운로드 완료";
                            MessageBox.Show("사전 파일이 성공적으로 복사되었습니다.", "다운로드 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);

                            if (!MozcDictionary.IsLoaded)
                            {
                                _ = Task.Run(() => { MozcDictionary.LoadDictionary(); });
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"다운로드 또는 압축해제 실패: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            _menuItemStatus.Text = "현재 상태: 사전 다운로드 실패";
                        }
                    }
                }

                if (File.Exists(dbPath))
                {
                    AppConfig.UseGoogleApi = false;
                }
                else
                {
                    AppConfig.UseGoogleApi = true;
                }
                SyncDictionaryApiMenuChecks();
            });

            _menuItemUseGoogleApi = new ToolStripMenuItem("Google 온라인 한자변환", null, (s, e) =>
            {
                AppConfig.UseGoogleApi = true;
                SyncDictionaryApiMenuChecks();
            });

            _trayContextMenu.Items.Add(_menuItemUseMozc);
            _trayContextMenu.Items.Add(_menuItemUseGoogleApi);
            _trayContextMenu.Items.Add(new ToolStripSeparator());

            _menuItemToggleKeyboardLayout = AddMenuToggle("한글CAPS 키보드 배열창", AppConfig.ShowKeyboardlayoutMenu, (s, e) =>
            {
                _isKeyboardLayoutOverlayEnabled = _menuItemToggleKeyboardLayout.Checked;
                AppConfig.DefaultShowKeyboardLayout = _isKeyboardLayoutOverlayEnabled;
                AppConfig.SaveToRegistry();
                if (!_isKeyboardLayoutOverlayEnabled) CloseAllLayoutForms();
                else RefreshKeyboardLayoutOverlay();
            });
            _menuItemToggleKeyboardLayout.CheckOnClick = true;
            _menuItemToggleKeyboardLayout.Checked = _isKeyboardLayoutOverlayEnabled;

            _menuItemToggleTextOverlay = AddMenuToggle("한글CAPS 입력문자 표시창", AppConfig.ShowTextOverlayMenu, (s, e) =>
            {
                _isTextOverlayEnabled = _menuItemToggleTextOverlay.Checked;
                AppConfig.DefaultShowTextOverlay = _isTextOverlayEnabled;
                AppConfig.SaveToRegistry();
                if (!_isTextOverlayEnabled) _frmTextOverlay?.Clear();
            });
            _menuItemToggleTextOverlay.CheckOnClick = true;
            _menuItemToggleTextOverlay.Checked = _isTextOverlayEnabled;

            _menuItemToggleIndicator = AddMenuToggle("엑셀/한글 작은원 표시", AppConfig.ShowSmallCircleMenu, (s, e) =>
            {
                _isMiniIndicatorEnabled = _menuItemToggleIndicator.Checked;
                AppConfig.DefaultEnableMiniIndicator = _isMiniIndicatorEnabled;
                AppConfig.SaveToRegistry();
                if (!_isMiniIndicatorEnabled)
                    UpdateLayeredIndicator(Color.Transparent, -10000, -10000);
            });
            _menuItemToggleIndicator.CheckOnClick = true;
            _menuItemToggleIndicator.Checked = _isMiniIndicatorEnabled;

            _menuItemToggleCopilotMap = AddMenuToggle("한자키 적용/복원 키맵핑", AppConfig.ShowCopilotMapMenu, (s, e) =>
            {
                bool isApplied = RegistryManager.IsMappingApplied();
                bool apply = !isApplied;
                string actionName = apply ? "적용" : "복원";

                if (MessageBox.Show($"Copilot 키를 한자키로 {actionName}하시겠습니까?\n(관리자 권한 및 재부팅 필요)", "키맵핑 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    if (RegistryManager.ToggleMapping(apply))
                    {
                        _menuItemToggleCopilotMap.Checked = apply;
                        AppConfig.EnableCopilotMap = apply;
                        MessageBox.Show($"키맵핑 {actionName} 완료.\n재부팅(Reboot)해 주시기 바랍니다.", "재부팅 필요", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else _menuItemToggleCopilotMap.Checked = isApplied;
                }
                else _menuItemToggleCopilotMap.Checked = isApplied;
            });
            _menuItemToggleCopilotMap.CheckOnClick = false;
            _menuItemToggleCopilotMap.Checked = RegistryManager.IsMappingApplied();
            AppConfig.EnableCopilotMap = RegistryManager.IsMappingApplied();

            AddMenuSeparatorIf(AppConfig.ShowKeyboardlayoutMenu || AppConfig.ShowTextOverlayMenu || AppConfig.ShowCopilotMapMenu);
            _trayContextMenu.Items.Add(new ToolStripMenuItem("종료 (Exit)", null, (s, e) => this.Close()));

            SyncCapsMenuChecks();
            SyncDictionaryApiMenuChecks();
        }

        private void SyncDictionaryApiMenuChecks()
        {
            if (_menuItemUseMozc != null) _menuItemUseMozc.Checked = !AppConfig.UseGoogleApi;
            if (_menuItemUseGoogleApi != null) _menuItemUseGoogleApi.Checked = AppConfig.UseGoogleApi;
        }

        private ToolStripMenuItem AddMenuToggle(string text, bool show, EventHandler onClick)
        {
            var item = new ToolStripMenuItem(text, null, onClick);
            if (show) _trayContextMenu.Items.Add(item);
            return item;
        }

        private void AddMenuSeparatorIf(bool condition) { if (condition) _trayContextMenu.Items.Add(new ToolStripSeparator()); }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WindowPosChangedMessage) Task.Delay(200).ContinueWith(_ => this.BeginInvoke(new Action(() => RebuildAssetsWithRetry(RebuildRetryAfterWindowPosChangedMs))));
            base.WndProc(ref m);
        }

        protected override void OnPaint(PaintEventArgs e) { }
        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _currentContextHwnd = NativeMethods.GetForegroundWindow();
            _lastPolledHwnd = _currentContextHwnd;
            _lastForegroundHwnd = _currentContextHwnd;
            _lastHangulSyncState = ImeState.CheckHangulPublic(_currentContextHwnd);

            _frmTextOverlay = new TextOverlayForm();

            if (_currentContextHwnd != IntPtr.Zero && !IsTaskbarWindow(_currentContextHwnd) && !IsAppOrTrayWindow(_currentContextHwnd))
            {
                LastValidHwnd = _currentContextHwnd;
                LastValidFocusHwnd = SearchFocusedInputHwnd(_currentContextHwnd);
            }

            ApplyVisualState(ImeState.Detect(_currentContextHwnd, _activeCapsMode == CapsMode.Japanese1, _activeCapsMode == CapsMode.Japanese2, _activeCapsMode == CapsMode.Japanese3));
            
            _winEventProc = new NativeMethods.WinEventDelegate(WinEventCallback);
            IntPtr winEventProcPtr = Marshal.GetFunctionPointerForDelegate(_winEventProc);
            _hWinEventHookForeground = NativeMethods.SetWinEventHook(NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, winEventProcPtr, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);
            _hWinEventHookFocus = NativeMethods.SetWinEventHook(NativeMethods.EVENT_OBJECT_FOCUS, NativeMethods.EVENT_OBJECT_FOCUS, IntPtr.Zero, winEventProcPtr, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);
            
            ProcessStateCheck(null, EventArgs.Empty);
        }

        private void WinEventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (this.IsHandleCreated) {
                this.BeginInvoke(new Action(() => ProcessStateCheck(null, EventArgs.Empty)));
            }
        }

        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            if (this.InvokeRequired) { this.BeginInvoke(new Action(() => OnDisplaySettingsChanged(sender, e))); return; }
            Task.Delay(DisplaySettingsChangedDelayMs).ContinueWith(_ => this.BeginInvoke(new Action(() => RebuildAssetsWithRetry(RebuildRetryAfterScaleChangeMs))));
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.Accessibility || e.Category == UserPreferenceCategory.Mouse)
            {
                if (this.InvokeRequired) { this.BeginInvoke(new Action(() => OnUserPreferenceChanged(sender, e))); return; }
                Task.Delay(UserPreferenceChangedDelayMs).ContinueWith(_ => this.BeginInvoke(new Action(() => RebuildAssetsWithRetry(RebuildRetryAfterScaleChangeMs))));
            }
        }

        public void RequestLayoutRefresh() => this.BeginInvoke(new Action(RefreshKeyboardLayoutOverlay));
        public void RequestStateCheck() => this.BeginInvoke(new Action(() => ProcessStateCheck(null, EventArgs.Empty)));

        private void UpdateCapsMode(CapsMode mode)
        {
            _activeCapsMode = mode;
            AppConfig.DefaultCapsMode = (int)mode;
            AppConfig.SaveToRegistry();
            SyncCapsMenuChecks();
            _previousImeState = (ImeState.State)(-1);
            RefreshKeyboardLayoutOverlay();

            IntPtr activeHwnd = NativeMethods.GetForegroundWindow();
            if (activeHwnd != IntPtr.Zero && (IsTaskbarWindow(activeHwnd) || IsAppOrTrayWindow(activeHwnd)))
                EnforceCapsModeToTarget(activeHwnd, 1);

            IntPtr targetHwnd = LastValidFocusHwnd != IntPtr.Zero ? LastValidFocusHwnd : (LastValidHwnd != IntPtr.Zero ? LastValidHwnd : activeHwnd);
            if (targetHwnd != IntPtr.Zero)
            {
                if (!IsTaskbarWindow(targetHwnd) && !IsAppOrTrayWindow(targetHwnd)) NativeMethods.SetForegroundWindow(targetHwnd);
                EnforceCapsModeToTarget(targetHwnd);
            }
        }

        public string GetCapsModeOverlayText()
        {
            return _activeCapsMode switch
            {
                CapsMode.Japanese1 => "일본어1_조합형",
                CapsMode.Japanese2 => "일본어2_조합형",
                CapsMode.Japanese3 => "일본어3_3Layer",
                _ => "일본어 모드"
            };
        }

        private void SyncCapsMenuChecks()
        {
            if (_menuItemCapsWinDefault != null) _menuItemCapsWinDefault.Checked = (_activeCapsMode == CapsMode.WinDefault);
            if (_menuItemCapsEngineer != null) _menuItemCapsEngineer.Checked = (_activeCapsMode == CapsMode.Engineer);
            if (_menuItemCapsPali != null) _menuItemCapsPali.Checked = (_activeCapsMode == CapsMode.Pali);
            if (_menuItemCapsJapanese1 != null) _menuItemCapsJapanese1.Checked = (_activeCapsMode == CapsMode.Japanese1);
            if (_menuItemCapsJapanese2 != null) _menuItemCapsJapanese2.Checked = (_activeCapsMode == CapsMode.Japanese2);
            if (_menuItemCapsJapanese3 != null) _menuItemCapsJapanese3.Checked = (_activeCapsMode == CapsMode.Japanese3);
        }

        // ---------------------------------------------------------
        // 포인터 모드 제어
        // ---------------------------------------------------------
        private void UpdatePointerMode(PointerMode mode)
        {
            _activePointerMode = mode;
            AppConfig.DefaultPointerMode = (int)mode;
            AppConfig.SaveToRegistry();
            SyncPointerMenuChecks();
            _previousImeState = (ImeState.State)(-1);
            // WinColor 모드는 색상 커서 생성이 필요하므로 에셋을 다시 빌드합니다.
            if (mode == PointerMode.WinColor)
            {
                RebuildStateAssets();
            }
        }

        private void SyncPointerMenuChecks()
        {
            if (_menuItemPointerWinDefault != null) _menuItemPointerWinDefault.Checked = (_activePointerMode == PointerMode.WinDefault);
            if (_menuItemPointerWinColor   != null) _menuItemPointerWinColor.Checked   = (_activePointerMode == PointerMode.WinColor);
            if (_menuItemPointerNewColor   != null) _menuItemPointerNewColor.Checked   = (_activePointerMode == PointerMode.NewColor);
        }

        private void ApplyCapsModeBase(IntPtr targetHwnd)
        {
            if (targetHwnd == IntPtr.Zero) return;
            ImeState.SetHangulState(targetHwnd, true);
            bool capsOn = (NativeMethods.GetKeyState(NativeMethods.VK_CAPITAL) & 0x0001) != 0;
            if (!capsOn) NativeMethods.SimulateCapsLock();
        }

        private void EnforceCapsModeToTarget(IntPtr targetHwnd, int retryCount = 2)
        {
            if (targetHwnd == IntPtr.Zero) return;
            ApplyCapsModeBase(targetHwnd);
            if (ImeState.CheckHangulPublic(targetHwnd) || retryCount <= 0) return;

            IntPtr rootHwnd = LastValidHwnd != IntPtr.Zero ? LastValidHwnd : targetHwnd;

            Task.Delay(TrayContextMenuForegroundDelayRetryMs).ContinueWith(_ =>
                this.BeginInvoke(new Action(() =>
                {
                    IntPtr retryTarget = SearchFocusedInputHwnd(rootHwnd);
                    if (retryTarget == IntPtr.Zero) retryTarget = rootHwnd;

                    if (retryTarget != IntPtr.Zero && !IsTaskbarWindow(retryTarget) && !IsAppOrTrayWindow(retryTarget)) NativeMethods.SetForegroundWindow(retryTarget);
                    EnforceCapsModeToTarget(retryTarget, retryCount - 1);
                })));
        }

        private void ProcessStateCheck(object? sender, EventArgs e)
        {
            IntPtr actualHFore = NativeMethods.GetForegroundWindow();
            bool isFocusChanged = (actualHFore != _lastPolledHwnd);

            bool isTaskbar = IsTaskbarWindow(actualHFore);
            bool isTrayOrApp = IsAppOrTrayWindow(actualHFore);
            bool isLayoutForm = IsLayoutFormForeground(actualHFore);

            CacheLastValidWindows(actualHFore, isTaskbar, isTrayOrApp, isLayoutForm);
            SyncSystemHangulState(actualHFore, isTaskbar, isTrayOrApp, isLayoutForm, isFocusChanged);

            _lastPolledHwnd = actualHFore;

            IntPtr contextHwnd = ResolveContextHwnd(actualHFore);
            bool cachedIsHangulMode = (isTaskbar || isTrayOrApp || isLayoutForm) ? _lastHangulSyncState : ImeState.CheckHangulPublic(contextHwnd);
            ushort contextLangId = ResolveLanguageId(contextHwnd);

            TrackCurrentWindow(contextHwnd, isTaskbar, isTrayOrApp, isLayoutForm);

            ImeState.State currentState = ImeState.Detect(
                contextHwnd,
                enablePali:      _activeCapsMode == CapsMode.Pali,
                enableJapanese1: _activeCapsMode == CapsMode.Japanese1,
                enableJapanese2: _activeCapsMode == CapsMode.Japanese2,
                enableJapanese3: _activeCapsMode == CapsMode.Japanese3,
                enableEngineer:  _activeCapsMode == CapsMode.Engineer);

            ActiveInputModeContext activeInputMode = ResolveInputModeContext(currentState);

            GlobalInputHook.UpdateContext(new GlobalInputHook.HookContextSnapshot(
                contextHwnd, contextLangId, cachedIsHangulMode, activeInputMode.ActiveProcessor,
                activeInputMode.IsPaliModeActive, activeInputMode.IsEngineerModeActive,
                activeInputMode.IsJapanese1ModeActive, activeInputMode.IsJapanese2ModeActive, activeInputMode.IsJapanese3ModeActive));

            if (KanjiCandidateOverlay.IsActive)
            {
                RefreshKeyboardLayoutOverlay();
                return;
            }

            if (currentState != _previousImeState)
            {
                _previousImeState = currentState;
                ApplyVisualState(currentState);
            }

            // 엑셀/한글 작은원 인디케이터를 현재 마우스 커서 위치에 렌더링
            if (_isCurrentProcessTarget)
                RenderMiniIndicator(currentState);
            else
                UpdateLayeredIndicator(Color.Transparent, HiddenLayeredWindowLocation, HiddenLayeredWindowLocation);

            RefreshKeyboardLayoutOverlay();
        }

        private bool IsLayoutFormForeground(IntPtr actualHFore) => _frmKeyboardLayout != null && actualHFore == _frmKeyboardLayout.Handle;

        private void CacheLastValidWindows(IntPtr actualHFore, bool isTaskbar, bool isTrayOrApp, bool isLayoutForm)
        {
            if (!isTaskbar && !isTrayOrApp && !isLayoutForm && actualHFore != IntPtr.Zero && actualHFore != this.Handle)
            {
                LastValidHwnd = actualHFore;
                LastValidFocusHwnd = SearchFocusedInputHwnd(actualHFore);
            }
        }

        private void SyncSystemHangulState(IntPtr actualHFore, bool isTaskbar, bool isTrayOrApp, bool isLayoutForm, bool isFocusChanged)
        {
            // 작업 표시줄(isTaskbar)은 자체적인 IME 컨텍스트를 가질 수 있으며 검색창 등에서 한영키 입력이 가능하므로
            // 동기화 대상(isOurWindow)에서 제외하여 한영키 입력 시 상태 변화를 감지하고 LastValidHwnd로 동기화하도록 수정합니다.
            bool isOurWindow = (isTrayOrApp || isLayoutForm);

            bool isCurrentHangul;
            if (isOurWindow)
            {
                isCurrentHangul = _lastHangulSyncState;
            }
            else
            {
                isCurrentHangul = ImeState.CheckHangulPublic(actualHFore);
            }

            if (isFocusChanged)
            {
                if (!isOurWindow)
                {
                    _lastHangulSyncState = isCurrentHangul;
                }
            }
            else if (!isOurWindow && isCurrentHangul != _lastHangulSyncState)
            {
                _lastHangulSyncState = isCurrentHangul;

                Action<IntPtr> SetState = (hwnd) => { if (hwnd != IntPtr.Zero && hwnd != actualHFore) ImeState.SetHangulState(hwnd, isCurrentHangul); };
                SetState(LastValidHwnd);
                SetState(_frmKeyboardLayout?.Handle ?? IntPtr.Zero);
                SetState(this.Handle);

                IntPtr kanjiOverlayHandle = KanjiCandidateOverlay.ActiveHandle;
                if (kanjiOverlayHandle != IntPtr.Zero)
                {
                    SetState(kanjiOverlayHandle);
                }
            }
        }

        private IntPtr ResolveContextHwnd(IntPtr actualHFore) => (LastValidHwnd != IntPtr.Zero) ? LastValidHwnd : actualHFore;

        private static ushort ResolveLanguageId(IntPtr contextHwnd)
        {
            if (contextHwnd == IntPtr.Zero) return 0;
            uint tid = NativeMethods.GetWindowThreadProcessId(contextHwnd, out _);
            return (ushort)(NativeMethods.GetKeyboardLayout(tid).ToInt64() & 0xFFFF);
        }

        private void TrackCurrentWindow(IntPtr contextHwnd, bool isTaskbar, bool isTrayOrApp, bool isLayoutForm)
        {
            if (contextHwnd != _currentContextHwnd)
            {
                if (!isTaskbar && !isTrayOrApp && !isLayoutForm)
                {
                    _lastForegroundHwnd = contextHwnd;
                    _isCurrentProcessTarget = EvaluateTargetProcess(contextHwnd);
                    _isPointerInIBeamCell = false;
                }
                _currentContextHwnd = contextHwnd;
            }
        }

        private static bool EvaluateTargetProcess(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return false;
            NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid); 
            if (pid == 0) return false;
            
            try 
            { 
                using var proc = System.Diagnostics.Process.GetProcessById((int)pid);
                string n = proc.ProcessName; 
                foreach (string a in AppConfig.IndicatorTargetApps) 
                {
                    if (n.Equals(a, StringComparison.OrdinalIgnoreCase)) return true; 
                }
            } 
            catch { } 
            return false;
        }

        private ActiveInputModeContext ResolveInputModeContext(ImeState.State state)
        {
            foreach (var map in _capsModeMaps)
            {
                if (_activeCapsMode == map.Mode && state == map.ActiveState)
                {
                    return new ActiveInputModeContext(
                        map.Mode == CapsMode.Pali,      // isPaliModeActive
                        map.Mode == CapsMode.Engineer,  // isEngineerModeActive
                        map.Mode == CapsMode.Japanese1, 
                        map.Mode == CapsMode.Japanese2, 
                        map.Mode == CapsMode.Japanese3, 
                        map.Processor
                    );
                }
            }
            return new ActiveInputModeContext(false, false, false, false, false, null);
        }

        public void ShowOverlay(string text, int durationMs = AppConfig.OverlayDefaultDurationMs, OverlayPositionMode mode = OverlayPositionMode.CharInput)
        {
            if (!_isTextOverlayEnabled) return;

            float scaledFontSize = AppConfig.OverlayDefaultFontSize * _currentDpiScale;
            int scaledHeight = (int)Math.Round(AppConfig.OverlayDefaultHeight * _currentDpiScale);
            int scaledCharWidth = (int)Math.Round(AppConfig.OverlayDefaultCharWidth * _currentDpiScale);
            int scaledPadWidth = (int)Math.Round(AppConfig.OverlayDefaultPaddingWidth * _currentDpiScale);
            int scaledYOffset = (int)Math.Round(AppConfig.OverlayDefaultYOffset * _currentDpiScale);

            if (this.InvokeRequired) this.BeginInvoke(new Action(() => ExecuteShowOverlay(text, durationMs > 0, scaledFontSize, scaledHeight, scaledCharWidth, scaledPadWidth, scaledYOffset, mode)));
            else ExecuteShowOverlay(text, durationMs > 0, scaledFontSize, scaledHeight, scaledCharWidth, scaledPadWidth, scaledYOffset, mode);
        }

        public void ClearOverlay() => _frmTextOverlay?.Clear();

        private void ExecuteShowOverlay(string ch, bool useTimer, float fontSize, int formH, int charW, int padW, int yOffset, OverlayPositionMode mode)
        {
            int length = 0; foreach (char c in ch) length += (c >= 0x1100 && c <= 0xD7AF) ? 2 : 1;
            int minWidth = (int)Math.Round(40 * _currentDpiScale);
            Size sz = new Size(Math.Max(length * (charW / 2) + padW, minWidth), formH);

            Point pt = CalculateOverlayLocation(sz, yOffset, mode);
            _frmTextOverlay?.ShowOverlay(ch, useTimer, fontSize, sz.Width, sz.Height, pt.X, pt.Y);
        }

        private static Point CalculateOverlayLocation(Size overlaySize, int yOffset, OverlayPositionMode mode)
        {
            Rectangle caretRect = ResolveCaretRectangle();
            var screen = Screen.FromPoint(caretRect.Location);
            int x = caretRect.Left;
            int y = caretRect.Bottom + yOffset;

            if (mode == OverlayPositionMode.ModeSwitch)
            {
                x = screen.WorkingArea.Left + (screen.WorkingArea.Width - overlaySize.Width) / 2;
                y = screen.WorkingArea.Top + (screen.WorkingArea.Height * 3) / 4 - overlaySize.Height / 2;
            }
            else if (mode == OverlayPositionMode.CharInput || mode == OverlayPositionMode.CharToggle)
            {
                x = caretRect.Right - overlaySize.Width;
            }
            else if (mode == OverlayPositionMode.SelectionToggle)
            {
                x = caretRect.Left;
            }

            if (x + overlaySize.Width > screen.WorkingArea.Right)
                x = screen.WorkingArea.Right - overlaySize.Width;
            if (x < screen.WorkingArea.Left)
                x = screen.WorkingArea.Left;

            if (y + overlaySize.Height > screen.WorkingArea.Bottom)
                y = caretRect.Top - overlaySize.Height - 5;
            if (y < screen.WorkingArea.Top)
                y = screen.WorkingArea.Top;

            return new Point(x, y);
        }

        private void RebuildAssetsWithRetry(int retryDelayMs)
        {
            RebuildStateAssets();
            if (retryDelayMs > 0)
            {
                Task.Delay(retryDelayMs).ContinueWith(_ => this.BeginInvoke(new Action(RebuildStateAssets)));
            }
        }

        private void RebuildStateAssets()
        {
            bool trayWasVisible = false;
            try { trayWasVisible = _sysTrayIcon?.Visible ?? false; } catch { }

            foreach (var asset in _assetCache.Values) try { asset.Dispose(); } catch { }
            _assetCache.Clear(); RestoreDefaults();

            float dpi = 96f;
            IntPtr hFore = NativeMethods.GetForegroundWindow();
            if (hFore != IntPtr.Zero)
            {
                IntPtr hMonitor = NativeMethods.MonitorFromWindow(hFore, NativeMethods.MONITOR_DEFAULTTONEAREST);
                if (hMonitor != IntPtr.Zero && NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 && dpiX > 0) dpi = dpiX;
            }
            else { uint sysDpi = NativeMethods.GetDpiForSystem(); if (sysDpi > 0) dpi = sysDpi; }

            _currentDpiScale = dpi / 96f;
            _pointerPhysicalSize = Math.Max(32, (int)Math.Round(32 * _currentDpiScale));
            _physIndicatorOffsetX = _pointerPhysicalSize * 0.5f;

            bool winColorFailed = false;

            // Program.cs 내 RebuildStateAssets 메서드의 커서 생성 부분 수정
            foreach (ImeState.State state in Enum.GetValues(typeof(ImeState.State)))
            {
                if (!AppConfig.Themes.TryGetValue(state, out AppConfig.Theme t)) continue;
                try
                {
                    // [수정] Config.cs에 정의된 t.IBeamColor를 그대로 사용하도록 강제 변환 로직 제거
                    IntPtr hArrowNew = PointerGraphicsFactory.CreateColoredSystemPointer(NativeMethods.OCR_NORMAL, t.PointerColor, _pointerPhysicalSize);
                    IntPtr hIBeamNew = PointerGraphicsFactory.CreateColoredSystemPointer(NativeMethods.OCR_IBEAM,  t.IBeamColor,   _pointerPhysicalSize);
                    IntPtr hArrowWin = PointerGraphicsFactory.CreateColoredSystemPointer(NativeMethods.OCR_NORMAL, t.PointerColor, _pointerPhysicalSize);
                    IntPtr hIBeamWin = PointerGraphicsFactory.CreateColoredSystemPointer(NativeMethods.OCR_IBEAM,  t.IBeamColor,   _pointerPhysicalSize);

                    if (hArrowWin == IntPtr.Zero) { hArrowWin = NativeMethods.CopyIcon(hArrowNew); winColorFailed = true; }
                    if (hIBeamWin == IntPtr.Zero) { hIBeamWin = NativeMethods.CopyIcon(hIBeamNew); winColorFailed = true; }

                    _assetCache[state] = new StateAssets
                    {
                        DotColor = t.PointerColor, Description = t.Description,
                        ArrowNewPtr = hArrowNew, IBeamNewPtr = hIBeamNew,
                        ArrowWinPtr = hArrowWin, IBeamWinPtr = hIBeamWin,
                        TrayIcon = BuildTrayIcon(t.TrayText, t.TrayBgColor, t.TrayTextColor),
                        IBeamCompareHandleNew = NativeMethods.CopyIcon(hIBeamNew),
                        IBeamCompareHandleWin = NativeMethods.CopyIcon(hIBeamWin)
                    };
                }
                catch { }
            }

            try
            {
                if (trayWasVisible && _sysTrayIcon != null)
                {
                    _sysTrayIcon.Visible = true;
                    ImeState.State st = _previousImeState == (ImeState.State)(-1) ? ImeState.State.EnglishLower : _previousImeState;
                    if (_assetCache.TryGetValue(st, out var ast) && ast.TrayIcon != null) _sysTrayIcon.Icon = ast.TrayIcon;
                }
            }
            catch { }

            // WinColor 모드에서 커서 생성 실패 시 NewColor로 자동 전환
            if (_activePointerMode == PointerMode.WinColor && winColorFailed)
            {
                _activePointerMode = PointerMode.NewColor; SyncPointerMenuChecks(); _previousImeState = (ImeState.State)(-1);
            }
        }


        private static Icon BuildTrayIcon(string text, Color bg, Color fg)
        {
            using Bitmap bmp = new(AppConfig.TrayIconSize, AppConfig.TrayIconSize);
            using Graphics g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using SolidBrush bgBrush = new(bg); g.FillRectangle(bgBrush, 0, 0, AppConfig.TrayIconSize, AppConfig.TrayIconSize);

            bool lower = !string.IsNullOrEmpty(text) && char.IsLower(text[0]);
            using Font font = new(lower ? "Segoe Print" : "Segoe UI Black", lower ? AppConfig.TrayLowercaseFontSize : AppConfig.TrayUppercaseFontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            using SolidBrush fgBrush = new(fg);
            using StringFormat sf = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };

            RectangleF rect = lower ? TrayIconTextRectLower : TrayIconTextRectUpper;
            if (lower)
            {
                g.DrawString(text, font, fgBrush, new RectangleF(rect.X, rect.Y, rect.Width, rect.Height), sf);
                g.DrawString(text, font, fgBrush, new RectangleF(rect.X + 1f, rect.Y, rect.Width, rect.Height), sf);
                g.DrawString(text, font, fgBrush, new RectangleF(rect.X, rect.Y + 1f, rect.Width, rect.Height), sf);
                g.DrawString(text, font, fgBrush, new RectangleF(rect.X + 1f, rect.Y + 1f, rect.Width, rect.Height), sf);
                g.DrawString(text, font, fgBrush, new RectangleF(rect.X + 0.5f, rect.Y + 0.5f, rect.Width, rect.Height), sf);
            }
            else g.DrawString(text, font, fgBrush, rect, sf);

            IntPtr hIcon = bmp.GetHicon(); Icon icon = (Icon)Icon.FromHandle(hIcon).Clone(); NativeMethods.DestroyIcon(hIcon); return icon;
        }

        private void ApplyVisualState(ImeState.State state)
        {
            if (!_assetCache.TryGetValue(state, out StateAssets? assets)) return;
            _currentIndicatorColor = assets.DotColor;

            try { if (assets.TrayIcon != null && (_sysTrayIcon.Icon == null || _sysTrayIcon.Icon.Handle != assets.TrayIcon.Handle)) _sysTrayIcon.Icon = assets.TrayIcon; }
            catch { _sysTrayIcon.Icon = assets.TrayIcon; }

            // 포인터 모드에 따라 마우스 커서 적용
            switch (_activePointerMode)
            {
                case PointerMode.WinDefault:
                    RestoreDefaults(); _lastAppliedArrowHandle = IntPtr.Zero; break;
                case PointerMode.WinColor:
                case PointerMode.NewColor:
                    IntPtr hArr = NativeMethods.CopyIcon(_activePointerMode == PointerMode.WinColor ? assets.ArrowWinPtr : assets.ArrowNewPtr);
                    IntPtr hIb  = NativeMethods.CopyIcon(_activePointerMode == PointerMode.WinColor ? assets.IBeamWinPtr : assets.IBeamNewPtr);
                    _lastAppliedArrowHandle = hArr;
                    if (hArr != IntPtr.Zero) { if (!NativeMethods.SetSystemCursor(hArr, NativeMethods.OCR_NORMAL)) NativeMethods.DestroyCursor(hArr); }
                    if (hIb  != IntPtr.Zero) { if (!NativeMethods.SetSystemCursor(hIb,  NativeMethods.OCR_IBEAM))  NativeMethods.DestroyCursor(hIb); }
                    break;
            }

            _sysTrayIcon.Text = UiText.TrayTooltip(assets.Description);
            _menuItemStatus.Text = UiText.StatusLabel(assets.Description);
        }

        private void RefreshKeyboardLayoutOverlay()
        {
            if (!_isKeyboardLayoutOverlayEnabled) { CloseAllLayoutForms(); return; }

            var processor = GlobalInputHook.ActiveProcessor;
            bool isPhyShift = (NativeMethods.GetKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;
            if (AppConfig.EnableCopilotMap && ((NativeMethods.GetKeyState(NativeMethods.VK_LWIN) & 0x8000) != 0 || (NativeMethods.GetKeyState(NativeMethods.VK_RWIN) & 0x8000) != 0)) isPhyShift = false;

            bool isVirtShift = processor != null ? processor.IsVirtualShift : _isShiftVisualInverted;
            string suffix = (isPhyShift ^ isVirtShift) ? "2" : "1";
            string? name = null;
            switch (_previousImeState)
            {
                case ImeState.State.EnglishLower:
                case ImeState.State.EnglishUpper:
                case ImeState.State.JapaneseIME:
                    name = $"EnglishKey{suffix}.png";
                    break;
                case ImeState.State.Hangul:
                    name = $"KoreanKey{suffix}.png";
                    break;
                case ImeState.State.PaliUS:
                case ImeState.State.PaliHangul:
                    name = $"PaliKey{suffix}.png";
                    break;
                case ImeState.State.Engineer:
                    name = $"EngineerKey{suffix}.png";
                    break;
                case ImeState.State.JapaneseHangul1:
                    name = $"Japan1Key{suffix}.png";
                    break;
                case ImeState.State.JapaneseHangul2:
                    name = $"Japan2Key{suffix}.png";
                    break;
                case ImeState.State.JapaneseHangul3:
                    name = $"Japan3Layer{(processor?.CurrentLayer ?? 1)}Key{suffix}.png";
                    break;
                default:
                    name = $"KoreanKey{suffix}.png";
                    break;
            }

            if (name == null) return;

            if (_frmKeyboardLayout == null || _frmKeyboardLayout.IsDisposed)
            {
                _frmKeyboardLayout = new KeyboardLayoutForm();
                if (_lastKeyboardLayoutLocation != Point.Empty) _frmKeyboardLayout.Location = _lastKeyboardLayoutLocation;
                _frmKeyboardLayout.OnLayoutDoubleClicked += (s, e) => { if (GlobalInputHook.ActiveProcessor != null) GlobalInputHook.ActiveProcessor.ToggleVirtualShift(); else _isShiftVisualInverted = !_isShiftVisualInverted; RefreshKeyboardLayoutOverlay(); };
                _frmKeyboardLayout.OnClosedByUser += (s, e) => { _isKeyboardLayoutOverlayEnabled = false; _menuItemToggleKeyboardLayout.Checked = false; CloseAllLayoutForms(); };
            }

            _frmKeyboardLayout.UpdateImage(name);
            if (!_frmKeyboardLayout.Visible) { _frmKeyboardLayout.Show(); if (_frmKeyboardLayout.WindowState == FormWindowState.Minimized) _frmKeyboardLayout.WindowState = FormWindowState.Normal; }
        }

        private void CloseAllLayoutForms()
        {
            if (_frmKeyboardLayout != null) { _lastKeyboardLayoutLocation = _frmKeyboardLayout.Location; _frmKeyboardLayout.Close(); _frmKeyboardLayout = null; }
        }

        // ---------------------------------------------------------
        // 포인터 복원 및 커서 상태 감지
        // ---------------------------------------------------------

        /// <summary>시스템 기본 커서로 복원합니다 (Windows 기본 포인터 모드 및 종료 시 호출).</summary>
        public static void RestoreDefaults() => NativeMethods.SystemParametersInfo(NativeMethods.SPI_SETCURSORS, 0, IntPtr.Zero, NativeMethods.SPIF_SENDCHANGE);

        // ---------------------------------------------------------
        // 작은원(Mini Indicator) 렌더링 - 엑셀/한글 커서 옆에 작은 색상 원 표시
        // ---------------------------------------------------------

        private void RenderMiniIndicator(ImeState.State state)
        {
            if (!NativeMethods.GetCursorPos(out NativeMethods.POINT pt)) return;
            
            if (_isCurrentProcessTarget && _isMiniIndicatorEnabled)
            {
                bool isIBeam = EvaluatePointerIsIBeam(state);
                if (isIBeam != _isPointerInIBeamCell) 
                { 
                    UpdateLayeredIndicator(Color.Transparent, HiddenLayeredWindowLocation, HiddenLayeredWindowLocation); 
                    _isPointerInIBeamCell = isIBeam; 
                }
                
                if (!_isPointerInIBeamCell)
                {
                    float tx = pt.X + (EvaluatePointerIsArrow() ? PointerDiagonalFactor * AppConfig.IndicatorOffset * (_pointerPhysicalSize / 32f) : _physIndicatorOffsetX);
                    float ty = pt.Y + (EvaluatePointerIsArrow() ? PointerDiagonalFactor * AppConfig.IndicatorOffset * (_pointerPhysicalSize / 32f) : _pointerPhysicalSize * IBeamIndicatorYOffsetFactor);
                    if (ty < pt.Y + _pointerPhysicalSize + IndicatorBottomMargin) ty = pt.Y + _pointerPhysicalSize + IndicatorBottomMargin;
                    
                    UpdateLayeredIndicator(_currentIndicatorColor, (int)Math.Round(tx - _indicatorCanvasSize / 2f), (int)Math.Round(ty - _indicatorCanvasSize / 2f));
                }
                else 
                {
                    UpdateLayeredIndicator(Color.Transparent, HiddenLayeredWindowLocation, HiddenLayeredWindowLocation);
                }
            }
            else 
            {
                UpdateLayeredIndicator(Color.Transparent, HiddenLayeredWindowLocation, HiddenLayeredWindowLocation);
            }
        }

        private void UpdateLayeredIndicator(Color c, int x, int y)
        {
            bool update = false;
            if (c != _lastRenderedIndicatorColor) 
            { 
                _lastRenderedIndicatorColor = c; 
                if (c != Color.Transparent) RenderIndicatorBuffer(c); 
                update = true; 
            }
            if (x != _lastIndicatorX || y != _lastIndicatorY) 
            { 
                _lastIndicatorX = x; 
                _lastIndicatorY = y; 
                update = true; 
            }
            if (!update) return;

            NativeMethods.SIZE sz = new() { cx = _indicatorCanvasSize, cy = _indicatorCanvasSize };
            NativeMethods.POINT src = new() { X = 0, Y = 0 }, dst = new() { X = x, Y = y };
            NativeMethods.BLENDFUNCTION bf = new() { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };

            if (c == Color.Transparent || !_isIndicatorRendered)
            {
                if (_dcIndicatorMem != IntPtr.Zero)
                {
                    dst.X = -10000; dst.Y = -10000; bf.SourceConstantAlpha = 0;
                    IntPtr sDc = NativeMethods.GetDC(IntPtr.Zero);
                    _ = NativeMethods.UpdateLayeredWindow(this.Handle, sDc, ref dst, ref sz, _dcIndicatorMem, ref src, 0, ref bf, 2);
                    _ = NativeMethods.ReleaseDC(IntPtr.Zero, sDc);
                }
                return;
            }
            IntPtr curDc = NativeMethods.GetDC(IntPtr.Zero);
            _ = NativeMethods.UpdateLayeredWindow(this.Handle, curDc, ref dst, ref sz, _dcIndicatorMem, ref src, 0, ref bf, 2);
            _ = NativeMethods.ReleaseDC(IntPtr.Zero, curDc);
        }

        private void RenderIndicatorBuffer(Color c)
        {
            if (_dcIndicatorMem != IntPtr.Zero) { if (_hBmpIndicatorOld != IntPtr.Zero) NativeMethods.SelectObject(_dcIndicatorMem, _hBmpIndicatorOld); NativeMethods.DeleteDC(_dcIndicatorMem); _dcIndicatorMem = IntPtr.Zero; }
            if (_hBmpIndicator != IntPtr.Zero) { NativeMethods.DeleteObject(_hBmpIndicator); _hBmpIndicator = IntPtr.Zero; }
            if (_dcIndicatorScreen != IntPtr.Zero) { NativeMethods.ReleaseDC(IntPtr.Zero, _dcIndicatorScreen); _dcIndicatorScreen = IntPtr.Zero; }
            if (c == Color.Transparent) { _isIndicatorRendered = false; return; }

            float sz = AppConfig.IndicatorSize * _currentDpiScale, pW = 1.0f;
            _indicatorCanvasSize = (int)Math.Ceiling(sz + (pW * 2) + 6); if (_indicatorCanvasSize % 2 != 0) _indicatorCanvasSize++;

            using Bitmap bmp = new(_indicatorCanvasSize, _indicatorCanvasSize, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.Clear(Color.Transparent);
                float ct = _indicatorCanvasSize / 2f, r = sz / 2f;
                using SolidBrush b = new(c); g.FillEllipse(b, ct - r, ct - r, sz, sz);
                using Pen p = new(c == Color.White ? Color.Black : (c == Color.Black ? Color.White : Color.Black), pW); g.DrawEllipse(p, ct - r, ct - r, sz, sz);
            }
            
            _dcIndicatorScreen = NativeMethods.GetDC(IntPtr.Zero); _dcIndicatorMem = NativeMethods.CreateCompatibleDC(_dcIndicatorScreen);
            NativeMethods.BITMAPINFO bmi = new() { biSize = s_bmiSize, biWidth = bmp.Width, biHeight = -bmp.Height, biPlanes = 1, biBitCount = 32, biCompression = 0 };
            _hBmpIndicator = NativeMethods.CreateDIBSection(_dcIndicatorScreen, ref bmi, 0, out IntPtr pBits, IntPtr.Zero, 0);
            
            if (_hBmpIndicator != IntPtr.Zero)
            {
                var dat = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                int b = Math.Abs(dat.Stride) * bmp.Height; unsafe { Buffer.MemoryCopy((void*)dat.Scan0, (void*)pBits, b, b); } bmp.UnlockBits(dat);
            }
            _hBmpIndicatorOld = NativeMethods.SelectObject(_dcIndicatorMem, _hBmpIndicator); _isIndicatorRendered = true;
        }

        private bool EvaluatePointerIsIBeam(ImeState.State state)
        {
            NativeMethods.CURSORINFO ci = new() { cbSize = Marshal.SizeOf<NativeMethods.CURSORINFO>() };
            if (!NativeMethods.GetCursorInfo(ref ci) || ci.hCursor == IntPtr.Zero || !_assetCache.TryGetValue(state, out var a)) return false;
            return ci.hCursor == (_activePointerMode == PointerMode.WinColor ? a.IBeamCompareHandleWin : a.IBeamCompareHandleNew);
        }

        private bool EvaluatePointerIsArrow()
        {
            if (_activePointerMode == PointerMode.WinDefault)
            {
                try
                {
                    var ci = new NativeMethods.CURSORINFO { cbSize = Marshal.SizeOf<NativeMethods.CURSORINFO>() };
                    if (NativeMethods.GetCursorInfo(ref ci) && NativeMethods.GetIconInfo(ci.hCursor, out var ii))
                    {
                        bool isArr = ii.xHotspot == 0 && ii.yHotspot == 0;
                        if (ii.hbmMask != IntPtr.Zero) NativeMethods.DeleteObject(ii.hbmMask);
                        if (ii.hbmColor != IntPtr.Zero) NativeMethods.DeleteObject(ii.hbmColor);
                        return isArr;
                    }
                } catch { } return false;
            }
            if (_previousImeState == (ImeState.State)(-1) || !_assetCache.TryGetValue(_previousImeState, out var a)) return false;
            var cInfo = new NativeMethods.CURSORINFO { cbSize = Marshal.SizeOf<NativeMethods.CURSORINFO>() };
            return NativeMethods.GetCursorInfo(ref cInfo) && cInfo.hCursor != IntPtr.Zero && cInfo.hCursor != (_activePointerMode == PointerMode.WinColor ? a.IBeamCompareHandleWin : a.IBeamCompareHandleNew);
        }


        private static IntPtr SearchFocusedInputHwnd(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return IntPtr.Zero;
            NativeMethods.GUITHREADINFO gti = new() { cbSize = Marshal.SizeOf<NativeMethods.GUITHREADINFO>() };
            if (NativeMethods.GetGUIThreadInfo(NativeMethods.GetWindowThreadProcessId(hWnd, out _), ref gti))
            {
                if (gti.hwndFocus != IntPtr.Zero) return gti.hwndFocus;
                if (gti.hwndActive != IntPtr.Zero) return gti.hwndActive;
            }
            return hWnd;
        }

        private unsafe bool IsTaskbarWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return false;
            Span<char> nm = stackalloc char[256];
            fixed (char* p = nm)
            {
                int len = NativeMethods.GetClassName(hWnd, p, 256);
                if (len > 0) { var s = nm.Slice(0, len); return s.IndexOf("Shell_TrayWnd") >= 0 || s.IndexOf("NotifyIconOverflowWindow") >= 0; }
                return false;
            }
        }

        private unsafe bool IsAppOrTrayWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || hWnd == this.Handle) return true;
            NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid); if (pid == s_currentProcessId) return true;
            Span<char> nm = stackalloc char[256];
            fixed (char* p = nm)
            {
                int len = NativeMethods.GetClassName(hWnd, p, 256);
                if (len > 0) { var s = nm.Slice(0, len); return s.IndexOf("Progman") >= 0 || s.IndexOf("WorkerW") >= 0 || s.IndexOf("#32768") >= 0; }
                return false;
            }
        }
    }
    #endregion
}