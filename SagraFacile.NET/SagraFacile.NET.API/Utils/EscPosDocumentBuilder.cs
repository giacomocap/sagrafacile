using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SagraFacile.NET.API.Utils
{
    public enum EscPosAlignment
    {
        Left = 0,
        Center = 1,
        Right = 2
    }

    public class EscPosDocumentBuilder
    {
        private readonly List<byte> _buffer;
        private readonly Encoding _encoding;

        // Standard ESC/POS Commands (Hex or Decimal)
        private const byte ESC = 0x1B; // Escape
        private const byte GS = 0x1D;  // Group Separator
        private const byte LF = 0x0A;  // Line Feed

        public EscPosDocumentBuilder(string encodingName = "ibm858")
        {
            _buffer = new List<byte>();
            try
            {
                // Register the CodePages provider if not already registered.
                // This is necessary for .NET Core/5+ to support legacy encodings.
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                _encoding = Encoding.GetEncoding(encodingName);
            }
            catch (Exception)
            {
                // Fallback to a default if the specified one is not found
                _encoding = Encoding.ASCII;
            }
            InitializePrinter();
        }

        private EscPosDocumentBuilder AppendBytes(params byte[] bytes)
        {
            _buffer.AddRange(bytes);
            return this;
        }

        public EscPosDocumentBuilder InitializePrinter()
        {
            return AppendBytes(ESC, (byte)'@');
        }

        public EscPosDocumentBuilder AppendText(string text)
        {
            _buffer.AddRange(_encoding.GetBytes(text));
            return this;
        }

        public EscPosDocumentBuilder AppendLine(string text = "")
        {
            AppendText(text);
            return AppendBytes(LF);
        }

        public EscPosDocumentBuilder NewLine(int lines = 1)
        {
            for (int i = 0; i < lines; i++)
            {
                AppendBytes(LF);
            }
            return this;
        }

        public EscPosDocumentBuilder SetAlignment(EscPosAlignment alignment)
        {
            return AppendBytes(ESC, (byte)'a', (byte)alignment);
        }

        public EscPosDocumentBuilder SetEmphasis(bool enabled)
        {
            return AppendBytes(ESC, (byte)'E', (byte)(enabled ? 1 : 0));
        }

        public EscPosDocumentBuilder SetDoubleStrike(bool enabled)
        {
            return AppendBytes(ESC, (byte)'G', (byte)(enabled ? 1 : 0));
        }

        public EscPosDocumentBuilder SetFontSize(byte widthMagnification = 1, byte heightMagnification = 1)
        {
            widthMagnification = Math.Max((byte)1, Math.Min(widthMagnification, (byte)8));
            heightMagnification = Math.Max((byte)1, Math.Min(heightMagnification, (byte)8));
            byte size = (byte)(((widthMagnification - 1) << 4) | (heightMagnification - 1));
            return AppendBytes(GS, (byte)'!', size);
        }

        public EscPosDocumentBuilder ResetFontSize()
        {
            return AppendBytes(GS, (byte)'!', 0);
        }

        public EscPosDocumentBuilder SelectCharacterCodeTable(byte tableNumber)
        {
            // ESC t n: Select character code table
            // n can range from 0-5, 16-19 for common ones.
            // 19 is often PC858 (Multilingual Latin I + Euro)
            return AppendBytes(ESC, (byte)'t', tableNumber);
        }

        public EscPosDocumentBuilder CutPaper(bool fullCut = true)
        {
            return AppendBytes(GS, (byte)'V', (byte)(fullCut ? 0 : 1));
        }

        // QR Code Commands
        private const byte fn_QR_MODEL = 0x41;
        private const byte fn_QR_ERROR_CORRECTION = 0x45;
        private const byte fn_QR_MODULE_SIZE = 0x43;
        private const byte fn_QR_STORE_DATA = 0x50;
        private const byte fn_QR_PRINT = 0x51;

        public EscPosDocumentBuilder SetQRCodeModel2()
        {
            // pL was 4, but there are only 3 bytes of data (cn, fn, m). Corrected to 3.
            return AppendBytes(GS, (byte)'(', (byte)'k', 3, 0, 49, fn_QR_MODEL, 49);
        }

        public EscPosDocumentBuilder SetQRCodeErrorCorrection(byte level = 49) // L=48, M=49, Q=50, H=51
        {
            return AppendBytes(GS, (byte)'(', (byte)'k', 3, 0, 49, fn_QR_ERROR_CORRECTION, level);
        }

        public EscPosDocumentBuilder SetQRCodeModuleSize(byte size = 3)
        {
            size = Math.Max((byte)1, Math.Min(size, (byte)16));
            return AppendBytes(GS, (byte)'(', (byte)'k', 3, 0, 49, fn_QR_MODULE_SIZE, size);
        }

        public EscPosDocumentBuilder StoreQRCodeData(string data)
        {
            // Important: QR data should be stored using an encoding the scanner expects, typically UTF-8 or ASCII.
            // Here we use ASCII for broad compatibility. For international characters, UTF-8 might be needed
            // but ensure the printer supports it for QR codes.
            byte[] dataBytes = Encoding.ASCII.GetBytes(data);
            int len = dataBytes.Length + 3;
            byte pL = (byte)(len % 256);
            byte pH = (byte)(len / 256);

            var command = new List<byte> { GS, (byte)'(', (byte)'k', pL, pH, 49, fn_QR_STORE_DATA, 48 };
            command.AddRange(dataBytes);
            _buffer.AddRange(command);
            return this;
        }

        public EscPosDocumentBuilder PrintStoredQRCode()
        {
            return AppendBytes(GS, (byte)'(', (byte)'k', 3, 0, 49, fn_QR_PRINT, 48);
        }

        public EscPosDocumentBuilder PrintQRCode(string data, byte moduleSize = 3, byte errorCorrectionLevel = 49)
        {
            SetQRCodeModel2();
            SetQRCodeModuleSize(moduleSize);
            SetQRCodeErrorCorrection(errorCorrectionLevel);
            StoreQRCodeData(data);
            PrintStoredQRCode();
            return this;
        }

        public byte[] Build()
        {
            return _buffer.ToArray();
        }

        public string ToHexString()
        {
            return string.Join(" ", _buffer.Select(b => b.ToString("X2")));
        }
    }
}
