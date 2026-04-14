using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace BlendHub.Helpers
{
    public static class WindowHelper
    {
        public static void InitializeWithWindow(object obj)
        {
            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            WinRT.Interop.InitializeWithWindow.Initialize(obj, hwnd);
        }

        public static async Task<StorageFolder?> PickFolderAsync(PickerLocationId startLocation = PickerLocationId.DocumentsLibrary)
        {
            var picker = new FolderPicker();
            InitializeWithWindow(picker);
            picker.SuggestedStartLocation = startLocation;
            picker.FileTypeFilter.Add("*");
            return await picker.PickSingleFolderAsync();
        }
    }
}
