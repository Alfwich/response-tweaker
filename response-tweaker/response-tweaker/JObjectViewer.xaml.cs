using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Dataflow;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Newtonsoft.Json.Linq;
using response_tweaker.Annotations;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace response_tweaker
{
    public sealed partial class JObjectViewer : UserControl
    {
        public static readonly DependencyProperty SourceJObjectProperty = DependencyProperty.Register("SourceJObject", typeof(object), typeof(JObjectViewer), null);

        public JObjectViewerViewModel ViewModel { get; set; }
        private object _currentLevel;
        private readonly Stack<object> _levels = new Stack<object>();

        public JObjectViewer()
        {
            ViewModel = new JObjectViewerViewModel();
            InitializeComponent();
        }

        private void PushLevelAndLoadNewCurrent(object level)
        {
            if (_currentLevel == level)
            {
                return;
            }

            _levels.Push(_currentLevel);
            UpdateCurrentLevelView(level);
            PostLevelStackMutate();
        }

        private void PopLevelAndLoadNewCurrent()
        {
            UpdateCurrentLevelView(_levels.Peek());
            _levels.Pop();
            PostLevelStackMutate();
        }

        private void PostLevelStackMutate()
        {
            ViewModel.BackVisibility = _levels.Count > 0;
            if (_currentLevel != null)
            {
                var jObject = _currentLevel as JObject;
                if (jObject != null)
                {
                    ViewModel.CurrentPath = jObject.Path;
                    return;
                }

                var jArray = _currentLevel as JArray;
                if (jArray != null)
                {
                    ViewModel.CurrentPath = jArray.Path;
                }

            }
        }

        private bool UpdateCurrentLevelView(object level)
        {
            if (_currentLevel == level)
            {
                return false;
            }

            var jObject = level as JObject;
            if (jObject != null)
            {
                _currentLevel = level;
                ViewModel.JObjectCurrentListing.Clear();
                UpdateCurrentLevelJObject(jObject);
                return true;
            }

            var jArray = level as JArray;
            if (jArray != null)
            {
                _currentLevel = level;
                ViewModel.JObjectCurrentListing.Clear();
                UpdateCurrentLevelJArray(jArray);
                return true;
            }

            return false;
        }

        private void UpdateCurrentLevelJObject(JObject level)
        {
            foreach (var keyValuePair in level)
            {
                ViewModel.JObjectCurrentListing.Add(new JObjectRow
                {
                    Parent = level,
                    Key = keyValuePair.Key,
                    Value = keyValuePair.Value,
                    Label = $"{keyValuePair.Key}: {keyValuePair.Value}",
                    ClickEnabled = keyValuePair.Value is JObject || keyValuePair.Value is JArray
                });
            }
        }

        private void UpdateCurrentLevelJArray(JArray level, int startIndex = 0)
        {
            foreach (var token in level.Children())
            {
                var key = startIndex++.ToString();
                ViewModel.JObjectCurrentListing.Add(new JObjectRow
                {
                    Parent = level,
                    Key = key,
                    Value = token,
                    Label = $"{key}: {token}",
                    ClickEnabled = token is JObject || token is JArray
                });
            }
        }

        public object SourceJObject
        {
            get
            {
                return GetValue(SourceJObjectProperty) as JObject;
            }
            set
            {
                SetValue(SourceJObjectProperty, value);
                UpdateCurrentLevelView(value);
            }
        }

        private void ObjectLevelViewer_OnItemClick(object sender, ItemClickEventArgs e)
        {
            var objectRow = e.ClickedItem as JObjectRow;
            if (objectRow != null && objectRow.ClickEnabled)
            {
                PushLevelAndLoadNewCurrent(objectRow.Value);
            }
        }

        private void BackButton_OnClick(object sender, RoutedEventArgs e)
        {
            PopLevelAndLoadNewCurrent();
        }
    }

    public class JObjectRow
    {
        public object Parent { get; set; }
        public string Label { get; set; }
        public string Key { get; set; }
        public object Value { get; set; }
        public bool ClickEnabled { get; set; }
    }

    public class JObjectViewerViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<JObjectRow> JObjectCurrentListing { get; set; } = new ObservableCollection<JObjectRow>();

        private bool _backVisibility;
        public bool BackVisibility
        {
            get
            {
                return _backVisibility;
            }
            set
            {
                _backVisibility = value;
                OnPropertyChanged();
            }
        }

        private string _currentPath;

        public string CurrentPath
        {
            get
            {
                return _currentPath;
            }
            set
            {
                _currentPath = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
