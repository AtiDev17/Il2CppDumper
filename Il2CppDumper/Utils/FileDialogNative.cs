using System;
using System.Runtime.InteropServices;

namespace Il2CppDumper
{
    internal static class FileDialogNative
    {
        [ComImport]
        [ClassInterface(ClassInterfaceType.None)]
        [TypeLibType(TypeLibTypeFlags.FCanCreate)]
        [Guid(CLSIDGuid.FileOpenDialog)]
        internal class FileOpenDialogRCW
        { }

        internal static class IIDGuid
        {
            // IID GUID strings for relevant COM interfaces
            internal const string IModalWindow = "b4db1657-70d7-485e-8e3e-6fcb5a5c1802";
            internal const string IFileDialog = "42f85136-db7e-439c-85f1-e4075d135fc8";
            internal const string IFileOpenDialog = "d57c7288-d4ad-4768-be02-9d969532d960";
            internal const string IFileSaveDialog = "84bccd23-5fde-4cdb-aea4-af64b83d78ab";
            internal const string IFileDialogEvents = "973510DB-7D7F-452B-8975-74A85828D354";
            internal const string IShellItem = "43826D1E-E718-42EE-BC55-A1E261C37BFE";
            internal const string IShellItemArray = "B63EA76D-1F85-456F-A19C-48159EFA858B";
        }

        internal static class CLSIDGuid
        {
            internal const string FileOpenDialog = "DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7";
            internal const string FileSaveDialog = "C0B4E2F3-BA21-4773-8DBA-335EC946EB8B";
        }

        [ComImport()]
        [Guid(IIDGuid.IFileDialog)]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IFileDialog
        {
            [PreserveSig]
            int Show([In] IntPtr parent);

            void SetFileTypes([In] uint cFileTypes, [In][MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] rgFilterSpec);

            void SetFileTypeIndex([In] uint iFileType);

            void GetFileTypeIndex(out uint piFileType);

            void Advise([In, MarshalAs(UnmanagedType.Interface)] IFileDialogEvents pfde, out uint pdwCookie);

            void Unadvise([In] uint dwCookie);

            void SetOptions([In] FOS fos);

            void GetOptions(out FOS pfos);

            void SetDefaultFolder([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi);

            void SetFolder([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi);

            void GetFolder([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

            void GetCurrentSelection([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

            void SetFileName([In, MarshalAs(UnmanagedType.LPWStr)] string pszName);

            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);

            void SetTitle([In, MarshalAs(UnmanagedType.LPWStr)] string pszTitle);

            void SetOkButtonLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszText);

            void SetFileNameLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);

            void GetResult([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

            void AddPlace([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, int alignment);

            void SetDefaultExtension([In, MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);

            void Close([MarshalAs(UnmanagedType.Error)] int hr);

            void SetClientGuid([In] ref Guid guid);

            void ClearClientData();

            void SetFilter([MarshalAs(UnmanagedType.Interface)] IntPtr pFilter);
        }

        [ComImport,
        Guid(IIDGuid.IFileDialogEvents),
        InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IFileDialogEvents
        {
            // NOTE: some of these callbacks are cancelable - returning S_FALSE means that 
            // the dialog should not proceed (e.g. with closing, changing folder); to 
            // support this, we need to use the PreserveSig attribute to enable us to return
            // the proper HRESULT
            [PreserveSig]
            int OnFileOk([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd);

            [PreserveSig]
            int OnFolderChanging([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd, [In, MarshalAs(UnmanagedType.Interface)] IShellItem psiFolder);

            void OnFolderChange([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd);

            void OnSelectionChange([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd);

            void OnShareViolation([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd, [In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, out FDE_SHAREVIOLATION_RESPONSE pResponse);

            void OnTypeChange([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd);

            void OnOverwrite([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd, [In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, out FDE_OVERWRITE_RESPONSE pResponse);
        }

        [ComImport,
        Guid(IIDGuid.IShellItem),
        InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IShellItem
        {
            void BindToHandler([In, MarshalAs(UnmanagedType.Interface)] IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppv);

            void GetParent([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

            void GetDisplayName([In] SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);

            void GetAttributes([In] uint sfgaoMask, out uint psfgaoAttribs);

            void Compare([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, [In] uint hint, out int piOrder);
        }

        internal enum SIGDN : uint
        {
            NORMALDISPLAY = 0x00000000,           // SHGDN_NORMAL
            PARENTRELATIVEPARSING = 0x80018001,   // SHGDN_INFOLDER | SHGDN_FORPARSING
            DESKTOPABSOLUTEPARSING = 0x80028000,  // SHGDN_FORPARSING
            PARENTRELATIVEEDITING = 0x80031001,   // SHGDN_INFOLDER | SHGDN_FOREDITING
            DESKTOPABSOLUTEEDITING = 0x8004c000,  // SHGDN_FORPARSING | SHGDN_FORADDRESSBAR
            FILESYSPATH = 0x80058000,             // SHGDN_FORPARSING
            URL = 0x80068000,                     // SHGDN_FORPARSING
            PARENTRELATIVEFORADDRESSBAR = 0x8007c001,     // SHGDN_INFOLDER | SHGDN_FORPARSING | SHGDN_FORADDRESSBAR
            PARENTRELATIVE = 0x80080001           // SHGDN_INFOLDER
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto, Pack = 4)]
        internal struct COMDLG_FILTERSPEC
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            internal string pszName;
            [MarshalAs(UnmanagedType.LPWStr)]
            internal string pszSpec;
        }

        [Flags]
        internal enum FOS : uint
        {
            OVERWRITEPROMPT = 0x00000002,
            STRICTFILETYPES = 0x00000004,
            NOCHANGEDIR = 0x00000008,
            PICKFOLDERS = 0x00000020,
            FORCEFILESYSTEM = 0x00000040, // Ensure that items returned are filesystem items.
            ALLNONSTORAGEITEMS = 0x00000080, // Allow choosing items that have no storage.
            NOVALIDATE = 0x00000100,
            ALLOWMULTISELECT = 0x00000200,
            PATHMUSTEXIST = 0x00000800,
            FILEMUSTEXIST = 0x00001000,
            CREATEPROMPT = 0x00002000,
            SHAREAWARE = 0x00004000,
            NOREADONLYRETURN = 0x00008000,
            NOTESTFILECREATE = 0x00010000,
            HIDEMRUPLACES = 0x00020000,
            HIDEPINNEDPLACES = 0x00040000,
            NODEREFERENCELINKS = 0x00100000,
            DONTADDTORECENT = 0x02000000,
            FORCESHOWHIDDEN = 0x10000000,
            DEFAULTNOMINIMODE = 0x20000000
        }

        internal enum FDE_SHAREVIOLATION_RESPONSE
        {
            DEFAULT = 0x00000000,
            ACCEPT = 0x00000001,
            REFUSE = 0x00000002
        }

        internal enum FDE_OVERWRITE_RESPONSE
        {
            DEFAULT = 0x00000000,
            ACCEPT = 0x00000001,
            REFUSE = 0x00000002
        }
    }
}
