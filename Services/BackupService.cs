using GlassBilling.Services;

namespace GlassBilling.Services;

public class BackupService
{
    private readonly DatabaseService _db;

    public BackupService(DatabaseService db)
    {
        _db = db;
    }

    /// <summary>Copy the SQLite DB file to a user-chosen location</summary>
    public async Task<string?> BackupAsync()
    {
        try
        {
            await _db.CloseAsync();

            string fileName = $"glassbilling_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db3";

#if WINDOWS
            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("SQLite Database", new List<string> { ".db3" });
            savePicker.SuggestedFileName = fileName;

            var hwnd = ((MauiWinUIWindow)Application.Current!.Windows[0].Handler.PlatformView!).WindowHandle;
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

            var file = await savePicker.PickSaveFileAsync();
            if (file is null) return null;

            File.Copy(_db.DbPath, file.Path, overwrite: true);
            return file.Path;
#elif ANDROID
            // Android: copy to Downloads folder
            string destPath = Path.Combine(
                Android.OS.Environment.GetExternalStoragePublicDirectory(
                    Android.OS.Environment.DirectoryDownloads)!.AbsolutePath,
                fileName);

            File.Copy(_db.DbPath, destPath, overwrite: true);
            return destPath;
#else
            // Mac Catalyst / iOS: copy to Documents and share
            string destPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                fileName);

            File.Copy(_db.DbPath, destPath, overwrite: true);

            // Offer share sheet so user can save it anywhere
            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Save Backup",
                File  = new ShareFile(destPath)
            });

            return destPath;
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Backup error: {ex}");
            return null;
        }
    }

    /// <summary>Replace the current DB with a backup file chosen by the user</summary>
    public async Task<bool> RestoreAsync()
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select Backup File (.db3)",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI,   new[] { ".db3" } },
                    { DevicePlatform.Android, new[] { "application/octet-stream" } },
                    { DevicePlatform.iOS,     new[] { "public.database" } },
                })
            });

            if (result is null) return false;

            await _db.CloseAsync();
            File.Copy(result.FullPath, _db.DbPath, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Restore error: {ex}");
            return false;
        }
    }
}
