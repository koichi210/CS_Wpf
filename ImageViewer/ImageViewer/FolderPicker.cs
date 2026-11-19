using System;
using System.Runtime.InteropServices;

namespace ImageViewer
{
    // エクスプローラー形式(パスの貼り付けができる)のフォルダ選択ダイアログ。
    // ImageViewer2019はWindowsAPICodePackのCommonOpenFileDialogを使っているが、こちらは.NET Framework 4.0で
    // NuGetのdllを足さずに済むよう、Windows(Vista以降)のIFileOpenDialogを直接P/Invokeで呼んでいる
    public static class FolderPicker
    {
        private const uint _fosPickFolders = 0x00000020;
        private const uint _fosForceFileSystem = 0x00000040;
        private const uint _sigdnFileSysPath = 0x80058000;
        private const int _errorCancelled = unchecked((int)0x800704C7);

        [ComImport]
        [Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
        private class FileOpenDialogRcw
        {
        }

        [ComImport]
        [Guid("42f85136-db7e-439c-85f1-e4075d135fc8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [PreserveSig]
            int Show(IntPtr parent);
            void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
            void SetFileTypeIndex(uint iFileType);
            void GetFileTypeIndex(out uint piFileType);
            void Advise(IntPtr pfde, out uint pdwCookie);
            void Unadvise(uint dwCookie);
            void SetOptions(uint fos);
            void GetOptions(out uint pfos);
            void SetDefaultFolder(IShellItem psi);
            void SetFolder(IShellItem psi);
            void GetFolder(out IShellItem ppsi);
            void GetCurrentSelection(out IShellItem ppsi);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
            void GetResult(out IShellItem ppsi);
            void AddPlace(IShellItem psi, int fdap);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
            void Close(int hr);
            void SetClientGuid([In] ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr pFilter);
            void GetResults(out IntPtr ppenum);
            void GetSelectedItems(out IntPtr ppsai);
        }

        [ComImport]
        [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string path,
            IntPtr pbc,
            [In] ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);

        // フォルダを選ばせる。選ばれたらtrueとそのパス、キャンセルならfalse。
        // initialFolderが実在すれば、そこを開いた状態で始める
        public static bool TrySelectFolder(IntPtr owner, string title, string initialFolder, out string selectedPath)
        {
            selectedPath = null;
            IFileOpenDialog dialog = (IFileOpenDialog)new FileOpenDialogRcw();
            try
            {
                uint options;
                dialog.GetOptions(out options);
                dialog.SetOptions(options | _fosPickFolders | _fosForceFileSystem);
                if (!string.IsNullOrEmpty(title))
                {
                    dialog.SetTitle(title);
                }

                if (!string.IsNullOrEmpty(initialFolder) && System.IO.Directory.Exists(initialFolder))
                {
                    Guid shellItemGuid = typeof(IShellItem).GUID;
                    IShellItem folder;
                    if (SHCreateItemFromParsingName(initialFolder, IntPtr.Zero, ref shellItemGuid, out folder) == 0 && folder != null)
                    {
                        dialog.SetFolder(folder);
                        Marshal.ReleaseComObject(folder);
                    }
                }

                int hr = dialog.Show(owner);
                if (hr == _errorCancelled)
                {
                    return false;
                }
                Marshal.ThrowExceptionForHR(hr);

                IShellItem result;
                dialog.GetResult(out result);
                try
                {
                    result.GetDisplayName(_sigdnFileSysPath, out selectedPath);
                }
                finally
                {
                    Marshal.ReleaseComObject(result);
                }
                return !string.IsNullOrEmpty(selectedPath);
            }
            finally
            {
                Marshal.ReleaseComObject(dialog);
            }
        }
    }
}
