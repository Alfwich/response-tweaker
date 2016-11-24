using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.Storage.Pickers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using Windows.UI.Xaml.Data;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.ViewManagement;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace response_tweaker
{
    public sealed partial class MainPage
    {
        private const int SavedInfoMessageDisplaySeconds = 8;
        private const string RecentFilesKey = "recent-files-02";

        public MainPageViewModel ViewModel { get; set; }
        private RequestFileContents _requestFileContents;
        private ApplicationDataContainer localSettings;

        public MainPage()
        {
            ViewModel = new MainPageViewModel();
            InitializeComponent();
            localSettings = ApplicationData.Current.LocalSettings;
            SyncRecentFilesContainerWithViewModel(GetRecentFilesContainer());
        }

        private List<string> GetRecentFilesContainer()
        {
            string containerJson = localSettings.Values[RecentFilesKey] as string;
            var list = new List<string>();

            if (string.IsNullOrWhiteSpace(containerJson))
            {
                localSettings.Values[RecentFilesKey] = JsonConvert.SerializeObject(list);
                return list;
            }

            var jsonList = JsonConvert.DeserializeObject(containerJson) as JArray;
            foreach (var item in jsonList)
            {
                list.Add(item.ToString());
            }

            return list;
        }

        private void SyncRecentFilesContainerWithViewModel(List<string> list)
        {
            ViewModel.RecentFiles.Clear();
            ViewModel.HasRecentFiles = list.Count > 0;
            foreach (string item in list)
            {
                ViewModel.RecentFiles.Add(new RecentFileModel
                {
                    Path = item
                });
            }
        }

        private void SavePathToRecentFiles(string path)
        {
            var container = GetRecentFilesContainer();
            var fileName = path?.Split('\\').Last();
            if (!string.IsNullOrWhiteSpace(fileName) && container != null)
            {
                if (container.Contains(fileName))
                {
                    container.Remove(fileName);
                }

                container.Insert(0, fileName);
                localSettings.Values[RecentFilesKey] = JsonConvert.SerializeObject(container);
                SyncRecentFilesContainerWithViewModel(container);
            }
        }

        private void RemovePathFromRecentFiles(string path)
        {
            var container = GetRecentFilesContainer();
            var fileName = path?.Split('\\').Last();
            if (container != null && container.Contains(fileName))
            {
                container.Remove(path);
                localSettings.Values[RecentFilesKey] = JsonConvert.SerializeObject(container);
                SyncRecentFilesContainerWithViewModel(container);
            }
        }

        private void ClearRecentFilesList()
        {
            localSettings.Values[RecentFilesKey] = null;
            var container = GetRecentFilesContainer();
            SyncRecentFilesContainerWithViewModel(container);
        }

        private FileOpenPicker GetFilePicker()
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add(".txt");

            return picker;
        }
        private async Task<StorageFile> GetFile()
        {
            var picker = GetFilePicker();
            return await picker.PickSingleFileAsync();
        }

        private async Task SetOpenedFileState(StorageFile file)
        {
            ViewModel.FileNamePath = file.Path;
            ViewModel.WebFileNamePath = string.Empty;
            _requestFileContents = await RequestFileContents.ReadFileContents(file);
            ViewModel.OriginalFileContent = _requestFileContents.Payload;
            ViewModel.SourceObject = _requestFileContents.GetPayloadObject();
            ViewModel.WebErrorText = string.Empty;
            ViewModel.IsSaveEnabled = true;
            UpdateTitle(GetFileName());
            ViewModel.ShowInfoMessage($"Opened filed: {ViewModel.FileNamePath}");
        }

        private async Task SetOpenedWebResourceState(WebResponse response)
        {
            ViewModel.FileNamePath = response.ResponseUri.ToString();
            ViewModel.WebFileNamePath = string.Empty;
            _requestFileContents = await RequestFileContents.ReadResponseContents(response);
            ViewModel.OriginalFileContent = _requestFileContents.Payload;
            ViewModel.SourceObject = _requestFileContents.GetPayloadObject();
            ViewModel.WebErrorText = string.Empty;
            ViewModel.IsSaveEnabled = true;
            UpdateTitle(GetFileName());
            ViewModel.ShowInfoMessage($"Opened web resource: {ViewModel.FileNamePath}");
        }

        private void SetClosedOrFailedFileState()
        {
            ViewModel.FileNamePath = string.Empty;
            ViewModel.OriginalFileContent = string.Empty;
            ViewModel.WebErrorText = string.Empty;
            UpdateTitle(string.Empty);
            _requestFileContents = null;
            ViewModel.SourceObject = null;
            ViewModel.ShowInfoMessage("Failed to open resource");
            ViewModel.IsSaveEnabled = false;
        }

        private async void LoadFileButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ViewModel.WebFileNamePath))
            {

                var file = await GetFile();
                if (file != null)
                {
                    await SetOpenedFileState(file);
                }
                else
                {
                    SetClosedOrFailedFileState();
                }
            }
            else
            {
                var url = ViewModel.WebFileNamePath;

                try
                {
                    var request = WebRequest.Create(url);
                    request.Method = "GET";

                    using (var response = await request.GetResponseAsync())
                    {
                        if (response != null)
                        {
                            await SetOpenedWebResourceState(response);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to acquire json resource url: {ex.Message}");
                    SetClosedOrFailedFileState();
                }
            }
        }

        private string _lastSavedFilePath;
        private async Task SaveDataToFile(string fileName, RequestFileContents requestData, bool retryOnFailure = true)
        {
            var folder = ApplicationData.Current.LocalFolder;
            var prefix = string.Empty;
            prefix = ViewModel.SavedFilePrefix.Length == 0
                ? string.Empty
                : $"{ViewModel.SavedFilePrefix}.";

            try
            {
                var file = await folder.CreateFileAsync($"{prefix}{fileName}", CreationCollisionOption.ReplaceExisting);
                if (file != null)
                {
                    await FileIO.WriteTextAsync(file, requestData.ToString());
                    _lastSavedFilePath = file.Path;
                    SavePathToRecentFiles(file.Path);
                    ViewModel.ShowInfoMessage($"Saved to disk: {file.Path}", SavedInfoMessageDisplaySeconds);
                    ViewModel.IsClipboardCopyEnabled = true;
                    return;
                }

            }
            catch (Exception) when (retryOnFailure)
            {
                // Attempt to retry writing the file if it fails using a plain name
                ViewModel.SavedFilePrefix = string.Empty;
                await SaveDataToFile("request.txt", requestData, false);
                return;
            }

            ViewModel.ShowInfoMessage("Failed to save file to disk");
        }

        private string GetWebFileName()
        {
            var fileName = ViewModel.FileNamePath;
            fileName = fileName.Replace('/', '.');
            fileName = fileName.Replace(":", "");
            fileName = fileName.Replace("https", "");
            fileName = fileName.Replace("http", "");
            while (fileName.IndexOf("..") != -1)
            {
                fileName = fileName.Replace("..", ".");
            }

            while (fileName.Length > 0 && fileName[fileName.Length - 1] == '.')
            {
                fileName = fileName.Substring(0, fileName.Length - 1);
            }

            while (fileName.Length > 0 && fileName[0] == '.')
            {
                fileName = fileName.Substring(1);
            }

            return $"{fileName}.txt";
        }

        private string GetLocalFileName()
        {
            return ViewModel.FileNamePath.Split('\\').Last();
        }

        private async void JObjectViewer_OnObjectUpdated(object sender, ObjectUpdatedEventArgs e)
        {
            _requestFileContents.UpdatePayload(ViewModel.GetSourceObjectAsJson());
            await SaveDataToFile(GetFileName(), _requestFileContents);
        }

        private async void Prefix_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_requestFileContents != null)
            {
                await Task.Delay(200);
                JObjectViewer_OnObjectUpdated(null, null);
            }
        }

        private void PaneToggle_Click(object sender, RoutedEventArgs e)
        {
        }

        private void CopyToClipboard_Click(object sender, RoutedEventArgs e)
        {
            var dp = new DataPackage();
            dp.SetText(_lastSavedFilePath);
            Clipboard.SetContent(dp);
            ViewModel.ShowInfoMessage("Copied path to clipboard");
            ViewModel.IsClipboardCopyEnabled = false;
        }

        private string GetFileName()
        {
            return ViewModel.FileNamePath.Contains("http")
                    ? GetWebFileName()
                    : GetLocalFileName();
        }

        private void UpdateTitle(string fileName, string path = null)
        {
            ApplicationView appView = ApplicationView.GetForCurrentView();
            string title = string.IsNullOrWhiteSpace(path) ? fileName : $"{fileName} > {path}";
            appView.Title = title;
        }

        private void JObjectViewer_PathChanged(object sender, PathChangedEventArgs e)
        {
            UpdateTitle(GetFileName(), e.NewPath);
        }

        private async void SaveFileButton_Click(object sender, RoutedEventArgs e)
        {
            await SaveDataToFile(GetFileName(), _requestFileContents);
        }

        private async void ListView_ItemClick(object sender, Windows.UI.Xaml.Controls.ItemClickEventArgs e)
        {
            var recentModel = e.ClickedItem as RecentFileModel;
            var storageFolder = ApplicationData.Current.LocalFolder;
            try
            {
                var file = await storageFolder.CreateFileAsync(recentModel.Path, CreationCollisionOption.OpenIfExists);
                await SetOpenedFileState(file);
                SavePathToRecentFiles(recentModel.Path);
            }
            catch (Exception)
            {
                ViewModel.ShowInfoMessage($"Failed to open recent file: {recentModel.Label}");
                RemovePathFromRecentFiles(recentModel.Path);
            }
        }

        private void ClearRecentFilesList_Click(object sender, RoutedEventArgs e)
        {
            ClearRecentFilesList();
        }
    }

    public class RecentFileModel
    {
        private const int MaxLabelLength = 40;
        public string Path { get; set; }
        public string Label => Path.Length > MaxLabelLength
            ? $"...{Path.Substring(Path.Length - MaxLabelLength)}"
            : Path;
    }

    public class MainPageViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private object _sourceObject;
        public object SourceObject
        {
            get
            {
                return _sourceObject;
            }
            set
            {
                _sourceObject = value;
                OnPropertyChanged();
            }
        }

        private string _fileNamePath;
        public string FileNamePath
        {
            get
            {
                return _fileNamePath;
            }
            set
            {
                _fileNamePath = value;
                OnPropertyChanged();
            }
        }

        private string _webErrorText = string.Empty;
        public string WebErrorText
        {
            get
            {
                return _webErrorText;
            }
            set
            {
                _webErrorText = value;
                OnPropertyChanged();
            }
        }

        private string _webFileNamePath = string.Empty;
        public string WebFileNamePath
        {
            get
            {
                return _webFileNamePath;
            }
            set
            {
                _webFileNamePath = value;
                OnPropertyChanged();
            }
        }

        private string _infoText;
        public string InfoText
        {
            get
            {
                return _infoText;
            }
            set
            {
                _infoText = value;
                OnPropertyChanged();
            }
        }

        private bool _isSaveEnabled = false;
        public bool IsSaveEnabled
        {
            get
            {
                return _isSaveEnabled;
            }
            set
            {
                _isSaveEnabled = value;
                OnPropertyChanged();
            }
        }

        private bool _isClipboardCopyEnabled = false;
        public bool IsClipboardCopyEnabled
        {
            get
            {
                return _isClipboardCopyEnabled;
            }
            set
            {
                _isClipboardCopyEnabled = value;
                OnPropertyChanged();
            }
        }

        private string _originalFileContent;
        public string OriginalFileContent
        {
            get
            {
                return _originalFileContent;
            }
            set
            {
                _originalFileContent = value;
                OnPropertyChanged();
            }
        }

        private string _savedFilePrefix = string.Empty;
        public string SavedFilePrefix
        {
            get
            {
                return _savedFilePrefix;
            }
            set
            {
                _savedFilePrefix = value;
                OnPropertyChanged();
            }
        }

        public string GetSourceObjectAsJson()
        {
            var sourceJObject = _sourceObject as JObject;
            if (sourceJObject != null)
            {
                return JsonConvert.SerializeObject(sourceJObject);
            }

            var sourceJArray = _sourceObject as JArray;
            if (sourceJArray != null)
            {
                return JsonConvert.SerializeObject(sourceJArray);
            }

            return string.Empty;
        }

        private DispatcherTimer _hideTimer;
        private void HideInfoMessage(object s, object e)
        {
            InfoText = string.Empty;
            IsClipboardCopyEnabled = false;
            _hideTimer.Stop();
            _hideTimer = null;
        }

        public void ShowInfoMessage(string msg, int displaySeconds = 3)
        {
            if (_hideTimer != null)
            {
                _hideTimer.Stop();
                _hideTimer = null;
            }

            InfoText = msg;
            _hideTimer = new DispatcherTimer
            {
                Interval = new TimeSpan(0, 0, displaySeconds)
            };
            _hideTimer.Tick += HideInfoMessage;
            _hideTimer.Start();

        }

        public ObservableCollection<RecentFileModel> RecentFiles = new ObservableCollection<RecentFileModel>();

        private bool _hasRecentFiles = false;
        public bool HasRecentFiles
        {
            get
            {
                return _hasRecentFiles;
            }
            set
            {
                _hasRecentFiles = value;
                OnPropertyChanged();
            }
        }
    }

    public class RequestFileContents
    {
        public static async Task<RequestFileContents> ReadResponseContents(WebResponse response)
        {
            var resultRequestFileContents = new RequestFileContents();
            for (int i = 0; i < response.Headers.Count; ++i)
            {
                var key = response.Headers.AllKeys[i];
                var value = response.Headers[key];
                resultRequestFileContents.Headers += $"{key}: {value}\n";
            }

            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    resultRequestFileContents.Payload += await reader.ReadLineAsync();
                }
            }

            return resultRequestFileContents;
        }

        public static async Task<RequestFileContents> ReadFileContents(StorageFile file)
        {
            var resultRequestFileContents = new RequestFileContents();
            var hasHitPayload = false;

            using (var inputStream = await file.OpenReadAsync())
            using (var classicsStream = inputStream.AsStreamForRead())
            using (var streamReader = new StreamReader(classicsStream))
            {
                while (streamReader.Peek() > 0)
                {
                    var line = $"{streamReader.ReadLine()}\n";
                    if (line == "\n")
                    {
                        hasHitPayload = true;
                        continue;
                    }

                    if (hasHitPayload)
                    {
                        resultRequestFileContents.Payload += line;
                    }
                    else
                    {
                        resultRequestFileContents.Headers += line;
                    }
                }
            }

            return resultRequestFileContents;
        }

        public string Headers { get; private set; } = string.Empty;
        public string Payload { get; private set; } = string.Empty;


        public object GetPayloadObject()
        {
            try
            {
                if (Payload.Trim()[0] == '[')
                {
                    return JArray.Parse(Payload);
                }

                return JObject.Parse(Payload);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to deserialize object: {Payload}\nmessage: {ex.Message}");
                return null;
            }
        }

        public void UpdatePayload(string newPayload)
        {
            Payload = newPayload;
            UpdateContentLengthHeader(newPayload.Length);
        }

        private const string ContentLengthHeaderKey = "content-length";
        private void UpdateContentLengthHeader(int newLength)
        {
            var contentLengthPosition = Headers.ToLower().IndexOf(ContentLengthHeaderKey, StringComparison.Ordinal);
            if (contentLengthPosition != -1)
            {
                var startPos = contentLengthPosition + ContentLengthHeaderKey.Length;
                while (startPos < Headers.Length && (Headers[startPos] == ':' || Headers[startPos] == ' '))
                {
                    startPos++;
                }

                var endPos = startPos;
                while (endPos < Headers.Length && char.IsNumber(Headers[endPos]))
                {
                    endPos++;
                }

                if (startPos >= Headers.Length || endPos >= Headers.Length)
                {
                    return;
                }

                Headers = Headers.Substring(0, startPos) + newLength + Headers.Substring(endPos);
            }
            else
            {
                Headers = Headers + $"Content-Length: {newLength}\n";
            }
        }

        public override string ToString()
        {
            return $"{Headers}\n{Payload}";
        }
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value as bool? ?? false)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return null;
        }
    }

    public class StringNullOrEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return string.IsNullOrWhiteSpace(value as string) == true
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return null;
        }
    }
}
