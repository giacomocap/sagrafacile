using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SagraFacile.WindowsPrinterService.Printing
{
    public static class RawPrinterHelper
    {
        // Structure and API declarions for raw printing.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public class DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)] public string pDocName;
            [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile;
            [MarshalAs(UnmanagedType.LPStr)] public string pDataType;

            public DOCINFOA()
            {
                pDocName = ".NET RAW Document";
                pDataType = "RAW";
            }
        }

        [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

        [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool StartDocPrinter(IntPtr hPrinter, Int32 level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

        [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, Int32 dwCount, out Int32 dwWritten);

        public static bool SendBytesToPrinter(string szPrinterName, byte[] bytes)
        {
            IntPtr pBytes;
            Int32 dwCount;
            Int32 dwWritten = 0;

            dwCount = bytes.Length;
            pBytes = Marshal.AllocHGlobal(dwCount);
            Marshal.Copy(bytes, 0, pBytes, dwCount);

            IntPtr hPrinter = IntPtr.Zero;
            DOCINFOA di = new DOCINFOA();
            bool bSuccess = false;

            if (OpenPrinter(szPrinterName.Normalize(), out hPrinter, IntPtr.Zero))
            {
                if (StartDocPrinter(hPrinter, 1, di))
                {
                    if (StartPagePrinter(hPrinter))
                    {
                        bSuccess = WritePrinter(hPrinter, pBytes, dwCount, out dwWritten);
                        EndPagePrinter(hPrinter);
                    }
                    EndDocPrinter(hPrinter);
                }
                ClosePrinter(hPrinter);
            }

            Marshal.FreeHGlobal(pBytes);

            if (!bSuccess)
            {
                // Error logging should be handled by the caller which has the logger instance.
            }
            return bSuccess;
        }

        /// <summary>
        /// Sends raw data (string) to the specified printer.
        /// Assumes the string is ESC/POS commands that the printer understands.
        /// </summary>
        /// <param name="szPrinterName">Name of the printer.</param>
        /// <param name="rawData">The raw string data to send.</param>
        /// <returns>true if successful, false otherwise.</returns>
        public static bool SendStringToPrinter(string szPrinterName, string rawData)
        {
            // This method now uses the byte-based method for consistency.
            // It will use a default encoding. For specific encoding needs, the byte[] overload is preferred.
            byte[] bytes = Encoding.GetEncoding("ibm850").GetBytes(rawData);
            return SendBytesToPrinter(szPrinterName, bytes);
        }
    }
}
