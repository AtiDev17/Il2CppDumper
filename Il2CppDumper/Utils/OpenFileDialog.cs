using System;
using static Il2CppDumper.FileDialogNative;

namespace Il2CppDumper
{
    internal sealed class OpenFileDialog
    {
        private static readonly char[] FilterSeparators = ['|'];

        public string Title { get; set; }
        public string Filter { get; set; }
        public string FileName { get; set; }

        public bool ShowDialog()
        {
            var dialog = (IFileDialog)new FileOpenDialogRCW();
            dialog.GetOptions(out var options);
            options |= FOS.FORCEFILESYSTEM | FOS.NOVALIDATE | FOS.DONTADDTORECENT;
            dialog.SetOptions(options);
            if (!string.IsNullOrEmpty(Title))
            {
                dialog.SetTitle(Title);
            }
            if (!string.IsNullOrEmpty(Filter))
            {
                string[] filterElements = Filter.Split(FilterSeparators);
                COMDLG_FILTERSPEC[] filter = new COMDLG_FILTERSPEC[filterElements.Length / 2];
                for (int x = 0; x < filterElements.Length; x += 2)
                {
                    filter[x / 2].pszName = filterElements[x];
                    filter[x / 2].pszSpec = filterElements[x + 1];
                }
                dialog.SetFileTypes((uint)filter.Length, filter);
            }
            if (dialog.Show(IntPtr.Zero) == 0)
            {
                dialog.GetResult(out var shellItem);
                shellItem.GetDisplayName(SIGDN.FILESYSPATH, out var ppszName);
                FileName = ppszName;
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    internal sealed class FolderBrowserDialog
    {
        public string Description { get; set; }
        public string SelectedPath { get; set; }

        public bool ShowDialog()
        {
            var dialog = (IFileDialog)new FileOpenDialogRCW();
            dialog.GetOptions(out var options);
            options |= FOS.PICKFOLDERS | FOS.FORCEFILESYSTEM | FOS.NOVALIDATE | FOS.DONTADDTORECENT;
            dialog.SetOptions(options);
            if (!string.IsNullOrEmpty(Description))
            {
                dialog.SetTitle(Description);
            }
            if (dialog.Show(IntPtr.Zero) == 0)
            {
                dialog.GetResult(out var shellItem);
                shellItem.GetDisplayName(SIGDN.FILESYSPATH, out var ppszName);
                SelectedPath = ppszName;
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}