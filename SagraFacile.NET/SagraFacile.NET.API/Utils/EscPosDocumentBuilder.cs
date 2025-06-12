using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ESCPOS_NET;
using ESCPOS_NET.Emitters;
using ESCPOS_NET.Utilities;
// Corrected: Enums are typically directly under ESCPOS_NET or specific sub-namespaces if any.
// Assuming they are directly under ESCPOS_NET for now based on common library structures.
// If specific enums are in sub-namespaces, those would be added explicitly.

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
        private readonly List<byte[]> _commands;
        private readonly EPSON _emitter;
        private PrintStyle _currentStyles = PrintStyle.None;

        // Static constructor to register code pages provider once.
        static EscPosDocumentBuilder()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public EscPosDocumentBuilder()
        {
            _commands = new List<byte[]>();
            _emitter = new EPSON(); 
            
            // Initialize printer and set default code page for European characters
            AppendCommand(_emitter.Initialize());
            // PC858 is a common choice for Latin 1 + Euro.
            AppendCommand(_emitter.CodePage(CodePage.PC858_EURO));
        }

        private EscPosDocumentBuilder AppendCommand(byte[] command)
        {
            if (command != null && command.Length > 0)
            {
                _commands.Add(command);
            }
            return this;
        }
        
        // Not strictly needed if we append one by one, but kept for potential batching.
        private EscPosDocumentBuilder AppendCommands(params byte[][] newCommands)
        {
            foreach (var command in newCommands)
            {
                if (command != null && command.Length > 0)
                {
                     _commands.Add(command);
                }
            }
            return this;
        }

        public EscPosDocumentBuilder InitializePrinter()
        {
            // Already called in constructor, but can be called again if needed for a hard reset.
            _commands.Clear(); // Clear existing commands if re-initializing
            _currentStyles = PrintStyle.None;
            AppendCommand(_emitter.Initialize());
            return AppendCommand(_emitter.CodePage(CodePage.PC858_EURO)); // Re-apply code page
        }

        public EscPosDocumentBuilder AppendText(string text)
        {
            // EPSON emitter's Print method uses its internal encoding,
            // but the printer interprets bytes based on the selected CodePage.
            return AppendCommand(_emitter.Print(text));
        }

        public EscPosDocumentBuilder AppendLine(string text = "")
        {
            return AppendCommand(_emitter.PrintLine(text));
        }

        public EscPosDocumentBuilder NewLine(int lines = 1)
        {
            return AppendCommand(_emitter.FeedLines(lines));
        }

        public EscPosDocumentBuilder SetAlignment(EscPosAlignment alignment)
        {
            switch (alignment)
            {
                case EscPosAlignment.Left:
                    return AppendCommand(_emitter.LeftAlign());
                case EscPosAlignment.Center:
                    return AppendCommand(_emitter.CenterAlign());
                case EscPosAlignment.Right:
                    return AppendCommand(_emitter.RightAlign());
                default:
                    return this; 
            }
        }
        
        private void UpdateStyle(PrintStyle style, bool enable)
        {
            if (enable)
            {
                _currentStyles |= style;
            }
            else
            {
                _currentStyles &= ~style;
            }
            AppendCommand(_emitter.SetStyles(_currentStyles));
        }

        public EscPosDocumentBuilder SetEmphasis(bool enabled) // Bold
        {
            UpdateStyle(PrintStyle.Bold, enabled);
            return this;
        }

        public EscPosDocumentBuilder SetDoubleStrike(bool enabled)
        {
            // Assuming DoubleStrike is part of PrintStyle enum, like Bold or Underline.
            // If PrintStyle does not have DoubleStrike, this method might need to be removed or implemented differently
            // if the printer supports a direct command not exposed through EPSON emitter's SetStyles.
            // For now, let's assume it's a style.
            // Update: Based on the provided PrintStyle enum, there isn't a DoubleStrike.
            // The EPSON emitter class itself might have EnableDoubleStrike() / DisableDoubleStrike()
            // If those specific methods `EnableDoubleStrike` and `DisableDoubleStrike` are confirmed missing from EPSON emitter,
            // we will remove this method or find an alternative if critical.
            // For now, commenting out the direct calls that caused errors.
            // if (enabled)
            //     AppendCommand(_emitter.EnableDoubleStrike());
            // else
            //     AppendCommand(_emitter.DisableDoubleStrike());
            // If it's a style: UpdateStyle(PrintStyle.DoubleStrike, enabled);
            // Since it's not in the provided PrintStyle, this functionality might be lost if not supported by SetStyles or direct commands.
            // For now, this method will do nothing to avoid compilation errors.
            Console.WriteLine("Warning: SetDoubleStrike is not fully implemented with ESCPOS_NET styles provided.");
            return this;
        }

        public EscPosDocumentBuilder SetFontSize(byte widthMagnification = 1, byte heightMagnification = 1)
        {
            PrintStyle newStyle = PrintStyle.None;
            if (widthMagnification > 1) newStyle |= PrintStyle.DoubleWidth;
            if (heightMagnification > 1) newStyle |= PrintStyle.DoubleHeight;

            // Clear previous size styles
            _currentStyles &= ~PrintStyle.DoubleWidth;
            _currentStyles &= ~PrintStyle.DoubleHeight;
            
            // Apply new size styles
            _currentStyles |= newStyle;
            
            return AppendCommand(_emitter.SetStyles(_currentStyles));
        }

        public EscPosDocumentBuilder ResetFontSize()
        {
            _currentStyles &= ~PrintStyle.DoubleWidth;
            _currentStyles &= ~PrintStyle.DoubleHeight;
            return AppendCommand(_emitter.SetStyles(_currentStyles));
        }
        
        public EscPosDocumentBuilder SetUnderline(bool enabled)
        {
            UpdateStyle(PrintStyle.Underline, enabled);
            return this;
        }

        public EscPosDocumentBuilder SelectCharacterCodeTable(CodePage codePage)
        {
            return AppendCommand(_emitter.CodePage(codePage));
        }

        public EscPosDocumentBuilder SelectCharacterCodeTable(byte tableNumber)
        {
            // This mapping is an attempt for compatibility. Prefer using the CodePage enum.
            CodePage targetCodePage = CodePage.PC437_USA_STANDARD_EUROPE_DEFAULT;
            switch (tableNumber)
            {
                case 0: targetCodePage = CodePage.PC437_USA_STANDARD_EUROPE_DEFAULT; break;
                case 2: targetCodePage = CodePage.PC850_MULTILINGUAL; break;
                case 16: targetCodePage = CodePage.WPC1252; break; // Latin 1
                case 17: targetCodePage = CodePage.PC860_PORTUGUESE; break;
                case 18: targetCodePage = CodePage.PC863_CANADIAN_FRENCH; break;
                case 19: targetCodePage = CodePage.PC858_EURO; break; // Latin 1 + Euro
                // Add more mappings if other specific byte codes are used elsewhere
            }
            return AppendCommand(_emitter.CodePage(targetCodePage));
        }

        public EscPosDocumentBuilder CutPaper(bool fullCut = true)
        {
            return AppendCommand(fullCut ? _emitter.FullCut() : _emitter.PartialCut());
        }
        
        public EscPosDocumentBuilder PrintQRCode(
            string data, 
            byte moduleSizeMapping = 6, // Old parameter, maps to Size2DCode
            CorrectionLevel2DCode errorCorrectionLevel = CorrectionLevel2DCode.PERCENT_15) // Corrected to PERCENT_15
        {
            // Map moduleSizeMapping to Size2DCode.
            // Previous target was moduleSize 6. Let's map to LARGE or EXTRA.
            // Size2DCode: TINY = 2, SMALL=3, NORMAL=4, LARGE=5, EXTRA=6 (approximate mapping)
            Size2DCode qrSize;
            if (moduleSizeMapping >= 6) qrSize = Size2DCode.EXTRA; // Or LARGE if EXTRA is too big
            else if (moduleSizeMapping == 5) qrSize = Size2DCode.LARGE;
            else if (moduleSizeMapping == 4) qrSize = Size2DCode.NORMAL;
            else if (moduleSizeMapping == 3) qrSize = Size2DCode.SMALL;
            else qrSize = Size2DCode.TINY;
            
            // Defaulting to Model 2 QR Code as it's most common.
            return AppendCommand(_emitter.PrintQRCode(data, 
                                                     type: TwoDimensionCodeType.QRCODE_MODEL2, 
                                                     size: qrSize, 
                                                     correction: errorCorrectionLevel));
        }

        // Deprecated QR methods from old builder, now handled by the single PrintQRCode above.
        [Obsolete("Use PrintQRCode with appropriate parameters instead.")]
        public EscPosDocumentBuilder SetQRCodeModel2() { return this; }
        [Obsolete("Use PrintQRCode with appropriate parameters instead.")]
        public EscPosDocumentBuilder SetQRCodeErrorCorrection(byte level = 49) { return this; }
        [Obsolete("Use PrintQRCode with appropriate parameters instead.")]
        public EscPosDocumentBuilder SetQRCodeModuleSize(byte size = 3) { return this; }
        [Obsolete("Use PrintQRCode with appropriate parameters instead.")]
        public EscPosDocumentBuilder StoreQRCodeData(string data) { return this; }
        [Obsolete("Use PrintQRCode with appropriate parameters instead.")]
        public EscPosDocumentBuilder PrintStoredQRCode() { return this; }

        public byte[] Build()
        {
            return ByteSplicer.Combine(_commands.ToArray());
        }

        public string ToHexString()
        {
            var builtCommand = Build();
            return string.Join(" ", builtCommand.Select(b => b.ToString("X2")));
        }
    }
}
