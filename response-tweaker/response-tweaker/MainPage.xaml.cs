using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.Storage.Pickers;
using Newtonsoft.Json.Linq;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace response_tweaker
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage
    {
        public MainPageViewModel ViewModel { get; set; }
        private RequestFileContents _requestFileContents = null;
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

        private void ParseData_OnClick(object sender, RoutedEventArgs e)
        {
            ViewModel.SourceObject = _requestFileContents.GetPayloadObject();
            ViewModel.ParseEnabled = false;
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

    }
}
