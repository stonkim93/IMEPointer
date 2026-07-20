// Program.cs - IMEPointer
#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Runtime.CompilerServices.DisableRuntimeMarshalling]

namespace IMEPointer
{
    #region [ 사용자 설정 영역 (AppConfig) ]
    internal static class AppConfig
    {
        // ---------------------------------------------------------
        // 1. 성능 및 기본 설정
        // ---------------------------------------------------------
        /// IME 상태 감지 주기 (단위: ms). 기본값 15ms. 
        /// ※ CPU 점유율이 높을 경우 30~50으로 상향 조정하세요.
        public const int PollingInterval = 30;
        public static readonly string[] IndicatorTargetApps = { "excel", "hwp" };
        public const float IndicatorSize = 8.0f;
        public const float IndicatorOffset = 20.0f;

        // ---------------------------------------------------------
        // 2. 트레이 메뉴 표시 옵션 (UI)
        // ---------------------------------------------------------
        // .csproj의 조건부 컴파일 상수(DefineConstants)와 연동하여 메뉴 표시 여부를 결정합니다.
        // 빌드 시 제외된 항목은 자동으로 트레이 메뉴에서도 숨김 처리됩니다.
        public static bool ShowPointerWinDefault = true;    // "WIN Default Pointer" 메뉴 표시 여부        
        public static bool ShowPointerWinColor = true;      // "WIN Color Pointer" 메뉴 표시 여부    
        public static bool ShowPointerNewColor = true;      // "NEW Color Pointer" 메뉴 표시 여부    
        public static bool ShowCapsHangul = true;           // [0] "한글CAPS 한글" 메뉴 표시 여부    

#if ENABLE_CAPS_ENGINEER
        public static bool ShowCapsEngineer = true;         // [1] "한글CAPS 공학용_특수기호" 메뉴 표시 여부    
#else
        public static bool ShowCapsEngineer = false;
#endif

#if ENABLE_CAPS_PALI
        public static bool ShowCapsPali = true;             // [2] "한글CAPS Pali_Sanskrit" 메뉴 표시 여부    
#else
        public static bool ShowCapsPali = false;
#endif

#if ENABLE_CAPS_JAPANESE1
        public static bool ShowCapsJapanese1 = true;        // [3] "한글CAPS 일본어1_조합형" 메뉴 표시 여부    
#else
        public static bool ShowCapsJapanese1 = false;
#endif

#if ENABLE_CAPS_JAPANESE2
        public static bool ShowCapsJapanese2 = true;        // [4] "한글CAPS 일본어2_3Layer" 메뉴 표시 여부    
#else
        public static bool ShowCapsJapanese2 = false;
#endif

#if ENABLE_KEYBOARD_LAYOUT
        public static bool ShowKeyboardlayoutMenu = true;       // "한글CAPS 키보드 배열창" 메뉴 표시 여부    
#else
        public static bool ShowKeyboardlayoutMenu = false;
#endif

        public static bool ShowTextOverlayMenu = true;      // "한글CAPS 입력문자 표시창" 메뉴 표시 여부    
        public static bool ShowSmallCircleMenu = true;      // "한글/엑셀 작은원 표시" 메뉴 표시 여부    

        // ---------------------------------------------------------
        // 3. 프로그램 시작 시 초기 모드 설정
        // ---------------------------------------------------------
        /// 기본 포인터 모드 (0: WinDefault, 1: WinColor, 2: NewColor)
        public static int DefaultPointerMode = 2;           // Pointer 기본모드 지정
        public static int DefaultCapsMode = 3;              // 한글CAPS 기본모드 지정
        
        public static bool DefaultShowKeyboardLayout = true;    // "한글CAPS 키보드 배열창" 옵션 활성화 여부
        public static bool DefaultShowTextOverlay = true;       // "한글CAPS 입력문자 표시창" 옵션 활성화 여부
        public static bool DefaultEnableMiniIndicator = true;   // "한글/엑셀 작은원 표시" 활성화 여부
        public static bool IsOverlayKey2Mode = false;           // 입력문자 표시창 'Key2' 상태를 관리하기 위한 전역 변수 추가

        public struct Theme
        {
            public Color PointerColor;   // 마우스 포인터 색상
            public Color TrayBgColor;    // 트레이 아이콘 배경색
            public Color TrayTextColor;  // 트레이 아이콘 글자색
            public string TrayText;      // 트레이 아이콘에 표시될 글자
            public string Description;   // 상태 설명 텍스트
            public Color IBeamColor;     // 텍스트 커서(I-Beam) 색상
        }

        /// 각 언어 및 입력 상태별 테마 매핑 딕셔너리입니다. 색상 커스터마이징 시 아래 Color 값을 수정하세요.
        public static readonly Dictionary<ImeState.State, Theme> Themes = new()
        {
            [ImeState.State.EnglishLower] = new Theme { PointerColor = Color.White, TrayBgColor = Color.Black, TrayTextColor = Color.White, TrayText = "e", Description = "영어 소문자 [e]", IBeamColor = Color.Black },
            [ImeState.State.EnglishUpper] = new Theme { PointerColor = Color.DeepSkyBlue, TrayBgColor = Color.Black, TrayTextColor = Color.DeepSkyBlue, TrayText = "E", Description = "영어 대문자 [E]", IBeamColor = Color.DeepSkyBlue },
            [ImeState.State.Hangul] = new Theme { PointerColor = Color.Red, TrayBgColor = Color.Red, TrayTextColor = Color.White, TrayText = "K", Description = "한글 (Caps Off) [K]", IBeamColor = Color.Red },
            [ImeState.State.PaliUS] = new Theme { PointerColor = Color.Orange, TrayBgColor = Color.Black, TrayTextColor = Color.Orange, TrayText = "p", Description = "Pali어 Unicode [p]", IBeamColor = Color.Orange },
            [ImeState.State.Engineer] = new Theme { PointerColor = Color.Orange, TrayBgColor = Color.Black, TrayTextColor = Color.Orange, TrayText = "S", Description = "한글CAPS 공학용 특수기호 [S]", IBeamColor = Color.Orange },
            [ImeState.State.PaliHangul] = new Theme { PointerColor = Color.Orange, TrayBgColor = Color.Black, TrayTextColor = Color.Orange, TrayText = "P", Description = "한글CAPS Pali어 [P]", IBeamColor = Color.Orange },
            [ImeState.State.JapaneseIME] = new Theme { PointerColor = Color.Lime, TrayBgColor = Color.Black, TrayTextColor = Color.Lime, TrayText = "j", Description = "Japanese IME [j]", IBeamColor = Color.Lime },
            [ImeState.State.JapaneseHangul1] = new Theme { PointerColor = Color.Lime, TrayBgColor = Color.Black, TrayTextColor = Color.Lime, TrayText = "J", Description = "한글CAPS 일본어1 [J]", IBeamColor = Color.Lime },
            [ImeState.State.JapaneseHangul2] = new Theme { PointerColor = Color.Lime, TrayBgColor = Color.Black, TrayTextColor = Color.Lime, TrayText = "J", Description = "한글CAPS 일본어2 [J]", IBeamColor = Color.Lime }
        };
    }
    #endregion

    internal static class UiText
    {
        public const string AppName = "IMEPointer";
        public const string AlreadyRunningMessage = "이미 실행 중입니다.";
        public const string FatalErrorPrefix = "치명적 오류:\n";
        public const string StatusChecking = "현재 상태: 확인 중...";
        public const string HangulCapsMode = "한글CAPS 모드";
        public const string EnglishLowerMode = "영어 소문자 모드";
        public const string Hiragana = "Hiragana";
        public const string Katakana = "Katakana";
        public const string ExitMenu = "종료(Exit)";

        public static string TrayTooltip(string description) => $"{AppName}: {description}";
        public static string StatusLabel(string description) => $"현재 상태: {description}";
        public static string LayerLabel(int layer) => $"Layer{layer}";
    }

    #region [ 진입점 (Main) ]
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // 중복 실행 방지 (IMEPali, IMEPointer)
            using Mutex mutexPali = new Mutex(true, "IMEPali_SingleInstance", out bool firstPali);
            using Mutex mutex = new Mutex(true, "IMEPointer_SingleInstance", out bool first);
            if (!first)
            {
                MessageBox.Show(UiText.AlreadyRunningMessage, UiText.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 예외 발생 및 종료 시 커서 원래대로 복구
            AppDomain.CurrentDomain.UnhandledException += (s, e) => MainForm.RestoreDefaults();
            AppDomain.CurrentDomain.ProcessExit += (s, e) => MainForm.RestoreDefaults();
            Application.ThreadException += (s, e) => MainForm.RestoreDefaults();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            try
            {
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{UiText.FatalErrorPrefix}{ex.Message}", UiText.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
    #endregion

    #region [ 커서 그래픽 처리 팩토리 ]
    internal static class WinColorPointerFactory
    {
        // [이번 수정: 부드러운 외곽선을 위해 알파 채널 절단 임계값 등 하드코딩 제거 및 보간 로직 전면 개편]
        public static IntPtr CreateColoredSystemPointer(uint ocrId, Color targetColor, int renderSize)
        {
            // 문제 원인: LoadImage에 크기를 강제 지정하고 GDI+로 스케일링할 때 Pre-multiplied Alpha가 손실되거나 
            // 픽셀이 이중으로 보간되어 외곽선이 울퉁불퉁해지는 현상(특히 화살표 꼬리)이 발생합니다.
            // 해결: 1. LoadImage 시 LR_SHARED를 해제하여 시스템 렌더러가 올바른 DPI의 고해상도 커서를 가져오도록 유도.
            //       2. GDI+ 스케일링을 제거하고 DrawIconEx의 네이티브 렌더링 엔진을 활용.
            IntPtr hPointer = NativeMethods.LoadImage(IntPtr.Zero, (IntPtr)ocrId, NativeMethods.IMAGE_CURSOR, renderSize, renderSize, 0);
            
            // 실패할 경우 공유된 기본 커서로 폴백(Fallback)
            if (hPointer == IntPtr.Zero)
                hPointer = NativeMethods.LoadImage(IntPtr.Zero, (IntPtr)ocrId, NativeMethods.IMAGE_CURSOR, 0, 0, NativeMethods.LR_SHARED | NativeMethods.LR_DEFAULTSIZE);

            if (hPointer == IntPtr.Zero) return IntPtr.Zero;

            int hotX = 0, hotY = 0;
            if (NativeMethods.GetIconInfo(hPointer, out NativeMethods.ICONINFO iiPointer))
            {
                hotX = iiPointer.xHotspot; 
                hotY = iiPointer.yHotspot;
                if (iiPointer.hbmColor != IntPtr.Zero) NativeMethods.DeleteObject(iiPointer.hbmColor);
                if (iiPointer.hbmMask != IntPtr.Zero) NativeMethods.DeleteObject(iiPointer.hbmMask);
            }

            using Bitmap? rendered = RenderPointerToArgbBitmap(hPointer, renderSize, out int actualWidth, out int actualHeight);

            if (rendered == null) return IntPtr.Zero;

            RecolorCursorStraight(rendered, targetColor, ocrId);

            Bitmap finalBitmap = rendered;
            Bitmap? outlined = null;

            if (ocrId == NativeMethods.OCR_IBEAM)
            {
                int brightness = (targetColor.R * 299 + targetColor.G * 587 + targetColor.B * 114) / 1000;
                Color outlineColor = brightness > 128 ? Color.Black : Color.White;
                outlined = AddSmoothOutline(rendered, outlineColor);
                finalBitmap = outlined;
            }

            // 스케일된 크기에 맞춰 HotSpot 좌표 보정
            float scaleX = (float)renderSize / actualWidth;
            float scaleY = (float)renderSize / actualHeight;
            int scaledHotX = (int)Math.Round(hotX * scaleX);
            int scaledHotY = (int)Math.Round(hotY * scaleY);

            IntPtr ptr = BitmapToPointer(finalBitmap, scaledHotX, scaledHotY);

            outlined?.Dispose();
            return ptr;
        }

        private static unsafe Bitmap AddSmoothOutline(Bitmap src, Color outlineColor)
        {
            // [이번 수정: IBeam 커서 외곽선의 계단 현상을 제거하기 위해 Soft Alpha 혼합 방식을 적용]
            int width = src.Width, height = src.Height;
            Bitmap result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var srcData = src.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var dstData = result.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            byte* pSrc = (byte*)srcData.Scan0;
            byte* pDst = (byte*)dstData.Scan0;
            int stride = srcData.Stride;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * stride + x * 4;
                    byte srcA = pSrc[idx + 3];

                    if (srcA == 255)
                    {
                        pDst[idx] = pSrc[idx]; pDst[idx + 1] = pSrc[idx + 1];
                        pDst[idx + 2] = pSrc[idx + 2]; pDst[idx + 3] = 255;
                    }
                    else
                    {
                        int maxNeighborAlpha = 0;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                int ny = y + dy, nx = x + dx;
                                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                                {
                                    int nA = pSrc[ny * stride + nx * 4 + 3];
                                    if (nA > maxNeighborAlpha) maxNeighborAlpha = nA;
                                }
                            }
                        }

                        if (srcA > 0)
                        {
                            float alphaRatio = srcA / 255.0f;
                            pDst[idx] = (byte)(pSrc[idx] * alphaRatio + outlineColor.B * (1 - alphaRatio));
                            pDst[idx + 1] = (byte)(pSrc[idx + 1] * alphaRatio + outlineColor.G * (1 - alphaRatio));
                            pDst[idx + 2] = (byte)(pSrc[idx + 2] * alphaRatio + outlineColor.R * (1 - alphaRatio));
                            pDst[idx + 3] = (byte)Math.Max(srcA, maxNeighborAlpha > 0 ? 150 : 0);
                        }
                        else if (maxNeighborAlpha > 0)
                        {
                            pDst[idx] = outlineColor.B; pDst[idx + 1] = outlineColor.G; pDst[idx + 2] = outlineColor.R;
                            pDst[idx + 3] = (byte)(maxNeighborAlpha * 0.6f);
                        }
                        else
                        {
                            pDst[idx] = pDst[idx + 1] = pDst[idx + 2] = pDst[idx + 3] = 0;
                        }
                    }
                }
            }
            src.UnlockBits(srcData);
            result.UnlockBits(dstData);
            return result;
        }

