#nullable enable
using System;
using System.Drawing;
using System.Drawing.Imaging;
//using System.Runtime.InteropServices;
namespace IMEPointer {
    #region [ 6. 그래픽 및 포인터 팩토리 (PointerGraphicsFactory) ]
    /// <summary>
    /// [수정: 이름 변경 및 구조 개선] WinColorPointerFactory -> PointerGraphicsFactory로 변경하여 그래픽 생성 역할을 명확히 하였습니다.
    /// </summary>
    internal static class PointerGraphicsFactory
    {
        public static IntPtr CreateColoredSystemPointer(uint ocrId, Color targetColor, int renderSize)
        {
            IntPtr hPointer = NativeMethods.LoadImage(IntPtr.Zero, (IntPtr)ocrId, NativeMethods.IMAGE_CURSOR, renderSize, renderSize, 0);
            
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

            using Bitmap? rendered = RenderPointerToArgbBitmap(hPointer, out int actualWidth, out int actualHeight);
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

            Bitmap? scaledBitmap = null;
            if (finalBitmap.Width != renderSize || finalBitmap.Height != renderSize)
            {
                scaledBitmap = new Bitmap(renderSize, renderSize, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(scaledBitmap))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.DrawImage(finalBitmap, new Rectangle(0, 0, renderSize, renderSize));
                }
                finalBitmap = scaledBitmap;
            }

            float scaleX = (float)renderSize / actualWidth;
            float scaleY = (float)renderSize / actualHeight;
            int scaledHotX = (int)Math.Round(hotX * scaleX);
            int scaledHotY = (int)Math.Round(hotY * scaleY);

            IntPtr ptr = BitmapToPointer(finalBitmap, scaledHotX, scaledHotY);
            
            scaledBitmap?.Dispose();
            outlined?.Dispose();
            return ptr;
        }

        private static unsafe Bitmap AddSmoothOutline(Bitmap src, Color outlineColor)
        {
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

        private static unsafe Bitmap? RenderPointerToArgbBitmap(IntPtr hPointer, out int actualWidth, out int actualHeight)
        {
            actualWidth = 32;
            actualHeight = 32;
            
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

            NativeMethods.BITMAPINFO bmi = new() { biSize = sizeof(NativeMethods.BITMAPINFO), biWidth = actualWidth, biHeight = -actualHeight, biPlanes = 1, biBitCount = 32, biCompression = 0 };
            IntPtr hdcScreen = NativeMethods.GetDC(IntPtr.Zero);
            IntPtr hdcMem = NativeMethods.CreateCompatibleDC(hdcScreen);
            IntPtr hDib = NativeMethods.CreateDIBSection(hdcMem, ref bmi, 0, out IntPtr pBits, IntPtr.Zero, 0);

            if (hDib == IntPtr.Zero) { NativeMethods.DeleteDC(hdcMem); NativeMethods.ReleaseDC(IntPtr.Zero, hdcScreen); return null; }

            IntPtr hOld = NativeMethods.SelectObject(hdcMem, hDib);
            int byteCount = actualWidth * actualHeight * 4;
            new Span<byte>((void*)pBits, byteCount).Clear();

            const uint DI_NORMAL = 0x0003;
            NativeMethods.DrawIconEx(hdcMem, 0, 0, hPointer, actualWidth, actualHeight, 0, IntPtr.Zero, DI_NORMAL);

            Bitmap bmp = new Bitmap(actualWidth, actualHeight, PixelFormat.Format32bppArgb);
            var bmpData = bmp.LockBits(new Rectangle(0, 0, actualWidth, actualHeight), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            
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

        // PointerGraphicsFactory.cs 내 RecolorCursorStraight 메서드 수정
        private static unsafe void RecolorCursorStraight(Bitmap bmp, Color targetColor, uint ocrId)
        {
            var bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            byte* ptr = (byte*)bmpData.Scan0;
            int len = bmp.Width * bmp.Height * 4;

            for (int i = 0; i < len; i += 4)
            {
                byte a = ptr[i + 3];
                if (a == 0) continue;

                byte b = ptr[i], g = ptr[i + 1], r = ptr[i + 2];
                
                // [수정] 화살표 커서뿐만 아니라 I-Beam 커서도 targetColor의 명도를 반영하도록 부드럽게 개선
                float intensity = (r * 0.299f + g * 0.587f + b * 0.114f) / 255.0f;
                if (ocrId == NativeMethods.OCR_NORMAL)
                {
                    ptr[i] = (byte)(b + (targetColor.B - b) * intensity);
                    ptr[i + 1] = (byte)(g + (targetColor.G - g) * intensity);
                    ptr[i + 2] = (byte)(r + (targetColor.R - r) * intensity);
                }
                else // OCR_IBEAM
                {
                    // 순수 원색으로 덮되 알파 채널(투명도)이 존재하는 외곽선은 자연스럽게 유지
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
}
