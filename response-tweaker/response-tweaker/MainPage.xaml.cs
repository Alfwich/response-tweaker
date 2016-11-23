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
            ViewModel.SaveEnabled = true;
            ViewModel.ParseEnabled = true;
            ViewModel.FileNamePath = file.Path;
            ViewModel.SavedFileNamePath = string.Empty;
            _requestFileContents = await RequestFileContents.ReadFileContents(file);
            ViewModel.OriginalFileContent = _requestFileContents.Payload;
            ViewModel.SourceObject = _requestFileContents.GetPayloadObject();
            ViewModel.ParseEnabled = false;
        }

        private void SetClosedOrFailedFileState()
        {
            ViewModel.SaveEnabled = false;
            ViewModel.ParseEnabled = false;
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

        private void LoadUrlButton_OnClick(object sender, RoutedEventArgs e)
        {
        }

        private async Task SaveDataToFile(string fileName, RequestFileContents requestData)
        {
            var folder = ApplicationData.Current.LocalFolder;
            var file = await folder.CreateFileAsync($"modified.{fileName}", CreationCollisionOption.ReplaceExisting);
            if (file != null)
            {
                await FileIO.WriteTextAsync(file, requestData.ToString());
                ViewModel.SavedFileNamePath = file.Path;
            }
        }

        private async void JObjectViewer_OnObjectUpdated(object sender, ObjectUpdatedEventArgs e)
        {
            await SaveDataToFile(ViewModel.FileNamePath.Split('\\').Last(), _requestFileContents);
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

        private bool _saveEnabled;
        public bool SaveEnabled
        {
            get
            {
                return _saveEnabled;
            }
            set
            {
                _saveEnabled = value;
                OnPropertyChanged();
            }
        }

        private bool _parseEnabled;
        public bool ParseEnabled
        {
            get
            {
                return _parseEnabled;
            }
            set
            {
                _parseEnabled = value;
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

                Debug.WriteLine("**************** Old Headers:" + Headers);
                Headers = Headers.Substring(0, startPos) + newLength + Headers.Substring(endPos);
                Debug.WriteLine("**************** New Headers:" + Headers);
            }
        }

        public override string ToString()
        {
            return $"{Headers}\n{Payload}";
        }
    }
}