        private static unsafe Bitmap? RenderPointerToArgbBitmap(IntPtr hPointer, int targetSize, out int actualWidth, out int actualHeight)
        {
            actualWidth = targetSize;
            actualHeight = targetSize;
            
            if (NativeMethods.GetIconInfo(hPointer, out NativeMethods.ICONINFO ii))
            {
                IntPtr hBmp = ii.hbmColor != IntPtr.Zero ? ii.hbmColor : ii.hbmMask;
                if (hBmp != IntPtr.Zero)
                {
                    using (Image img = Image.FromHbitmap(hBmp))
                    {
                        actualWidth = img.Width;
                        actualHeight = ii.hbmColor != IntPtr.Zero ? img.Height : img.Height / 2;
                    }
                }
                if (ii.hbmColor != IntPtr.Zero) NativeMethods.DeleteObject(ii.hbmColor);
                if (ii.hbmMask != IntPtr.Zero) NativeMethods.DeleteObject(ii.hbmMask);
            }

            NativeMethods.BITMAPINFO bmi = new() { biSize = sizeof(NativeMethods.BITMAPINFO), biWidth = targetSize, biHeight = -targetSize, biPlanes = 1, biBitCount = 32, biCompression = 0 };
            IntPtr hdcScreen = NativeMethods.GetDC(IntPtr.Zero);
            IntPtr hdcMem = NativeMethods.CreateCompatibleDC(hdcScreen);
            IntPtr hDib = NativeMethods.CreateDIBSection(hdcMem, ref bmi, 0, out IntPtr pBits, IntPtr.Zero, 0);

            if (hDib == IntPtr.Zero) { NativeMethods.DeleteDC(hdcMem); NativeMethods.ReleaseDC(IntPtr.Zero, hdcScreen); return null; }

            IntPtr hOld = NativeMethods.SelectObject(hdcMem, hDib);
            int byteCount = targetSize * targetSize * 4;
            new Span<byte>((void*)pBits, byteCount).Clear();

            // [이번 수정: DrawIconEx의 네이티브 크기 조절 기능을 사용하여 깨끗한 확대 수행]
            const uint DI_NORMAL = 0x0003;
            NativeMethods.DrawIconEx(hdcMem, 0, 0, hPointer, targetSize, targetSize, 0, IntPtr.Zero, DI_NORMAL);

            Bitmap bmp = new Bitmap(targetSize, targetSize, PixelFormat.Format32bppArgb);
            var bmpData = bmp.LockBits(new Rectangle(0, 0, targetSize, targetSize), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            
            byte* src = (byte*)pBits;
            byte* dst = (byte*)bmpData.Scan0;
            
            long alphaSum = 0;
            for (int i = 3; i < byteCount; i += 4) alphaSum += src[i];

            if (alphaSum == 0)
            {
                for (int i = 0; i < byteCount; i += 4)
                {
                    byte b = src[i], g = src[i+1], r = src[i+2];
                    if (r > 0 || g > 0 || b > 0)
                    {
                        dst[i] = b; dst[i+1] = g; dst[i+2] = r; dst[i+3] = 255;
                    }
                    else
                    {
                        dst[i] = dst[i+1] = dst[i+2] = dst[i+3] = 0;
                    }
                }
            }
            else
            {
                // [이번 수정: DIB Section의 Pre-multiplied ARGB를 수동으로 정확히 역연산하여 복구]
                for (int i = 0; i < byteCount; i += 4)
                {
                    byte b = src[i], g = src[i+1], r = src[i+2], a = src[i+3];
                    if (a == 0)
                    {
                        dst[i] = dst[i+1] = dst[i+2] = dst[i+3] = 0;
                    }
                    else if (a == 255)
                    {
                        dst[i] = b; dst[i+1] = g; dst[i+2] = r; dst[i+3] = 255;
                    }
                    else
                    {
                        dst[i] = (byte)Math.Min(255, (b * 255) / a);
                        dst[i+1] = (byte)Math.Min(255, (g * 255) / a);
                        dst[i+2] = (byte)Math.Min(255, (r * 255) / a);
                        dst[i+3] = a;
                    }
                }
            }
            
            bmp.UnlockBits(bmpData);

            NativeMethods.SelectObject(hdcMem, hOld);
            NativeMethods.DeleteObject(hDib); NativeMethods.DeleteDC(hdcMem); NativeMethods.ReleaseDC(IntPtr.Zero, hdcScreen);

            return bmp;
        }

        private static unsafe void RecolorCursorStraight(Bitmap bmp, Color targetColor, uint ocrId)
        {
            // [이번 수정: 하드 임계값(Threshold)을 제거하고, 원본 픽셀의 명도를 기반으로 색상을 보간하여 Anti-Aliasing 계단 현상을 완벽하게 방지]
            var bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            byte* ptr = (byte*)bmpData.Scan0;
            int len = bmp.Width * bmp.Height * 4;

            for (int i = 0; i < len; i += 4)
            {
                byte a = ptr[i + 3];
                if (a == 0) continue;

                byte b = ptr[i], g = ptr[i + 1], r = ptr[i + 2];
                
                if (ocrId == NativeMethods.OCR_NORMAL)
                {
                    // 일반 화살표: 흰 바탕 + 검은 테두리의 명도 비율을 이용하여 대상 색상을 부드럽게 입힘
                    float intensity = (r * 0.299f + g * 0.587f + b * 0.114f) / 255.0f;
                    ptr[i] = (byte)(b + (targetColor.B - b) * intensity);
                    ptr[i + 1] = (byte)(g + (targetColor.G - g) * intensity);
                    ptr[i + 2] = (byte)(r + (targetColor.R - r) * intensity);
                }
                else
                {
                    // IBeam 및 기타 커서는 통째로 색상 적용
                    ptr[i] = targetColor.B;
                    ptr[i + 1] = targetColor.G;
                    ptr[i + 2] = targetColor.R;
                }
            }
            bmp.UnlockBits(bmpData);
        }

        private static unsafe IntPtr BitmapToPointer(Bitmap bmp, int hotX, int hotY)
        {
            IntPtr hBmpColor = IntPtr.Zero, hBmpMask = IntPtr.Zero;
            IntPtr hdcScreen = NativeMethods.GetDC(IntPtr.Zero);
            try
            {
                NativeMethods.BITMAPINFO bmi = new() { biSize = sizeof(NativeMethods.BITMAPINFO), biWidth = bmp.Width, biHeight = -bmp.Height, biPlanes = 1, biBitCount = 32, biCompression = 0 };
                hBmpColor = NativeMethods.CreateDIBSection(hdcScreen, ref bmi, 0, out IntPtr pBits, IntPtr.Zero, 0);
                
                if (hBmpColor != IntPtr.Zero)
                {
                    var bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    byte* pSrc = (byte*)bmpData.Scan0;
                    byte* pDst = (byte*)pBits;
                    int bytes = Math.Abs(bmpData.Stride) * bmp.Height;

                    // [이번 수정: CreateIconIndirect에 사용되는 hbmColor는 반드시 'Premultiplied Alpha'여야 합니다.
                    // 단순 메모리 복사(MemoryCopy) 대신, 픽셀마다 알파값을 명시적으로 곱해 외곽선 계단 현상을 제거합니다.]
                    for (int i = 0; i < bytes; i += 4)
                    {
                        byte a = pSrc[i + 3];
                        if (a == 0)
                        {
                            pDst[i] = pDst[i + 1] = pDst[i + 2] = pDst[i + 3] = 0;
                        }
                        else if (a == 255)
                        {
                            pDst[i] = pSrc[i];
                            pDst[i + 1] = pSrc[i + 1];
                            pDst[i + 2] = pSrc[i + 2];
                            pDst[i + 3] = 255;
                        }
                        else
                        {
                            pDst[i] = (byte)((pSrc[i] * a) / 255);
                            pDst[i + 1] = (byte)((pSrc[i + 1] * a) / 255);
                            pDst[i + 2] = (byte)((pSrc[i + 2] * a) / 255);
                            pDst[i + 3] = a;
                        }
                    }
                    bmp.UnlockBits(bmpData);
                }

                using Bitmap maskBmp = new(bmp.Width, bmp.Height, PixelFormat.Format1bppIndexed);
                hBmpMask = maskBmp.GetHbitmap(); 
                
                NativeMethods.ICONINFO ii = new() { fIcon = 0, xHotspot = hotX, yHotspot = hotY, hbmMask = hBmpMask, hbmColor = hBmpColor };
                return NativeMethods.CreateIconIndirect(ref ii);
            }
            catch { return IntPtr.Zero; }
            finally
            {
                if (hBmpColor != IntPtr.Zero) NativeMethods.DeleteObject(hBmpColor);
                if (hBmpMask != IntPtr.Zero) NativeMethods.DeleteObject(hBmpMask);
                if (hdcScreen != IntPtr.Zero) NativeMethods.ReleaseDC(IntPtr.Zero, hdcScreen);
            }
        }
    }
    #endregion

