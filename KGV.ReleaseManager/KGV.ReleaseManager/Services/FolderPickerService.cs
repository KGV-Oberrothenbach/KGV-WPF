using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.IO;

namespace KGV.ReleaseManager.Services;

public sealed class FolderPickerService
{
    public string? PickFolder(Window owner, string? initialDirectory)
    {
        var dialog = (IFileOpenDialog)new FileOpenDialog();

        try
        {
            dialog.GetOptions(out var options);
            options |= FOS.FOS_PICKFOLDERS | FOS.FOS_FORCEFILESYSTEM | FOS.FOS_PATHMUSTEXIST;
            dialog.SetOptions(options);

            if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            {
                if (SHCreateItemFromParsingName(initialDirectory, IntPtr.Zero, typeof(IShellItem).GUID, out var shellItem) == 0)
                {
                    dialog.SetFolder(shellItem);
                }
            }

            var hwnd = new WindowInteropHelper(owner).Handle;
            var hr = dialog.Show(hwnd);
            if (hr != 0)
            {
                return null;
            }

            dialog.GetResult(out var result);
            result.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var path);
            return path;
        }
        finally
        {
            Marshal.FinalReleaseComObject(dialog);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);

    [ComImport]
    [Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
    private class FileOpenDialog
    {
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [ComImport]
    [Guid("D57C7288-D4AD-4768-BE02-9D969532D960")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        [PreserveSig] int Show(IntPtr parent);
        void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
        void SetFileTypeIndex(uint iFileType);
        void GetFileTypeIndex(out uint piFileType);
        void Advise(IntPtr pfde, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(FOS fos);
        void GetOptions(out FOS pfos);
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
        void SetClientGuid([MarshalAs(UnmanagedType.LPStruct)] Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr pFilter);
        void GetResults(out IntPtr ppenum);
        void GetSelectedItems(out IntPtr ppsai);
    }

    [Flags]
    private enum FOS : uint
    {
        FOS_FORCEFILESYSTEM = 0x00000040,
        FOS_PICKFOLDERS = 0x00000020,
        FOS_PATHMUSTEXIST = 0x00000800,
    }

    private enum SIGDN : uint
    {
        SIGDN_FILESYSPATH = 0x80058000,
    }
}
