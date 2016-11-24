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

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace response_tweaker
{
    public sealed partial class MainPage
    {
        public MainPageViewModel ViewModel { get; set; }
        private RequestFileContents _requestFileContents;
        public MainPage()
        {
            ViewModel = new MainPageViewModel();
            InitializeComponent();
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
            ViewModel.SavedFileNamePath = string.Empty;
            _requestFileContents = await RequestFileContents.ReadFileContents(file);
            ViewModel.OriginalFileContent = _requestFileContents.Payload;
            ViewModel.SourceObject = _requestFileContents.GetPayloadObject();
        }

        private async Task SetOpenedWebResourceState(WebResponse response)
        {
            ViewModel.FileNamePath = string.Empty;
            ViewModel.SavedFileNamePath = string.Empty;
            _requestFileContents = await RequestFileContents.ReadResponseContents(response);
            ViewModel.OriginalFileContent = _requestFileContents.Payload;
            ViewModel.SourceObject = _requestFileContents.GetPayloadObject();
        }
        private void SetClosedOrFailedFileState()
        {
            ViewModel.FileNamePath = string.Empty;
            ViewModel.OriginalFileContent = string.Empty;
            ViewModel.SavedFileNamePath = string.Empty;
        }

        private async void LoadFileButton_OnClick(object sender, RoutedEventArgs e)
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

        private async void LoadUrlButton_OnClick(object sender, RoutedEventArgs e)
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
            }
        }

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
                    ViewModel.SavedFileNamePath = file.Path;
                }
            }
            catch (Exception) when (retryOnFailure)
            {
                // Attempt to retry writing the file if it fails using a plain name
                ViewModel.SavedFilePrefix = string.Empty;
                await SaveDataToFile("request.txt", requestData, false);
            }
        }

        private string GetWebFileName()
        {
            var fileName = ViewModel.WebFileNamePath;
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
            var fileName = ViewModel.FileNamePath == string.Empty
                ? GetWebFileName()
                : GetLocalFileName();

            _requestFileContents.UpdatePayload(ViewModel.GetSourceObjectAsJson());
            await SaveDataToFile(fileName, _requestFileContents);
        }

        private async void Prefix_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_requestFileContents != null)
            {
                await Task.Delay(200);
                JObjectViewer_OnObjectUpdated(null, null);
            }
        }
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


        private string _webFileNamePath = "http://content.hbonow.com/content/tag/v1/series/westworld/xbox-v1.json";
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

        private string _savedFileNamePath;
        public string SavedFileNamePath
        {
            get
            {
                return _savedFileNamePath;
            }
            set
            {
                _savedFileNamePath = value;
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

        private string _savedFilePrefix;
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

        private JObject _deserializedObject;

        public object GetPayloadObject()
        {
            if (_deserializedObject != null)
            {
                return _deserializedObject;
            }

            try
            {
                _deserializedObject = JObject.Parse(Payload);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to deserialize object: {Payload}\nmessage: {ex.Message}");
                _deserializedObject = null;
            }

            return _deserializedObject;
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
}