    #region [ 자판 배열창 폼 ]
    public class KeyboardLayoutForm : Form
    {
        private readonly PictureBox _pictureBox;
        public event EventHandler? OnLayoutDoubleClicked;
        public event EventHandler? OnClosedByUser;
        private string _currentImageName = "";
        private Size _currentImageSize = new Size(600, 200);

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x00020000;   // WS_MINIMIZEBOX
                cp.Style |= 0x00080000;   // WS_SYSMENU
                cp.ExStyle |= 0x00040000; // WS_EX_APPWINDOW
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE (입력 포커스 유지)
                return cp;
            }
        }

        protected override bool ShowWithoutActivation => true;

        public KeyboardLayoutForm()
        {
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.ShowInTaskbar = true;
            this.TopMost = true; 
            this.Text = "IMEPointer 자판 배열창";
            
            int screenWidth = Screen.PrimaryScreen?.WorkingArea.Width ?? 800;
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(Math.Max(0, (screenWidth - this.Width) / 2), 50);

            // images 폴더 경로 반영 및 아이콘 로드
            try 
            { 
                var assembly = typeof(Program).Assembly;
                // 아이콘 파일이 images 폴더에 있다면 아래와 같이 ".images." 를 추가합니다.
                // 만약 파일명이 다르면 "IMEPointer.images.파일명.ico" 로 수정하세요.
                using (Stream? stream = assembly.GetManifestResourceStream("IMEPointer.images.IMEPointer.ico"))
                {
                    if (stream != null)
                    {
                        this.Icon = new Icon(stream);
                    }
                }
            } 
            catch { }

            _pictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.White
            };

            _pictureBox.DoubleClick += (s, e) => OnLayoutDoubleClicked?.Invoke(this, EventArgs.Empty);
            this.Controls.Add(_pictureBox);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (this.WindowState == FormWindowState.Normal)
            {
                if (this.ClientSize != _currentImageSize && _currentImageSize.Width > 0 && _currentImageSize.Height > 0)
                {
                    this.ClientSize = _currentImageSize;
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                OnClosedByUser?.Invoke(this, EventArgs.Empty);
            }
            base.OnFormClosing(e);
        }

        public void UpdateImage(string imageName)
        {
            if (_currentImageName == imageName) return;
            _currentImageName = imageName;
            this.Text = imageName;

            try
            {
                var assembly = typeof(Program).Assembly;
                // images 폴더 내의 리소스를 찾도록 .images. 문자열을 추가
                string resourceName = $"IMEPointer.images.{imageName}";
                using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        Image? oldImg = _pictureBox.Image;
                        Image newImg = Image.FromStream(stream);
                        _pictureBox.Image = newImg;
                        _currentImageSize = newImg.Size;
                        if (this.WindowState == FormWindowState.Normal)
                        {
                            this.ClientSize = _currentImageSize;
                        }
                        oldImg?.Dispose();
                    }
                    else
                    {
                        Image? oldImg = _pictureBox.Image;
                        _pictureBox.Image = null;
                        oldImg?.Dispose();
                    }
                }
            }
            catch
            {
                Image? oldImg = _pictureBox.Image;
                _pictureBox.Image = null;
                oldImg?.Dispose();
            }
        }
    }
    #endregion

    #region [ 오버레이 표시 폼 (TextOverlayForm) ]
    public class TextOverlayForm : Form
    {
        private readonly System.Windows.Forms.Timer _hideTimer;
        private string _text = "";
        private float _fontSize = 22f;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST
                return cp;
            }
        }
        protected override bool ShowWithoutActivation => true;

        public TextOverlayForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.BackColor = Color.Black;
            this.ForeColor = Color.White;
            this.TopMost = true;
            this.ShowInTaskbar = false;

            _hideTimer = new System.Windows.Forms.Timer { Interval = 1500 };
            _hideTimer.Tick += (s, e) => this.Hide();
            
            this.Paint += TextOverlayForm_Paint;
        }

        public void ShowOverlay(string text, bool useTimer, float fontSize, int width, int height, int x, int y)
        {
            _text = text;
            _fontSize = fontSize;
            
            this.Size = new Size(width, height);
            this.Location = new Point(x, y);
            
            if (useTimer)
            {
                _hideTimer.Stop();
                _hideTimer.Start();
            }
            else
            {
                _hideTimer.Stop();
            }
            
            if (!this.Visible) 
            {
                this.Show(); 
            }
            this.Invalidate();
        }

        private void TextOverlayForm_Paint(object? sender, PaintEventArgs e)
        {
            // 배율(DPI) 중복 스케일링을 방지하고 정확한 크기를 제어하기 위해 단위를 Pixel로 명시
            using Font f = new Font("Malgun Gothic", _fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            TextRenderer.DrawText(e.Graphics, _text, f, this.ClientRectangle, Color.White, Color.Black, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        
        public void Clear()
        {
            _hideTimer.Stop();
            this.Hide();
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
        private const int HiddenLayeredWindowLocation = -10000;
        private const int WindowPosChangedMessage = 0x001A;
        private const int TrayContextMenuForegroundDelayRetryMs = 60;
        private const int RebuildRetryAfterWindowPosChangedMs = 800;
        private const int RebuildRetryAfterScaleChangeMs = 1500;
        private const int DisplaySettingsChangedDelayMs = 400;
        private const int UserPreferenceChangedDelayMs = 600;
        private const int OverlayDefaultDurationMs = 1500;
        private const float OverlayDefaultFontSize = 29f;   // 폰트 단위를 Pixel로 취급하기 위해 기존 22pt(약 29px) 기준값으로 조정
        private const int OverlayDefaultHeight = 52;
        private const int OverlayDefaultCharWidth = 30;
        private const int OverlayDefaultPaddingWidth = 24;
        private const int OverlayDefaultYOffset = 40;
        private const float PointerDiagonalFactor = 0.7071f;
        private const float IBeamIndicatorYOffsetFactor = 0.65f;
        private const float IndicatorBottomMargin = 4f;
        private const int TrayIconSize = 32;
        private const float TrayLowercaseFontSize = 31F;
        private const float TrayUppercaseFontSize = 32F;
        private static readonly RectangleF TrayIconTextRectLower = new RectangleF(-2.0f, -5.0f, 36f, 36f);
        private static readonly RectangleF TrayIconTextRectUpper = new RectangleF(-2.0f, -3.5f, 36f, 36f);

        private readonly struct ActiveInputModeContext
        {
            public readonly bool IsPaliModeActive;
            public readonly bool IsEngineerModeActive;
            public readonly bool IsJapanese1ModeActive;
            public readonly bool IsJapanese2ModeActive;
            public readonly IKeyProcessor? ActiveProcessor;

            public ActiveInputModeContext(
                bool isPaliModeActive,
                bool isEngineerModeActive,
                bool isJapanese1ModeActive,
                bool isJapanese2ModeActive,
                IKeyProcessor? activeProcessor)
            {
                IsPaliModeActive = isPaliModeActive;
                IsEngineerModeActive = isEngineerModeActive;
                IsJapanese1ModeActive = isJapanese1ModeActive;
                IsJapanese2ModeActive = isJapanese2ModeActive;
                ActiveProcessor = activeProcessor;
            }
        }

        private readonly struct CapsModeStateMapping
        {
            public readonly CapsMode Mode;
            public readonly ImeState.State ActiveState;
            public readonly IKeyProcessor Processor;

            public CapsModeStateMapping(CapsMode mode, ImeState.State activeState, IKeyProcessor processor)
            {
                Mode = mode;
                ActiveState = activeState;
                Processor = processor;
            }
        }

        private static class TrayIconRenderer
        {
            public static Icon Create(string text, Color bgColor, Color textColor)
            {
                using Bitmap bmp = new(TrayIconSize, TrayIconSize);
                using Graphics g = Graphics.FromImage(bmp);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using SolidBrush bgBrush = new(bgColor);
                g.FillRectangle(bgBrush, 0, 0, TrayIconSize, TrayIconSize);

                bool startsLower = !string.IsNullOrEmpty(text) && char.IsLower(text[0]);
                using Font font = new(startsLower ? "Segoe Print" : "Segoe UI Black", startsLower ? TrayLowercaseFontSize : TrayUppercaseFontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                using SolidBrush textBrush = new(textColor);
                using StringFormat sf = new()
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    FormatFlags = StringFormatFlags.NoWrap
                };

                RectangleF layoutRect = startsLower ? TrayIconTextRectLower : TrayIconTextRectUpper;

                if (startsLower)
                {
                    DrawBoldLowercase(g, text, font, textBrush, layoutRect, sf);
                }
                else
                {
                    g.DrawString(text, font, textBrush, layoutRect, sf);
                }

                IntPtr hIcon = bmp.GetHicon();
                Icon icon = (Icon)Icon.FromHandle(hIcon).Clone();
                NativeMethods.DestroyIcon(hIcon);
                return icon;
            }

            private static void DrawBoldLowercase(Graphics g, string text, Font font, Brush textBrush, RectangleF layoutRect, StringFormat sf)
            {
                g.DrawString(text, font, textBrush, new RectangleF(layoutRect.X, layoutRect.Y, layoutRect.Width, layoutRect.Height), sf);
                g.DrawString(text, font, textBrush, new RectangleF(layoutRect.X + 1.0f, layoutRect.Y, layoutRect.Width, layoutRect.Height), sf);
                g.DrawString(text, font, textBrush, new RectangleF(layoutRect.X, layoutRect.Y + 1.0f, layoutRect.Width, layoutRect.Height), sf);
                g.DrawString(text, font, textBrush, new RectangleF(layoutRect.X + 1.0f, layoutRect.Y + 1.0f, layoutRect.Width, layoutRect.Height), sf);
                g.DrawString(text, font, textBrush, new RectangleF(layoutRect.X + 0.5f, layoutRect.Y + 0.5f, layoutRect.Width, layoutRect.Height), sf);
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

        private readonly Dictionary<ImeState.State, StateAssets> _assetCache = new();
        private readonly System.Windows.Forms.Timer _stateTimer;
        private readonly NotifyIcon _trayIcon;
        private readonly ContextMenuStrip _contextMenu;
        private readonly ToolStripMenuItem _statusMenuItem;
        private bool _showTextOverlay = AppConfig.DefaultShowTextOverlay; 

        internal enum PointerMode { WinDefault = 0, WinColor = 1, NewColor = 2 }
        internal enum CapsMode { WinDefault = 0, Engineer = 1, Pali = 2, Japanese1 = 3, Japanese2 = 4 }

        private PointerMode _pointerMode = (PointerMode)AppConfig.DefaultPointerMode;
        private CapsMode _capsMode = (CapsMode)AppConfig.DefaultCapsMode;
        private bool _enableMiniIndicator = AppConfig.DefaultEnableMiniIndicator;
        private bool _showKeyboardLayoutOverlay = AppConfig.DefaultShowKeyboardLayout;

        private ToolStripMenuItem _menuPointerWinDefault = null!;
        private ToolStripMenuItem _menuPointerWinColor = null!;
        private ToolStripMenuItem _menuPointerNewColor = null!;
        private ToolStripMenuItem _menuCapsWinDefault = null!;
        private ToolStripMenuItem _menuCapsEngineer = null!;
        private ToolStripMenuItem _menuCapsPali = null!;
        private ToolStripMenuItem _menuCapsJapanese1 = null!;
        private ToolStripMenuItem _menuCapsJapanese2 = null!;
        private ToolStripMenuItem _toggleIndicatorMenuItem = null!;
        private ToolStripMenuItem _toggleKeyboardLayoutMenuItem = null!;
        private ToolStripMenuItem _toggleTextOverlayMenuItem = null!; 

        private bool _showMiniIndicator = false;
        private bool _visualShiftInversion = false; 
        private bool _lastSyncHangulState = false;
        private KeyboardLayoutForm? _keyboardLayoutForm;
        private TextOverlayForm? _textOverlayForm; 
        private Point _keyboardLayoutLastLocation = Point.Empty;

        private ImeState.State _lastState = (ImeState.State)(-1);
        private Color _currentDotColor = Color.White;
        private Color _lastIndicatorColor = Color.Empty;
        private IntPtr _lastForegroundHwnd = IntPtr.Zero;
        private IntPtr _currentHwnd = IntPtr.Zero;
        private IntPtr _lastPolledHFore = IntPtr.Zero; // 포커스 변경 감지를 위한 변수 추가

        private IntPtr _indicatorScreenDc = IntPtr.Zero;
        private IntPtr _indicatorMemDc = IntPtr.Zero;
        private IntPtr _indicatorHBitmap = IntPtr.Zero;
        private IntPtr _indicatorOldBitmap = IntPtr.Zero;
        private bool _isIndicatorBaked = false;
        private bool _isPointerInsideCell = false;
        private int _lastIndicatorX = int.MinValue;
        private int _lastIndicatorY = int.MinValue;

        private float _currentScaleRatio = 1.0f;
        private float _physIndicatorOffsetX = 0f;
        private int _indicatorCanvasSize = 16;
        private int _pointerPhysicalSize = 32;

        private IntPtr _lastAppliedArrowHandle = IntPtr.Zero;
        private static readonly unsafe int s_bmiSize = sizeof(NativeMethods.BITMAPINFO);
        // 트레이 메뉴 클릭 시 포커스 강탈로 인한 상태 초기화 현상을 막기 위해 현재 프로세스 ID 캐싱
        private static readonly uint _currentProcessId = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000080 | 0x00000020 | 0x00080000 | 0x08000000 | 0x00000008;
                return cp;
            }
        }

        public MainForm()
        {
            Instance = this;
            this.Size = new Size(HiddenFormSize, HiddenFormSize);
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(HiddenFormLocation, HiddenFormLocation);

            _contextMenu = new ContextMenuStrip();
            _statusMenuItem = new ToolStripMenuItem(UiText.StatusChecking) { Enabled = false };

            InitializeTrayMenu();

            _trayIcon = new NotifyIcon { Text = UiText.AppName, ContextMenuStrip = _contextMenu, Visible = true };
            _trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) { NativeMethods.SetForegroundWindow(this.Handle); _contextMenu.Show(Cursor.Position); }
            };

            GlobalInputHook.Install();

            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

            RebuildStateAssets();

            _stateTimer = new System.Windows.Forms.Timer { Interval = AppConfig.PollingInterval };
            _stateTimer.Tick += StateTimer_Tick;
        }

        // 트레이 메뉴 생성 로직 통합 (헬퍼 메서드를 이용한 중복 제거)
        private void InitializeTrayMenu()
        {
            _contextMenu.Items.Add(_statusMenuItem);
            _contextMenu.Items.Add(new ToolStripSeparator());

            // 포인터 메뉴
            _menuPointerWinDefault = AddConfigMenu("WIN Default Pointer", AppConfig.ShowPointerWinDefault, (s, e) => SetPointerMode(PointerMode.WinDefault));
            _menuPointerWinColor = AddConfigMenu("WIN Color Pointer", AppConfig.ShowPointerWinColor, (s, e) => SetPointerMode(PointerMode.WinColor));
            _menuPointerNewColor = AddConfigMenu("NEW Color Pointer", AppConfig.ShowPointerNewColor, (s, e) => SetPointerMode(PointerMode.NewColor));
            AddSeparatorIf(AppConfig.ShowPointerWinDefault || AppConfig.ShowPointerWinColor || AppConfig.ShowPointerNewColor);

            // 캡스 모드 메뉴
            _menuCapsWinDefault = AddConfigMenu("한글CAPS 한글_Default", AppConfig.ShowCapsHangul, (s, e) => SetCapsMode(CapsMode.WinDefault));
            _menuCapsEngineer = AddConfigMenu("한글CAPS 공학용_특수기호", AppConfig.ShowCapsEngineer, (s, e) => SetCapsMode(CapsMode.Engineer));
            _menuCapsPali = AddConfigMenu("한글CAPS Pali_Sanskrit", AppConfig.ShowCapsPali, (s, e) => SetCapsMode(CapsMode.Pali));
            _menuCapsJapanese1 = AddConfigMenu("한글CAPS 일본어1_조합형", AppConfig.ShowCapsJapanese1, (s, e) => SetCapsMode(CapsMode.Japanese1));
            _menuCapsJapanese2 = AddConfigMenu("한글CAPS 일본어2_3Layer", AppConfig.ShowCapsJapanese2, (s, e) => SetCapsMode(CapsMode.Japanese2));
            AddSeparatorIf(AppConfig.ShowCapsHangul || AppConfig.ShowCapsEngineer || AppConfig.ShowCapsPali || AppConfig.ShowCapsJapanese1 || AppConfig.ShowCapsJapanese2);

            // 옵션 메뉴
            _toggleKeyboardLayoutMenuItem = AddConfigMenu("한글CAPS 키보드 배열창", AppConfig.ShowKeyboardlayoutMenu, (s, e) =>
            {
                _showKeyboardLayoutOverlay = _toggleKeyboardLayoutMenuItem.Checked;
                if (!_showKeyboardLayoutOverlay) CloseAllLayoutForms();
                else RefreshKeyboardLayoutOverlay();
            });
            _toggleKeyboardLayoutMenuItem.CheckOnClick = true;
            _toggleKeyboardLayoutMenuItem.Checked = _showKeyboardLayoutOverlay;

            _toggleTextOverlayMenuItem = AddConfigMenu("한글CAPS 입력문자 표시창", AppConfig.ShowTextOverlayMenu, (s, e) =>
            {
                _showTextOverlay = _toggleTextOverlayMenuItem.Checked;
                if (!_showTextOverlay) _textOverlayForm?.Clear();
            });
            _toggleTextOverlayMenuItem.CheckOnClick = true;
            _toggleTextOverlayMenuItem.Checked = _showTextOverlay;

            _toggleIndicatorMenuItem = AddConfigMenu("한글/엑셀 작은원 표시", AppConfig.ShowSmallCircleMenu, (s, e) =>
            {
                _enableMiniIndicator = _toggleIndicatorMenuItem.Checked;
                if (!_enableMiniIndicator) UpdateLayeredIndicator(Color.Transparent, HiddenLayeredWindowLocation, HiddenLayeredWindowLocation);
            });
            _toggleIndicatorMenuItem.CheckOnClick = true;
            _toggleIndicatorMenuItem.Checked = _enableMiniIndicator;

            AddSeparatorIf(AppConfig.ShowKeyboardlayoutMenu || AppConfig.ShowTextOverlayMenu || AppConfig.ShowSmallCircleMenu);
            _contextMenu.Items.Add(new ToolStripMenuItem(UiText.ExitMenu, null, (s, e) => this.Close()));

            RefreshPointerModeCheck();
            RefreshCapsModeCheck();
        }

        private ToolStripMenuItem AddConfigMenu(string text, bool show, EventHandler onClick)
        {
            var item = new ToolStripMenuItem(text, null, onClick);
            if (show) _contextMenu.Items.Add(item);
            return item;
        }

        private void AddSeparatorIf(bool condition)
        {
            if (condition) _contextMenu.Items.Add(new ToolStripSeparator());
        }

        public void RequestLayoutRefresh() => this.BeginInvoke(new Action(RefreshKeyboardLayoutOverlay));

        private void SetPointerMode(PointerMode mode)
        {
            _pointerMode = mode;
            RefreshPointerModeCheck();
            _lastState = (ImeState.State)(-1);
            if (mode == PointerMode.WinColor)
            {
                _stateTimer.Stop(); RebuildStateAssets(); _stateTimer.Start();
            }
        }

        private void RefreshPointerModeCheck()
        {
            if (_menuPointerWinDefault != null) _menuPointerWinDefault.Checked = (_pointerMode == PointerMode.WinDefault);
            if (_menuPointerWinColor != null) _menuPointerWinColor.Checked = (_pointerMode == PointerMode.WinColor);
            if (_menuPointerNewColor != null) _menuPointerNewColor.Checked = (_pointerMode == PointerMode.NewColor);
        }

        // 선택된 한글CAPS 입력모드를 대상 창에 적용하는 헬퍼 메서드
        private void ApplySelectedCapsMode(IntPtr targetHwnd)
        {
            if (targetHwnd == IntPtr.Zero) return;

            ImeState.SetHangulState(targetHwnd, true);

            bool capsOn = (NativeMethods.GetKeyState(NativeMethods.VK_CAPITAL) & 0x0001) != 0;
            if (!capsOn)
            {
                NativeMethods.SimulateCapsLock();
            }
        }

        private void ApplySelectedCapsModeWithVerification(IntPtr targetHwnd, int retryCount = 2)
        {
            if (targetHwnd == IntPtr.Zero) return;

            ApplySelectedCapsMode(targetHwnd);

            if (ImeState.IsHangulModeSystemWide(targetHwnd)) return;
            if (retryCount <= 0) return;

            IntPtr retryRootHwnd = LastValidHwnd != IntPtr.Zero ? LastValidHwnd : targetHwnd;

            Task.Delay(TrayContextMenuForegroundDelayRetryMs).ContinueWith(_ =>
                this.BeginInvoke(new Action(() =>
                {
                    IntPtr retryTarget = GetFocusedInputHwnd(retryRootHwnd);
                    if (retryTarget == IntPtr.Zero) retryTarget = retryRootHwnd;

                    // IsTaskbarWindow, IsTrayOrAppWindow 호출
                    if (retryTarget != IntPtr.Zero && !IsTaskbarWindow(retryTarget) && !IsTrayOrAppWindow(retryTarget))
                    {
                        NativeMethods.SetForegroundWindow(retryTarget);
                    }

                    ApplySelectedCapsModeWithVerification(retryTarget, retryCount - 1);
                })));
        }

        private void SetCapsMode(CapsMode mode)
        {
            _capsMode = mode;
            RefreshCapsModeCheck();
            _lastState = (ImeState.State)(-1);
            RefreshKeyboardLayoutOverlay();

            IntPtr taskbarHwnd = NativeMethods.GetForegroundWindow();
            // IsTaskbarWindow, IsTrayOrAppWindow 호출
            if (taskbarHwnd != IntPtr.Zero && (IsTaskbarWindow(taskbarHwnd) || IsTrayOrAppWindow(taskbarHwnd)))
            {
                ApplySelectedCapsModeWithVerification(taskbarHwnd, 1);
            }

            IntPtr targetHwnd = LastValidFocusHwnd != IntPtr.Zero
                ? LastValidFocusHwnd
                : (LastValidHwnd != IntPtr.Zero ? LastValidHwnd : taskbarHwnd);

            if (targetHwnd != IntPtr.Zero)
            {
                // IsTaskbarWindow, IsTrayOrAppWindow 호출
                if (!IsTaskbarWindow(targetHwnd) && !IsTrayOrAppWindow(targetHwnd))
                {
                    NativeMethods.SetForegroundWindow(targetHwnd);
                }
                ApplySelectedCapsModeWithVerification(targetHwnd);
            }
        }

        private void RefreshCapsModeCheck()
        {
            if (_menuCapsWinDefault != null) _menuCapsWinDefault.Checked = (_capsMode == CapsMode.WinDefault);
            if (_menuCapsEngineer != null) _menuCapsEngineer.Checked = (_capsMode == CapsMode.Engineer);
            if (_menuCapsPali != null) _menuCapsPali.Checked = (_capsMode == CapsMode.Pali);
            if (_menuCapsJapanese1 != null) _menuCapsJapanese1.Checked = (_capsMode == CapsMode.Japanese1);
            if (_menuCapsJapanese2 != null) _menuCapsJapanese2.Checked = (_capsMode == CapsMode.Japanese2);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WindowPosChangedMessage) Task.Delay(200).ContinueWith(_ => this.BeginInvoke(new Action(() => RebakeWithRetry(RebuildRetryAfterWindowPosChangedMs))));
            base.WndProc(ref m);
        }

        private void RebakeWithRetry(int retryDelayMs)
        {
            _stateTimer.Stop(); RebuildStateAssets(); _stateTimer.Start();

            int currentPhysSize = _pointerPhysicalSize;
            if (retryDelayMs > 0)
            {
                Task.Delay(retryDelayMs).ContinueWith(_ =>
                    this.BeginInvoke(new Action(() =>
                    {
                        // GetSystemMetrics를 통해 시스템이 확정한 커서 크기를 바로 가져옵니다
                        int sysCursorWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXCURSOR);
                        int expectedPhys = sysCursorWidth > 0 ? sysCursorWidth : Math.Max(32, (int)Math.Round(32 * _currentScaleRatio));
                        
                        if (expectedPhys != currentPhysSize) { _stateTimer.Stop(); RebuildStateAssets(); _stateTimer.Start(); }
                    })));
            }
        }

        private void RebakeOnScaleChange() => RebakeWithRetry(RebuildRetryAfterScaleChangeMs);

        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            if (this.InvokeRequired) { this.BeginInvoke(new Action(() => OnDisplaySettingsChanged(sender, e))); return; }
            Task.Delay(DisplaySettingsChangedDelayMs).ContinueWith(_ => this.BeginInvoke(new Action(RebakeOnScaleChange)));
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.Accessibility || e.Category == UserPreferenceCategory.Mouse)
            {
                if (this.InvokeRequired) { this.BeginInvoke(new Action(() => OnUserPreferenceChanged(sender, e))); return; }
                Task.Delay(UserPreferenceChangedDelayMs).ContinueWith(_ => this.BeginInvoke(new Action(RebakeOnScaleChange)));
            }
        }

        protected override void OnPaint(PaintEventArgs e) { }
        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _currentHwnd = NativeMethods.GetForegroundWindow();
            _lastPolledHFore = _currentHwnd; // 포커스 변경 감지를 위한 초기화
            _lastForegroundHwnd = _currentHwnd;
            _showMiniIndicator = IsTargetProcess(_currentHwnd);
            _lastSyncHangulState = ImeState.IsHangulModeSystemWide(_currentHwnd);
            
            // 오버레이 폼 초기화 및 프로세서 델리게이트 연동
            _textOverlayForm = new TextOverlayForm();
            
            // 앱 시작 시에는 현재 포커스 창만 기억하고 입력모드는 강제 변경하지 않습니다.
            // IsTaskbarWindow 및 IsTrayOrAppWindow 호출
            if (_currentHwnd != IntPtr.Zero && !IsTaskbarWindow(_currentHwnd) && !IsTrayOrAppWindow(_currentHwnd))
            {
                LastValidHwnd = _currentHwnd;
                LastValidFocusHwnd = GetFocusedInputHwnd(_currentHwnd);
            }

            ApplyState(ImeState.Detect(_currentHwnd,
                _capsMode == CapsMode.Pali,
                _capsMode == CapsMode.Japanese1,
                _capsMode == CapsMode.Japanese2,
                _capsMode == CapsMode.Engineer));

            _stateTimer.Start();
        }

        // =======================================================================================
        // [오버레이 관련 렌더링 및 퍼블릭 API (Lang.cs 연동)]
        // =======================================================================================
        /// <summary>
        /// Lang.cs 혹은 다른 모듈에서 텍스트 표시창을 강제로 띄울 때 호출하는 공개 메서드입니다.
        /// </summary>
        
        public void ShowOverlay(string text, int durationMs = OverlayDefaultDurationMs)
        {
            if (!_showTextOverlay) return;

            // 포인터 크기 계산에 썼던 동일한 디스플레이 배율(_currentScaleRatio)을 적용하여 동적 스케일링
            float scaledFontSize = OverlayDefaultFontSize * _currentScaleRatio;
            int scaledHeight = (int)Math.Round(OverlayDefaultHeight * _currentScaleRatio);
            int scaledCharWidth = (int)Math.Round(OverlayDefaultCharWidth * _currentScaleRatio);
            int scaledPadWidth = (int)Math.Round(OverlayDefaultPaddingWidth * _currentScaleRatio);
            int scaledYOffset = (int)Math.Round(OverlayDefaultYOffset * _currentScaleRatio);

            ShowOverlayInternal(text, durationMs > 0, scaledFontSize, scaledHeight, scaledCharWidth, scaledPadWidth, scaledYOffset);
        }

        public void ClearOverlay() => _textOverlayForm?.Clear();

        private Size CalcOverlaySize(string ch, int charW, int padW, int formH)
        {
            // 한글/기타 문자열의 길이에 맞춰 오버레이 폭을 가변적으로 계산
            int length = 0;
            foreach (char c in ch) length += (c >= 0x1100 && c <= 0xD7AF) ? 2 : 1; 
            
            // 오버레이 폼의 최소 너비(기존 40)에도 배율을 반영하여 텍스트 짤림 방지
            int minWidth = (int)Math.Round(40 * _currentScaleRatio);
            
            return new Size(Math.Max(length * (charW / 2) + padW, minWidth), formH);
        }

        private void ShowOverlayInternal(string ch, bool useTimer, float fontSize, int formH, int charW, int padW, int yOffset)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => ShowOverlayInternal(ch, useTimer, fontSize, formH, charW, padW, yOffset)));
                return;
            }

            Point caretPos = GetInputCaretPosition();
            Size size = CalcOverlaySize(ch, charW, padW, formH);

            _textOverlayForm?.ShowOverlay(ch, useTimer, fontSize, size.Width, size.Height, caretPos.X, caretPos.Y + yOffset);
        }

        private static Point GetInputCaretPosition()
        {
            IntPtr hFore = NativeMethods.GetForegroundWindow();
            uint tid = NativeMethods.GetWindowThreadProcessId(hFore, out _);

            NativeMethods.GUITHREADINFO gti = new() { cbSize = Marshal.SizeOf<NativeMethods.GUITHREADINFO>() };
            if (NativeMethods.GetGUIThreadInfo(tid, ref gti) && gti.hwndCaret != IntPtr.Zero)
            {
                NativeMethods.POINT pt = new() { X = gti.rectLeft, Y = gti.rectBottom };
                NativeMethods.ClientToScreen(gti.hwndCaret, ref pt);
                return new Point(pt.X, pt.Y);
            }

            if (NativeMethods.GetCursorPos(out NativeMethods.POINT mPt))
                return new Point(mPt.X, mPt.Y);

            return Point.Empty;
        }
        // =======================================================================================

        private void DisposeAssetCache()
        {
            foreach (var asset in _assetCache.Values) try { asset.Dispose(); } catch { }
            _assetCache.Clear();
        }

        private static float GetPrimaryMonitorDpi()
        {
            IntPtr hFore = NativeMethods.GetForegroundWindow();
            if (hFore != IntPtr.Zero)
            {
                IntPtr hMonitor = NativeMethods.MonitorFromWindow(hFore, NativeMethods.MONITOR_DEFAULTTONEAREST);
                if (hMonitor != IntPtr.Zero && NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY) == 0 && dpiX > 0)
                    return (float)dpiX;
            }
            uint sysDpi = NativeMethods.GetDpiForSystem();
            return sysDpi > 0 ? (float)sysDpi : 96f;
        }

        private void RebuildStateAssets()
        {
            bool trayWasVisible = false;
            try { trayWasVisible = _trayIcon?.Visible ?? false; } catch { }

            DisposeAssetCache();
            RestoreDefaults();

            float dpi = GetPrimaryMonitorDpi();
            _currentScaleRatio = dpi / 96f;

            int sysCursorWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXCURSOR);
            int physicalSize = sysCursorWidth > 0 ? sysCursorWidth : Math.Max(32, (int)Math.Round(32 * _currentScaleRatio));

            _pointerPhysicalSize = physicalSize;
            _physIndicatorOffsetX = physicalSize * 0.5f;
            
            bool anyWinColorFailed = false;

            foreach (ImeState.State state in Enum.GetValues(typeof(ImeState.State)))
            {
                if (!AppConfig.Themes.TryGetValue(state, out AppConfig.Theme theme)) continue;
                try
                {
                    IntPtr hArrowNew = WinColorPointerFactory.CreateColoredSystemPointer(NativeMethods.OCR_NORMAL, theme.PointerColor, _pointerPhysicalSize);
                    IntPtr hIBeamNew = WinColorPointerFactory.CreateColoredSystemPointer(NativeMethods.OCR_IBEAM, theme.IBeamColor, _pointerPhysicalSize);

                    Color winIBeamColor = theme.IBeamColor == Color.White ? Color.Black : theme.IBeamColor;
                    IntPtr hArrowWin = WinColorPointerFactory.CreateColoredSystemPointer(NativeMethods.OCR_NORMAL, theme.PointerColor, _pointerPhysicalSize);
                    IntPtr hIBeamWin = WinColorPointerFactory.CreateColoredSystemPointer(NativeMethods.OCR_IBEAM, winIBeamColor, _pointerPhysicalSize);

                    if (hArrowWin == IntPtr.Zero) { hArrowWin = NativeMethods.CopyIcon(hArrowNew); anyWinColorFailed = true; }
                    if (hIBeamWin == IntPtr.Zero) { hIBeamWin = NativeMethods.CopyIcon(hIBeamNew); anyWinColorFailed = true; }

                    StateAssets assets = new()
                    {
                        DotColor = theme.PointerColor,
                        Description = theme.Description,
                        ArrowNewPtr = hArrowNew,
                        IBeamNewPtr = hIBeamNew,
                        ArrowWinPtr = hArrowWin,
                        IBeamWinPtr = hIBeamWin,
                        TrayIcon = TrayIconRenderer.Create(theme.TrayText, theme.TrayBgColor, theme.TrayTextColor),
                    };
                    assets.IBeamCompareHandleNew = NativeMethods.CopyIcon(hIBeamNew);
                    assets.IBeamCompareHandleWin = NativeMethods.CopyIcon(hIBeamWin);

                    _assetCache[state] = assets;
                }
                catch { }
            }

            try
            {
                if (trayWasVisible && _trayIcon != null)
                {
                    _trayIcon.Visible = true;
                    ImeState.State stateToApply = _lastState == (ImeState.State)(-1) ? ImeState.State.EnglishLower : _lastState;
                    if (_assetCache.TryGetValue(stateToApply, out var recoveryAssets) && recoveryAssets.TrayIcon != null) 
                        _trayIcon.Icon = recoveryAssets.TrayIcon;
                }
            }
            catch { }

            if (_pointerMode == PointerMode.WinColor && anyWinColorFailed)
            {
                _pointerMode = PointerMode.NewColor; RefreshPointerModeCheck(); _lastState = (ImeState.State)(-1);
            }
        }

        private void StateTimer_Tick(object? sender, EventArgs e)
        {
            IntPtr actualHFore = NativeMethods.GetForegroundWindow();
            
            // 포커스가 기존 창에서 새로 옮겨갔는지 확인하여 1회성 강제 동기화 여부를 결정
            bool isFocusChanged = (actualHFore != _lastPolledHFore);
            
            // 작업표시줄과 앱(트레이 등) 연관 창 분리 적용
            bool isTaskbar = IsTaskbarWindow(actualHFore);
            bool isTrayOrApp = IsTrayOrAppWindow(actualHFore);
            bool isLayoutForm = IsKeyboardLayoutForeground(actualHFore);

            UpdateLastValidWindows(actualHFore, isTaskbar, isTrayOrApp, isLayoutForm);
            SyncHangulStateAcrossWindows(actualHFore, isTaskbar, isTrayOrApp, isLayoutForm, isFocusChanged);

            _lastPolledHFore = actualHFore; // 다음 틱을 위해 저장

            IntPtr contextHwnd = ResolveContextWindow(actualHFore);
            bool cachedIsHangulMode = ImeState.IsHangulModeSystemWide(contextHwnd);
            ushort contextLangId = GetContextLanguageId(contextHwnd);

            UpdateCurrentWindowTracking(contextHwnd, isTaskbar, isTrayOrApp, isLayoutForm);

            ImeState.State currentState = DetectCurrentState(contextHwnd);
            ActiveInputModeContext activeInputMode = ResolveActiveInputMode(currentState);

            UpdateHookContext(contextHwnd, contextLangId, cachedIsHangulMode, activeInputMode);

            if (currentState != _lastState)
            {
                _lastState = currentState;
                ApplyState(currentState);
            }

            RefreshKeyboardLayoutOverlay();
            UpdateMiniIndicator(currentState);
        }

        private bool IsKeyboardLayoutForeground(IntPtr actualHFore) =>
            _keyboardLayoutForm != null && actualHFore == _keyboardLayoutForm.Handle;

        private void UpdateLastValidWindows(IntPtr actualHFore, bool isTaskbar, bool isTrayOrApp, bool isLayoutForm)
        {
            // 작업표시줄과 트레이/앱 창이 아닌 경우에만 실제 텍스트 입력창으로 갱신
            if (!isTaskbar && !isTrayOrApp && !isLayoutForm && actualHFore != IntPtr.Zero && actualHFore != this.Handle)
            {
                LastValidHwnd = actualHFore;
                LastValidFocusHwnd = GetFocusedInputHwnd(actualHFore);
            }
        }

        // 요청사항 1,2,3에 따른 동기화 로직 전면 개편 및 트레이/작업표시줄 역동기화(Bounce-back) 문제 해결
        private void SyncHangulStateAcrossWindows(IntPtr actualHFore, bool isTaskbar, bool isTrayOrApp, bool isLayoutForm, bool isFocusChanged)
        {
            bool currentHangul = ImeState.IsHangulModeSystemWide(actualHFore);

            if (isFocusChanged)
            {
                if (LastValidHwnd != IntPtr.Zero)
                {
                    bool groupA_Hangul = ImeState.IsHangulModeSystemWide(LastValidHwnd);

                    // 작업표시줄, 트레이/앱, 배열창으로 포커스 이동 시 Group A(문자 입력창)의 상태로 1회 강제 동기화
                    if (isTaskbar || isTrayOrApp || isLayoutForm)
                    {
                        if (groupA_Hangul != currentHangul)
                        {
                            ImeState.SetHangulState(actualHFore, groupA_Hangul);
                            
                            // 핵심 수정: 가짜 상태 할당 제거. 실제 윈도우에 반영된 상태를 다시 읽어와 기준값을 맞춤.
                            currentHangul = ImeState.IsHangulModeSystemWide(actualHFore);
                        }
                    }
                }
                _lastSyncHangulState = currentHangul;
            }
            else
            {
                // 포커스가 유지된 상태에서 상태가 변경된 경우
                if (currentHangul != _lastSyncHangulState)
                {
                    _lastSyncHangulState = currentHangul;

                    if (isTaskbar)
                    {
                        if (LastValidHwnd != IntPtr.Zero && LastValidHwnd != actualHFore)
                            ImeState.SetHangulState(LastValidHwnd, currentHangul);

                        if (_keyboardLayoutForm != null && _keyboardLayoutForm.Handle != IntPtr.Zero)
                            ImeState.SetHangulState(_keyboardLayoutForm.Handle, currentHangul);

                        if (this.Handle != IntPtr.Zero)
                            ImeState.SetHangulState(this.Handle, currentHangul);
                    }
                    else
                    {
                        if (LastValidHwnd != IntPtr.Zero && actualHFore != LastValidHwnd)
                            ImeState.SetHangulState(LastValidHwnd, currentHangul);

                        if (_keyboardLayoutForm != null && _keyboardLayoutForm.Handle != IntPtr.Zero && actualHFore != _keyboardLayoutForm.Handle)
                            ImeState.SetHangulState(_keyboardLayoutForm.Handle, currentHangul);

                        if (this.Handle != IntPtr.Zero && actualHFore != this.Handle)
                            ImeState.SetHangulState(this.Handle, currentHangul);
                    }
                }
            }
        }

        private IntPtr ResolveContextWindow(IntPtr actualHFore)
        {
            IntPtr contextHwnd = LastValidHwnd != IntPtr.Zero ? LastValidHwnd : actualHFore;
            return contextHwnd == IntPtr.Zero ? actualHFore : contextHwnd;
        }

        private static ushort GetContextLanguageId(IntPtr contextHwnd)
        {
            if (contextHwnd == IntPtr.Zero) return 0;

            uint contextTid = NativeMethods.GetWindowThreadProcessId(contextHwnd, out _);
            return (ushort)(NativeMethods.GetKeyboardLayout(contextTid).ToInt64() & 0xFFFF);
        }

        private void UpdateCurrentWindowTracking(IntPtr contextHwnd, bool isTaskbar, bool isTrayOrApp, bool isLayoutForm)
        {
            if (contextHwnd != _currentHwnd)
            {
                if (!isTaskbar && !isTrayOrApp && !isLayoutForm)
                {
                    _lastForegroundHwnd = contextHwnd;
                    _showMiniIndicator = IsTargetProcess(contextHwnd);
                    _isPointerInsideCell = false;
                }
                _currentHwnd = contextHwnd;
            }
        }

        private ImeState.State DetectCurrentState(IntPtr contextHwnd) =>
            ImeState.Detect(contextHwnd,
                _capsMode == CapsMode.Pali,
                _capsMode == CapsMode.Japanese1,
                _capsMode == CapsMode.Japanese2,
                _capsMode == CapsMode.Engineer);

        private ActiveInputModeContext ResolveActiveInputMode(ImeState.State currentState)
        {
            CapsModeStateMapping[] mappings =
            {
                new(CapsMode.Pali, ImeState.State.PaliHangul, KeyProcessorFactory.Pali),
                new(CapsMode.Engineer, ImeState.State.Engineer, KeyProcessorFactory.Engineer),
                new(CapsMode.Japanese1, ImeState.State.JapaneseHangul1, KeyProcessorFactory.Japanese1),
                new(CapsMode.Japanese2, ImeState.State.JapaneseHangul2, KeyProcessorFactory.Japanese2),
            };

            foreach (CapsModeStateMapping mapping in mappings)
            {
                if (_capsMode == mapping.Mode && currentState == mapping.ActiveState)
                {
                    return new ActiveInputModeContext(
                        mapping.Mode == CapsMode.Pali,
                        mapping.Mode == CapsMode.Engineer,
                        mapping.Mode == CapsMode.Japanese1,
                        mapping.Mode == CapsMode.Japanese2,
                        mapping.Processor);
                }
            }

            return new ActiveInputModeContext(false, false, false, false, null);
        }

        private static void UpdateHookContext(
            IntPtr contextHwnd,
            ushort contextLangId,
            bool cachedIsHangulMode,
            ActiveInputModeContext activeInputMode)
        {
            GlobalInputHook.UpdateContext(new GlobalInputHook.HookContextSnapshot(
                contextHwnd,
                contextLangId,
                cachedIsHangulMode,
                activeInputMode.ActiveProcessor,
                activeInputMode.IsPaliModeActive,
                activeInputMode.IsEngineerModeActive,
                activeInputMode.IsJapanese1ModeActive,
                activeInputMode.IsJapanese2ModeActive));
        }

        private void UpdateMiniIndicator(ImeState.State currentState)
        {
            if (!NativeMethods.GetCursorPos(out NativeMethods.POINT pt))
                return;

            if (_showMiniIndicator && _enableMiniIndicator)
            {
                bool isIBeamActive = IsCurrentPointerIBeam(currentState);
                if (isIBeamActive != _isPointerInsideCell)
                {
                    UpdateLayeredIndicator(Color.Transparent, HiddenLayeredWindowLocation, HiddenLayeredWindowLocation);
                    _isPointerInsideCell = isIBeamActive;
                }

                if (!_isPointerInsideCell)
                {
                    UpdateIndicatorNearPointer(pt.X, pt.Y);
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

        private void UpdateIndicatorNearPointer(float hotX, float hotY)
        {
            float targetX;
            float targetY;

            if (IsArrowPointer())
            {
                float offsetPhys = AppConfig.IndicatorOffset * (_pointerPhysicalSize / 32f);
                targetX = hotX + PointerDiagonalFactor * offsetPhys;
                targetY = hotY + PointerDiagonalFactor * offsetPhys;
            }
            else
            {
                targetX = hotX + _physIndicatorOffsetX;
                targetY = hotY + (_pointerPhysicalSize * IBeamIndicatorYOffsetFactor);
            }

            float pointerBottomY = hotY + _pointerPhysicalSize;
            if (targetY < pointerBottomY + IndicatorBottomMargin) targetY = pointerBottomY + IndicatorBottomMargin;

            int destX = (int)Math.Round(targetX - (_indicatorCanvasSize / 2.0f));
            int destY = (int)Math.Round(targetY - (_indicatorCanvasSize / 2.0f));
            UpdateLayeredIndicator(_currentDotColor, destX, destY);
        }

        private string? GetKeyboardLayoutImageName()
        {
            var processor = GlobalInputHook.ActiveProcessor;
            bool isPhysicalShift = (NativeMethods.GetKeyState(0x10) & 0x8000) != 0;
            bool isVirtualShift = processor != null ? processor.IsVirtualShift : _visualShiftInversion;

            bool displayShift = isPhysicalShift ^ isVirtualShift;
            string shiftSuffix = displayShift ? "2" : "1";

            // 키보드 배열창 기능 영어 및 한글 확장, "한글CAPS 한글" 처리 반영
            // 1. 영어 입력 모드 (소문자, 대문자 및 영문 기반 Pali US, Japanese IME 포함)
            if (_lastState == ImeState.State.EnglishLower || _lastState == ImeState.State.EnglishUpper || 
                _lastState == ImeState.State.PaliUS || _lastState == ImeState.State.JapaneseIME)
            {
                return $"EnglishKey{shiftSuffix}.png";
            }

            // 2. 한글 입력 모드 (Caps Lock Off 상태) 및 "한글CAPS 한글" 메뉴 선택 시
            if (_lastState == ImeState.State.Hangul)
            {
                return $"KoreanKey{shiftSuffix}.png";
            }

            // 3. 기존 특수 기능 모드 (한글 + Caps Lock On 상태)
            if (_capsMode == CapsMode.Pali) return $"PaliKey{shiftSuffix}.png";
            if (_capsMode == CapsMode.Engineer) return $"EngineerKey{shiftSuffix}.png";
            if (_capsMode == CapsMode.Japanese1) return $"Japan1Layer{(processor?.CurrentLayer ?? 1)}Key{shiftSuffix}.png";
            if (_capsMode == CapsMode.Japanese2) return $"Japan2Layer{(processor?.CurrentLayer ?? 1)}Key{shiftSuffix}.png";

            // 기본 예외 처리 (안전망)
            if (_capsMode == CapsMode.WinDefault) return $"KoreanKey{shiftSuffix}.png";
            
            return null;
        }

        private void RefreshKeyboardLayoutOverlay()
        {
            if (!_showKeyboardLayoutOverlay)
            {
                CloseAllLayoutForms();
                return;
            }

            string? imageName = GetKeyboardLayoutImageName();
            if (string.IsNullOrEmpty(imageName)) return;

            if (_keyboardLayoutForm == null || _keyboardLayoutForm.IsDisposed)
            {
                _keyboardLayoutForm = new KeyboardLayoutForm();
                
                if (_keyboardLayoutLastLocation != Point.Empty)
                {
                    _keyboardLayoutForm.Location = _keyboardLayoutLastLocation;
                }
                
                _keyboardLayoutForm.OnLayoutDoubleClicked += (s, e) =>
                {
                    if (GlobalInputHook.ActiveProcessor != null)
                        GlobalInputHook.ActiveProcessor.ToggleVirtualShift();
                    else
                        _visualShiftInversion = !_visualShiftInversion;
                    
                    RefreshKeyboardLayoutOverlay();
                };
                
                _keyboardLayoutForm.OnClosedByUser += (s, e) =>
                {
                    HideKeyboardLayoutOverlay();
                };
            }

            _keyboardLayoutForm.UpdateImage(imageName);

            if (!_keyboardLayoutForm.Visible)
            {
                _keyboardLayoutForm.Show();
                if (_keyboardLayoutForm.WindowState == FormWindowState.Minimized)
                {
                    _keyboardLayoutForm.WindowState = FormWindowState.Normal;
                }
            }
        }

        public void HideKeyboardLayoutOverlay()
        {
            _showKeyboardLayoutOverlay = false;
            _toggleKeyboardLayoutMenuItem.Checked = false;
            CloseAllLayoutForms();
        }

        private void CloseAllLayoutForms()
        {
            if (_keyboardLayoutForm != null)
            {
                _keyboardLayoutLastLocation = _keyboardLayoutForm.Location;
                _keyboardLayoutForm.Close();
                _keyboardLayoutForm = null;
            }
        }

        private void ApplyState(ImeState.State state)
        {
            if (!_assetCache.TryGetValue(state, out StateAssets? assets)) return;
            _currentDotColor = assets.DotColor;

            try
            {
                if (assets.TrayIcon != null && (_trayIcon.Icon == null || _trayIcon.Icon.Handle != assets.TrayIcon.Handle)) 
                    _trayIcon.Icon = assets.TrayIcon;
            }
            catch { _trayIcon.Icon = assets.TrayIcon; }

            switch (_pointerMode)
            {
                case PointerMode.WinDefault:
                    RestoreDefaults(); _lastAppliedArrowHandle = IntPtr.Zero; break;
                case PointerMode.WinColor:
                    {
                        IntPtr hArrow = NativeMethods.CopyIcon(assets.ArrowWinPtr); IntPtr hIBeam = NativeMethods.CopyIcon(assets.IBeamWinPtr);
                        _lastAppliedArrowHandle = hArrow; ReplaceSystemPointer(hArrow, NativeMethods.OCR_NORMAL); ReplaceSystemPointer(hIBeam, NativeMethods.OCR_IBEAM);
                        break;
                    }
                case PointerMode.NewColor:
                    {
                        IntPtr hArrow = NativeMethods.CopyIcon(assets.ArrowNewPtr); IntPtr hIBeam = NativeMethods.CopyIcon(assets.IBeamNewPtr);
                        _lastAppliedArrowHandle = hArrow; ReplaceSystemPointer(hArrow, NativeMethods.OCR_NORMAL); ReplaceSystemPointer(hIBeam, NativeMethods.OCR_IBEAM);
                        break;
                    }
            }

            _trayIcon.Text = UiText.TrayTooltip(assets.Description);
            _statusMenuItem.Text = UiText.StatusLabel(assets.Description);
        }

        private bool IsCurrentPointerIBeam(ImeState.State state)
        {
            NativeMethods.CURSORINFO ci = new() { cbSize = Marshal.SizeOf<NativeMethods.CURSORINFO>() };
            if (!NativeMethods.GetCursorInfo(ref ci) || ci.hCursor == IntPtr.Zero) return false;
            if (!_assetCache.TryGetValue(state, out StateAssets? assets)) return false;
            IntPtr compareHandle = _pointerMode == PointerMode.WinColor ? assets.IBeamCompareHandleWin : assets.IBeamCompareHandleNew;
            return ci.hCursor == compareHandle;
        }

        private bool IsArrowPointer()
        {
            if (_pointerMode == PointerMode.WinDefault)
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
                }
                catch { }
                return false;
            }

            if (_lastState == (ImeState.State)(-1)) return false;
            if (!_assetCache.TryGetValue(_lastState, out StateAssets? assets)) return false;
            var curInfo = new NativeMethods.CURSORINFO { cbSize = Marshal.SizeOf<NativeMethods.CURSORINFO>() };
            if (!NativeMethods.GetCursorInfo(ref curInfo) || curInfo.hCursor == IntPtr.Zero) return false;
            IntPtr ibeamCompare = _pointerMode == PointerMode.WinColor ? assets.IBeamCompareHandleWin : assets.IBeamCompareHandleNew;
            return curInfo.hCursor != ibeamCompare;
        }

        private static IntPtr GetFocusedInputHwnd(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return IntPtr.Zero;

            uint threadId = NativeMethods.GetWindowThreadProcessId(hWnd, out _);
            NativeMethods.GUITHREADINFO gti = new() { cbSize = Marshal.SizeOf<NativeMethods.GUITHREADINFO>() };

            if (NativeMethods.GetGUIThreadInfo(threadId, ref gti))
            {
                if (gti.hwndFocus != IntPtr.Zero) return gti.hwndFocus;
                if (gti.hwndActive != IntPtr.Zero) return gti.hwndActive;
            }

            return hWnd;
        }

        // 기존 IsTaskbarOrTrayWindow를 IsTaskbarWindow와 IsTrayOrAppWindow로 분리하여 역할 명확화
        private unsafe bool IsTaskbarWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return false;

            Span<char> className = stackalloc char[256];
            fixed (char* pName = className)
            {
                int length = NativeMethods.GetClassName(hWnd, pName, 256);
                if (length > 0)
                {
                    ReadOnlySpan<char> nameSpan = className.Slice(0, length);
                    return nameSpan.IndexOf("Shell_TrayWnd".AsSpan(), StringComparison.OrdinalIgnoreCase) >= 0
                        || nameSpan.IndexOf("NotifyIconOverflowWindow".AsSpan(), StringComparison.OrdinalIgnoreCase) >= 0; 
                }
                return false;
            }
        }

        private unsafe bool IsTrayOrAppWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || hWnd == this.Handle) return true;

            NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == _currentProcessId) return true; 

            Span<char> className = stackalloc char[256];
            fixed (char* pName = className)
            {
                int length = NativeMethods.GetClassName(hWnd, pName, 256);
                if (length > 0)
                {
                    ReadOnlySpan<char> nameSpan = className.Slice(0, length);
                    return nameSpan.IndexOf("Progman".AsSpan(), StringComparison.OrdinalIgnoreCase) >= 0
                        || nameSpan.IndexOf("WorkerW".AsSpan(), StringComparison.OrdinalIgnoreCase) >= 0
                        || nameSpan.IndexOf("#32768".AsSpan(), StringComparison.OrdinalIgnoreCase) >= 0; 
                }
                return false;
            }
        }

        private static void ReplaceSystemPointer(IntPtr hNew, uint pointerId)
        {
            if (hNew == IntPtr.Zero) return;
            if (!NativeMethods.SetSystemCursor(hNew, pointerId)) NativeMethods.DestroyCursor(hNew);
        }

        private static bool IsTargetProcess(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return false;
            NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == 0) return false;
            try
            {
                using var proc = System.Diagnostics.Process.GetProcessById((int)pid);
                string name = proc.ProcessName;
                foreach (string targetApp in AppConfig.IndicatorTargetApps)
                    if (name.Equals(targetApp, StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }
            catch { return false; }
        }

        public static void RestoreDefaults() => NativeMethods.SystemParametersInfo(NativeMethods.SPI_SETCURSORS, 0, IntPtr.Zero, NativeMethods.SPIF_SENDCHANGE);

        private void UpdateLayeredIndicator(Color color, int x, int y)
        {
            bool needsUpdate = false;
            if (color != _lastIndicatorColor)
            {
                _lastIndicatorColor = color;
                if (color != Color.Transparent) BakeIndicatorBuffer(color);
                needsUpdate = true;
            }
            if (x != _lastIndicatorX || y != _lastIndicatorY)
            {
                _lastIndicatorX = x; _lastIndicatorY = y; needsUpdate = true;
            }
            if (!needsUpdate) return;

            NativeMethods.SIZE sz = new() { cx = _indicatorCanvasSize, cy = _indicatorCanvasSize };
            NativeMethods.POINT srcPt = new() { X = 0, Y = 0 };
            NativeMethods.POINT destPt = new() { X = x, Y = y };
            NativeMethods.BLENDFUNCTION bf = new() { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };

            if (color == Color.Transparent || !_isIndicatorBaked)
            {
                if (_indicatorMemDc != IntPtr.Zero)
                {
                    destPt.X = -10000; destPt.Y = -10000; bf.SourceConstantAlpha = 0;
                    IntPtr sDc = NativeMethods.GetDC(IntPtr.Zero);
                    _ = NativeMethods.UpdateLayeredWindow(this.Handle, sDc, ref destPt, ref sz, _indicatorMemDc, ref srcPt, 0, ref bf, 2);
                    _ = NativeMethods.ReleaseDC(IntPtr.Zero, sDc);
                }
                return;
            }

            IntPtr curScreenDc = NativeMethods.GetDC(IntPtr.Zero);
            _ = NativeMethods.UpdateLayeredWindow(this.Handle, curScreenDc, ref destPt, ref sz, _indicatorMemDc, ref srcPt, 0, ref bf, 2);
            _ = NativeMethods.ReleaseDC(IntPtr.Zero, curScreenDc);
        }

        private void BakeIndicatorBuffer(Color color)
        {
            CleanUpIndicatorGdi();
            if (color == Color.Transparent) return;

            float circleSize = AppConfig.IndicatorSize * _currentScaleRatio;
            float penWidth = 1.0f;
            _indicatorCanvasSize = (int)Math.Ceiling(circleSize + (penWidth * 2) + 6);
            if (_indicatorCanvasSize % 2 != 0) _indicatorCanvasSize++;

            using Bitmap bmp = new(_indicatorCanvasSize, _indicatorCanvasSize, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.Clear(Color.Transparent);
                float center = _indicatorCanvasSize / 2f; float radius = circleSize / 2f;
                Color penColor = (color == Color.White) ? Color.Black : (color == Color.Black) ? Color.White : Color.Black;
                using SolidBrush brush = new(color); g.FillEllipse(brush, center - radius, center - radius, circleSize, circleSize);
                using Pen pen = new(penColor, penWidth); g.DrawEllipse(pen, center - radius, center - radius, circleSize, circleSize);
            }
            _indicatorScreenDc = NativeMethods.GetDC(IntPtr.Zero);
            _indicatorMemDc = NativeMethods.CreateCompatibleDC(_indicatorScreenDc);
            _indicatorHBitmap = CreateAlphaHBitmap(bmp, _indicatorScreenDc);
            _indicatorOldBitmap = NativeMethods.SelectObject(_indicatorMemDc, _indicatorHBitmap);
            _isIndicatorBaked = true;
        }

        private void CleanUpIndicatorGdi()
        {
            if (_indicatorMemDc != IntPtr.Zero)
            {
                if (_indicatorOldBitmap != IntPtr.Zero) { _ = NativeMethods.SelectObject(_indicatorMemDc, _indicatorOldBitmap); _indicatorOldBitmap = IntPtr.Zero; }
                _ = NativeMethods.DeleteDC(_indicatorMemDc); _indicatorMemDc = IntPtr.Zero;
            }
            if (_indicatorHBitmap != IntPtr.Zero)
            {
                _ = NativeMethods.DeleteObject(_indicatorHBitmap); _indicatorHBitmap = IntPtr.Zero;
            }
            if (_indicatorScreenDc != IntPtr.Zero)
            {
                _ = NativeMethods.ReleaseDC(IntPtr.Zero, _indicatorScreenDc); _indicatorScreenDc = IntPtr.Zero;
            }
            _isIndicatorBaked = false;
        }

        private static unsafe IntPtr CreateAlphaHBitmap(Bitmap bitmap, IntPtr hdcScreen)
        {
            NativeMethods.BITMAPINFO bmi = new() { biSize = s_bmiSize, biWidth = bitmap.Width, biHeight = -bitmap.Height, biPlanes = 1, biBitCount = 32, biCompression = 0 };
            IntPtr hBitmap = NativeMethods.CreateDIBSection(hdcScreen, ref bmi, 0, out IntPtr pBits, IntPtr.Zero, 0);
            if (hBitmap == IntPtr.Zero) return IntPtr.Zero;
            var bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            int bytes = Math.Abs(bmpData.Stride) * bitmap.Height;
            Buffer.MemoryCopy((void*)bmpData.Scan0, (void*)pBits, bytes, bytes);
            bitmap.UnlockBits(bmpData);
            return hBitmap;
        }
    }
    #endregion

    #region [ IME 상태 감지 모듈 ]
    // IME 상태 확인 로직 내 중복 함수 통합 및 윈도우별 캐싱 적용
    internal static class ImeState
    {
        public enum State
        {
            EnglishLower, EnglishUpper, Hangul, PaliUS, PaliHangul, JapaneseIME, JapaneseHangul1, JapaneseHangul2, Engineer
        }

        public static bool IsHangul(State state) =>
            state == State.Hangul || state == State.PaliHangul || state == State.JapaneseHangul1 || state == State.JapaneseHangul2 || state == State.Engineer;

        public static State Detect(IntPtr foregroundHwnd,
            bool enablePali = false, bool enableJapanese1 = false, bool enableJapanese2 = false, bool enableEngineer = false)
        {
            bool capsOn = (NativeMethods.GetKeyState(NativeMethods.VK_CAPITAL) & 0x0001) != 0;
            if (foregroundHwnd == IntPtr.Zero) return capsOn ? State.EnglishUpper : State.EnglishLower;

            uint threadId = NativeMethods.GetWindowThreadProcessId(foregroundHwnd, out _);
            long hklValue = NativeMethods.GetKeyboardLayout(threadId).ToInt64();
            ushort langId = (ushort)(hklValue & 0xFFFF);

            if (langId == 0x0409) return State.PaliUS;
            if (langId == 0x0411) return State.JapaneseIME;

            if (langId == 0x0412)
            {
                bool isHangul = IsHangulModeSystemWide(foregroundHwnd);
                if (isHangul)
                {
                    if (capsOn)
                    {
                        if (enablePali) return State.PaliHangul;
                        if (enableEngineer) return State.Engineer;
                        if (enableJapanese1) return State.JapaneseHangul1;
                        if (enableJapanese2) return State.JapaneseHangul2;
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

        public static bool IsHangulModeSystemWide(IntPtr foregroundHwnd)
        {
            return CheckHangulPublic(foregroundHwnd);
        }

        // 단일 변수 대신 Dictionary를 사용하여 각 윈도우 핸들별로 마지막 IME 상태를 캐싱
        private static readonly Dictionary<IntPtr, bool> _hangulStateCache = new Dictionary<IntPtr, bool>();

        public static bool CheckHangulPublic(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return false;

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
            
            // API 호출 실패 시(백그라운드 윈도우 등) 해당 윈도우의 마지막 정상 상태를 반환
            return _hangulStateCache.TryGetValue(hWnd, out bool cachedState) ? cachedState : false;
        }

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
                    
                    // 명시적으로 상태를 변경했으므로 해당 핸들의 캐시 상태도 즉시 업데이트
                    _hangulStateCache[hWnd] = setHangul;
                }
            }
        }
    }
    #endregion

    #region [ 전역 시스템 훅 모듈 통합 (GlobalInputHook) ]
    // KeyboardHook과 MouseHook을 단일 클래스로 통합하여 핸들 관리 단순화
    internal static class GlobalInputHook
    {
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

            public HookContextSnapshot(
                IntPtr contextHwnd,
                ushort contextLangId,
                bool isHangulMode,
                IKeyProcessor? activeProcessor,
                bool isPaliModeActive,
                bool isEngineerModeActive,
                bool isJapanese1ModeActive,
                bool isJapanese2ModeActive)
            {
                ContextHwnd = contextHwnd;
                ContextLangId = contextLangId;
                IsHangulMode = isHangulMode;
                ActiveProcessor = activeProcessor;
                IsPaliModeActive = isPaliModeActive;
                IsEngineerModeActive = isEngineerModeActive;
                IsJapanese1ModeActive = isJapanese1ModeActive;
                IsJapanese2ModeActive = isJapanese2ModeActive;
            }
        }

        public static bool IsEnabled { get; set; } = true;

        private static HookContextSnapshot _contextSnapshot = new(
            IntPtr.Zero,
            0,
            false,
            null,
            false,
            false,
            false,
            false);

        public static bool IsPaliModeActive => _contextSnapshot.IsPaliModeActive;
        public static bool IsEngineerModeActive => _contextSnapshot.IsEngineerModeActive;
        public static bool IsJapanese1ModeActive => _contextSnapshot.IsJapanese1ModeActive;
        public static bool IsJapanese2ModeActive => _contextSnapshot.IsJapanese2ModeActive;
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
                _mouseHookId = NativeMethods.SetWindowsHookEx(14, mouseCb, hMod, 0);
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

        public static void SendReplacement(int backCount, string text)
        {
            IsSending = true;
            for (int i = 0; i < backCount; i++) NativeMethods.SendBackspace();
            if (!string.IsNullOrEmpty(text)) NativeMethods.SendUnicodeString(text);
            IsSending = false;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
        private static IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam.ToInt32() == 0x0201)
            {
                ActiveProcessor?.OnMouseClick();
            }
            return NativeMethods.CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
        }

        private static bool IsInterestedKeyboardMessage(int msg) =>
            msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN;

        private static bool IsHanjaOrRightCtrl(int vkCode) => vkCode == 0x19 || vkCode == 0xA3;

        private static bool HasBlockedModifierChord(bool allowCtrlForCurrentKey)
        {
            bool isCtrl = (NativeMethods.GetKeyState(0x11) & 0x8000) != 0;
            if (isCtrl && !allowCtrlForCurrentKey) return true;

            if ((NativeMethods.GetKeyState(0x12) & 0x8000) != 0) return true;

            return (NativeMethods.GetKeyState(0x5B) & 0x8000) != 0
                || (NativeMethods.GetKeyState(0x5C) & 0x8000) != 0;
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
                return (IntPtr)1;
            }

            IKeyProcessor? hanjaProcessor = ActiveProcessor;
            if (hanjaProcessor != null && hanjaProcessor.ProcessHanjaKey(hFore, capsOn, isHangulMode))
            {
                MainForm.Instance?.RequestLayoutRefresh();
                return (IntPtr)1;
            }

            return BypassKeyboardHook(nCode, wParam, lParam);
        }

        private static IntPtr HandleLanguageProcessorKey(int nCode, IntPtr wParam, IntPtr lParam, int vkCode, IntPtr hFore, bool capsOn, bool 시isHangulMode)
        {
            IKeyProcessor? keyProcessor = ActiveProcessor;
            if (keyProcessor == null || ContextLangId != 0x0412)
                return BypassKeyboardHook(nCode, wParam, lParam);

            bool isShift = (NativeMethods.GetKeyState(0x10) & 0x8000) != 0;
            if (keyProcessor.ProcessKeyDown(vkCode, isShift, capsOn, hFore, 시isHangulMode))
                return (IntPtr)1;

            return BypassKeyboardHook(nCode, wParam, lParam);
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
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
            catch
            {
            }

            return BypassKeyboardHook(nCode, wParam, lParam);
        }
    }
    #endregion

    #region [ Win32 P/Invoke API 선언부 (NativeMethods) ]
    internal static unsafe partial class NativeMethods
    {
        // --- 상수 영역 ---
        public const int VK_CAPITAL = 0x14;
        public const int WM_IME_CONTROL = 0x0283;
        public const int IMC_GETCONVERSIONMODE = 0x0001;
        public const int IMC_SETCONVERSIONMODE = 0x0002;
        public const uint IME_CMODE_NATIVE = 0x0001;
        public const uint SMTO_ABORTIFHUNG = 0x0002;
        public const uint OCR_NORMAL = 32512;
        public const uint OCR_IBEAM = 32513;
        public const uint SPI_SETCURSORS = 0x0057;
        public const uint SPIF_SENDCHANGE = 0x0002;
        public const int WH_KEYBOARD_LL = 13;
        public const int WM_KEYDOWN = 0x0100;
        public const int WM_SYSKEYDOWN = 0x0104;
        public const uint INPUT_KEYBOARD = 1;
        public const uint KEYEVENTF_UNICODE = 0x0004;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const int MDT_EFFECTIVE_DPI = 0;
        public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        public const uint IMAGE_CURSOR = 2;
        public const uint LR_SHARED = 0x00008000;
        public const uint LR_DEFAULTSIZE = 0x00000040;
        // [수정: 포인터 및 커서 시스템 메트릭스 획득을 위한 상수 추가]
        public const int SM_CXCURSOR = 13;
        public const int SM_CYCURSOR = 14;

        // --- 구조체 영역 ---
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int cx, cy; }
        [StructLayout(LayoutKind.Sequential)] public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [StructLayout(LayoutKind.Sequential)] public struct ICONINFO { public int fIcon, xHotspot, yHotspot; public IntPtr hbmMask, hbmColor; }
        [StructLayout(LayoutKind.Sequential)] public struct GUITHREADINFO { public int cbSize, flags; public IntPtr hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret; public int rectLeft, rectTop, rectRight, rectBottom; }
        [StructLayout(LayoutKind.Sequential)] public struct BITMAPINFO { public int biSize, biWidth, biHeight; public short biPlanes, biBitCount; public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant; }
        [StructLayout(LayoutKind.Sequential)] public struct CURSORINFO { public int cbSize, flags; public IntPtr hCursor; public POINT ptScreenPos; }
        [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public InputUnion U; }
        [StructLayout(LayoutKind.Explicit)] public struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; [FieldOffset(0)] public HARDWAREINPUT hi; }
        [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx, dy, mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] public struct HARDWAREINPUT { public uint uMsg; public ushort wParamL, wParamH; }

        // --- User32.dll (창, 입력, 커서 관리) ---
        [LibraryImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static partial bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);
        [LibraryImport("user32.dll", EntryPoint = "LoadImageW", SetLastError = true)] public static partial IntPtr LoadImage(IntPtr hinst, IntPtr name, uint type, int cx, int cy, uint fuLoad);
        [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)] public static partial IntPtr SetWindowsHookEx(int idHook, delegate* unmanaged[Stdcall]<int, IntPtr, IntPtr, IntPtr> lpfn, IntPtr hMod, uint dwThreadId);
        [LibraryImport("user32.dll", EntryPoint = "UnhookWindowsHookEx", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] public static partial bool UnhookWindowsHookEx(IntPtr hhk);
        [LibraryImport("user32.dll", EntryPoint = "CallNextHookEx")] public static partial IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [LibraryImport("user32.dll", EntryPoint = "SendInput", SetLastError = true)] public static partial uint SendInput(uint nInputs, ReadOnlySpan<INPUT> pInputs, int cbSize);
        [LibraryImport("user32.dll", EntryPoint = "GetDpiForSystem")] public static partial uint GetDpiForSystem();
        [LibraryImport("user32.dll", EntryPoint = "MonitorFromWindow")] public static partial IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
        [LibraryImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static partial bool GetCursorInfo(ref CURSORINFO pci);
        [LibraryImport("user32.dll", EntryPoint = "GetIconInfo")][return: MarshalAs(UnmanagedType.Bool)] public static partial bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);
        [LibraryImport("user32.dll")][SuppressGCTransition] public static partial IntPtr GetForegroundWindow();
        [LibraryImport("user32.dll")][SuppressGCTransition] public static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [LibraryImport("user32.dll")][SuppressGCTransition] public static partial IntPtr GetKeyboardLayout(uint idThread);
        [LibraryImport("user32.dll")][SuppressGCTransition] public static partial short GetKeyState(int keyCode);
        [LibraryImport("user32.dll")][SuppressGCTransition][return: MarshalAs(UnmanagedType.Bool)] public static partial bool GetCursorPos(out POINT lpPoint);
        [LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutW")] public static partial IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);
        [LibraryImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static partial bool SetSystemCursor(IntPtr hcur, uint id);
        [LibraryImport("user32.dll")] public static partial IntPtr CopyIcon(IntPtr hIcon);
        [LibraryImport("user32.dll")] public static partial IntPtr CreateIconIndirect(ref ICONINFO iconinfo);
        [LibraryImport("user32.dll", EntryPoint = "GetClassNameW", StringMarshalling = StringMarshalling.Utf16)] public static partial int GetClassName(IntPtr hWnd, char* lpClassName, int nMaxCount);
        [LibraryImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static partial bool SetForegroundWindow(IntPtr hWnd);
        [LibraryImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static partial bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, uint crKey, ref BLENDFUNCTION pblend, uint dwFlags);

        // [수정: 포인터 및 커서 시스템 메트릭스 획득 API 추가]
        [LibraryImport("user32.dll")] public static partial int GetSystemMetrics(int nIndex);
        
        // --- Gdi32.dll / Imm32.dll / Shcore.dll / 커스텀 유틸리티 등 ---
        [LibraryImport("gdi32.dll", EntryPoint = "CreateCompatibleDC")] public static partial IntPtr CreateCompatibleDC(IntPtr hdc);
        [LibraryImport("gdi32.dll", EntryPoint = "DeleteDC")][return: MarshalAs(UnmanagedType.Bool)] public static partial bool DeleteDC(IntPtr hdc);
        [LibraryImport("gdi32.dll", EntryPoint = "CreateDIBSection")] public static partial IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi, uint iUsage, out IntPtr ppvBits, IntPtr hSection, uint dwOffset);
        [LibraryImport("gdi32.dll", EntryPoint = "SelectObject")] public static partial IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
        [LibraryImport("gdi32.dll", EntryPoint = "DeleteObject")][return: MarshalAs(UnmanagedType.Bool)] public static partial bool DeleteObject(IntPtr hObject);
        [LibraryImport("user32.dll", EntryPoint = "GetDC")] public static partial IntPtr GetDC(IntPtr hWnd);
        [LibraryImport("user32.dll", EntryPoint = "ReleaseDC")] public static partial int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [LibraryImport("user32.dll", EntryPoint = "DrawIconEx")][return: MarshalAs(UnmanagedType.Bool)] public static partial bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon, int cxWidth, int cyWidth, uint istepIfAniCur, IntPtr hbrFlickerFreeDraw, uint diFlags);
        [LibraryImport("user32.dll", EntryPoint = "DestroyCursor")][return: MarshalAs(UnmanagedType.Bool)] public static partial bool DestroyCursor(IntPtr hCursor);
        
        [LibraryImport("imm32.dll")][SuppressGCTransition] public static partial IntPtr ImmGetDefaultIMEWnd(IntPtr hWnd);
        [LibraryImport("imm32.dll")][SuppressGCTransition] public static partial IntPtr ImmGetContext(IntPtr hWnd);
        
        // [수정: 텍스트 짤림으로 누락된 P/Invoke 복구 및 추가 (빌드 오류 방지용)]
        [LibraryImport("imm32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static partial bool ImmGetConversionStatus(IntPtr hIMC, out uint lpfdwConversion, out uint lpfdwSentence);
        [LibraryImport("imm32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static partial bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);
        [LibraryImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static partial bool DestroyIcon(IntPtr hIcon);
        [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW")][return: MarshalAs(UnmanagedType.Bool)] public static partial bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);
        [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)] public static partial IntPtr GetModuleHandle(string lpModuleName);
        [LibraryImport("shcore.dll")] public static partial int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        // --- 누락된 User32.dll API 선언 추가 ---
        [LibraryImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static partial bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);
        // --- Kernel32.dll & Shcore.dll ---
        [LibraryImport("kernel32.dll")] public static partial IntPtr GlobalLock(IntPtr hMem);
        [LibraryImport("kernel32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static partial bool GlobalUnlock(IntPtr hMem);

        // ---  Lang.cs에서 Clipboard 사용을 위한 Win32 API (P/Invoke) 선언 --- 
        [DllImport("user32.dll", SetLastError = true)] public static extern bool OpenClipboard(IntPtr hWndNewOwner);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool CloseClipboard();
        [DllImport("user32.dll", SetLastError = true)] public static extern bool EmptyClipboard();
        [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr GetClipboardData(uint uFormat);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool IsClipboardFormatAvailable(uint format);

        // --- 유틸리티 메서드 ---
        public static void SimulateCapsLock()
        {
            INPUT[] inputs = new INPUT[2];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = VK_CAPITAL;
            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].U.ki.wVk = VK_CAPITAL;
            inputs[1].U.ki.dwFlags = KEYEVENTF_KEYUP;
            SendInput(2, inputs, Marshal.SizeOf<INPUT>());
        }

        public static void SendBackspace()
        {
            INPUT[] inputs = new INPUT[2];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = 0x08; // VK_BACK
            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].U.ki.wVk = 0x08;
            inputs[1].U.ki.dwFlags = KEYEVENTF_KEYUP;
            SendInput(2, inputs, Marshal.SizeOf<INPUT>());
        }

        public static void SendUnicodeString(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            INPUT[] inputs = new INPUT[text.Length * 2];
            for (int i = 0; i < text.Length; i++)
            {
                inputs[i * 2].type = INPUT_KEYBOARD;
                inputs[i * 2].U.ki.wScan = text[i];
                inputs[i * 2].U.ki.dwFlags = KEYEVENTF_UNICODE;

                inputs[i * 2 + 1].type = INPUT_KEYBOARD;
                inputs[i * 2 + 1].U.ki.wScan = text[i];
                inputs[i * 2 + 1].U.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;
            }
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        }
    }
    #endregion
}