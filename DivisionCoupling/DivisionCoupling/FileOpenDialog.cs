using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DivisionCoupling
{
    /// <summary>
    /// MFC版BrowseFileの CFileDialog(TRUE, ..., OFN_CREATEPROMPT, ...) と同じ「開く」ダイアログ。
    /// Microsoft.Win32.OpenFileDialog と WinForms の OpenFileDialog には OFN_CREATEPROMPT を指定する手段が無い
    /// （CreatePrompt があるのは SaveFileDialog だけで、そちらは「保存」ダイアログになってしまう）ので、
    /// GetOpenFileName を直接呼ぶ。存在しないファイル名で「開く」を押すと Windows 自身が
    /// 「(ファイル名) このファイルは存在しません。作成しますか?」を出し、「いいえ」ならダイアログに戻る。
    /// 文言も動きも Windows 標準のままなので、MFC版と同じになる（ファイル自体は作らない）。
    /// </summary>
    internal static class FileOpenDialog
    {
        // MFC版のフィルタ（'|'区切り）
        public const String Filter = "*.*|*.*|全て(*.*)|*.*";

        internal const int OFN_HIDEREADONLY = 0x00000004;
        internal const int OFN_ENABLEHOOK = 0x00000020;
        internal const int OFN_PATHMUSTEXIST = 0x00000800;
        internal const int OFN_FILEMUSTEXIST = 0x00001000;
        internal const int OFN_CREATEPROMPT = 0x00002000;
        internal const int OFN_EXPLORER = 0x00080000;

        // MFC版はVistaスタイル(IFileOpenDialog)で、FILEMUSTEXIST/PATHMUSTEXISTは外し、CREATEPROMPTだけ付けている。
        // フックを付けると古い見た目のダイアログになるので付けない（フック無しならWindowsがVistaスタイルで出す）。
        // MFC版のVistaスタイルには「読み取り専用」のチェックが無いので、HIDEREADONLYで隠して揃える
        internal const int Flags = OFN_EXPLORER | OFN_CREATEPROMPT | OFN_HIDEREADONLY;

        // 長いパスでも選べるよう、ファイル名の受け取りは大きめに取る（足りないとダイアログが失敗する）
        internal const int FileBufferLength = 32768;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct OPENFILENAME
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public IntPtr lpstrFilter;
            public IntPtr lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public IntPtr lpstrFile;
            public int nMaxFile;
            public IntPtr lpstrFileTitle;
            public int nMaxFileTitle;
            public IntPtr lpstrInitialDir;
            public IntPtr lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public IntPtr lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public IntPtr lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int FlagsEx;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetOpenFileNameW")]
        private static extern bool GetOpenFileName(ref OPENFILENAME ofn);

        // '|'区切りを、GetOpenFileNameが受け取る'\0'区切りにする（MFCのCFileDialogと同じ変換）。
        // 最後の'\0'の後ろにはマーシャリングの終端'\0'が付くので、末尾は'\0\0'になる
        internal static String ToNativeFilter(String filter)
        {
            return filter.Replace('|', '\0') + "\0";
        }

        /// <summary>
        /// ダイアログを出し、選ばれたファイルのフルパスを返す。キャンセル（または失敗）なら null。
        /// </summary>
        public static String Show(Window owner)
        {
            IntPtr filter = Marshal.StringToHGlobalUni(ToNativeFilter(Filter));
            IntPtr file = Marshal.AllocHGlobal(FileBufferLength * sizeof(char));
            try
            {
                // MFC版と同じく、毎回ファイル名は空で開く
                Marshal.WriteInt16(file, 0);

                OPENFILENAME ofn = new OPENFILENAME
                {
                    lStructSize = Marshal.SizeOf(typeof(OPENFILENAME)),
                    hwndOwner = owner != null ? new WindowInteropHelper(owner).Handle : IntPtr.Zero,
                    lpstrFilter = filter,
                    nFilterIndex = 1,
                    lpstrFile = file,
                    nMaxFile = FileBufferLength,
                    Flags = Flags,
                };
                if (!GetOpenFileName(ref ofn))
                {
                    return null;
                }
                return Marshal.PtrToStringUni(file);
            }
            finally
            {
                Marshal.FreeHGlobal(file);
                Marshal.FreeHGlobal(filter);
            }
        }
    }
}
