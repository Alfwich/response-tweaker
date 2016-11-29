using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Newtonsoft.Json.Linq;
using response_tweaker.Annotations;
using Windows.UI.ViewManagement;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace response_tweaker
{
    public class ObjectUpdatedEventArgs : EventArgs
    {
    }


    public class PathChangedEventArgs : EventArgs
    {
        public string NewPath { get; set; }
    }

    public sealed partial class JObjectViewer : UserControl
    {
        public static readonly DependencyProperty SourceJObjectProperty = DependencyProperty.Register("SourceJObject", typeof(object), typeof(JObjectViewer), null);

        public event EventHandler<ObjectUpdatedEventArgs> ObjectUpdated;
        public event EventHandler<PathChangedEventArgs> PathChanged;

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
            ViewModel.BackEnabled = _levels.Count > 0;
            if (_currentLevel != null)
            {
                var jObject = _currentLevel as JObject;
                if (jObject != null)
                {
                    ViewModel.CurrentPath = jObject.Path;
                    PathChanged?.Invoke(this, new PathChangedEventArgs { NewPath = jObject.Path });
                    return;
                }

                var jArray = _currentLevel as JArray;
                if (jArray != null)
                {
                    ViewModel.CurrentPath = jArray.Path;
                    PathChanged?.Invoke(this, new PathChangedEventArgs { NewPath = jArray.Path });
                    return;
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
                var row = new JObjectRow
                {
                    Parent = level,
                    Key = keyValuePair.Key,
                    KeyLabel = keyValuePair.Key,
                    Value = keyValuePair.Value,
                    ValueLabel = keyValuePair.Value.ToString(),
                    ClickEnabled = keyValuePair.Value is JObject || keyValuePair.Value is JArray,
                    EditAllowed = !(keyValuePair.Value is JObject || keyValuePair.Value is JArray)
                };
                row.ObjectUpdated += RowOnObjectUpdated;
                ViewModel.JObjectCurrentListing.Add(row);
            }
        }

        private void UpdateCurrentLevelJArray(JArray level, int startIndex = 0)
        {
            foreach (var token in level.Children())
            {
                var key = startIndex++.ToString();
                var row = new JObjectRow
                {
                    Parent = level,
                    Key = key,
                    KeyLabel = key,
                    Value = token,
                    ValueLabel = token.ToString(),
                    ClickEnabled = token is JObject || token is JArray,
                    EditAllowed = !(token is JObject || token is JArray)
                };

                row.ObjectUpdated += RowOnObjectUpdated;
                ViewModel.JObjectCurrentListing.Add(row);
            }
        }

        private void RowOnObjectUpdated(object sender, ObjectUpdatedEventArgs objectUpdatedEventArgs)
        {
            ObjectUpdated?.Invoke(sender, objectUpdatedEventArgs);
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

        private void JObjectRowClicked(JObjectRow row)
        {
            if (row != null && row.ClickEnabled)
            {
                PushLevelAndLoadNewCurrent(row.Value);
            }
        }

        private void BackButton_OnClick(object sender, RoutedEventArgs e)
        {
            PopLevelAndLoadNewCurrent();
        }

        private void ObjectLevelViewer_OnItemClick(object sender, ItemClickEventArgs e)
        {
            JObjectRowClicked(e.ClickedItem as JObjectRow);
        }

        private void OnObjectUpdated(ObjectUpdatedEventArgs e)
        {
            ObjectUpdated?.Invoke(this, e);
        }

    }

    public class JObjectRow : INotifyPropertyChanged
    {
        public event EventHandler<ObjectUpdatedEventArgs> ObjectUpdated;

        private const string EditButtonLabelValue = "Edit";
        private const string SaveButtonLabelValue = "Save";
        private const string ExpandButtonLabelValue = "+";
        private const string ContractButtonLabelValue = "-";
        private const int ContractedMaxLines = 5;
        private const int ExpandedMaxLines = int.MaxValue;

        public object Parent { get; set; }

        public string KeyLabel { get; set; }

        public string Key { get; set; }

        public JToken Value { get; set; }

        private string _valueLabel;
        public string ValueLabel
        {
            get
            {
                return _valueLabel;
            }
            set
            {
                _valueLabel = value;
                ExpandAllowed = EstimateNumberOfLines(value) > ContractedMaxLines;
                OnPropertyChanged();
            }
        }

        public bool ClickEnabled { get; set; }

        private bool _editEnabled;
        public bool EditEnabled
        {
            get { return _editEnabled; }
            set
            {
                _editEnabled = value;
                OnPropertyChanged();
            }
        }

        private string _editButtonLabel = EditButtonLabelValue;
        public string EditButtonLabel
        {
            get
            {
                return _editButtonLabel;
            }
            set
            {
                _editButtonLabel = value;
                OnPropertyChanged();
            }
        }

        private string _expandButtonLabel = ExpandButtonLabelValue;
        public string ExpandButtonLabel
        {
            get
            {
                return _expandButtonLabel;

            }
            set
            {
                _expandButtonLabel = value;
                OnPropertyChanged();
            }
        }

        private int _maxLinesForValueLabel = ContractedMaxLines;
        public int MaxLinesForValueLabel
        {
            get
            {
                return _maxLinesForValueLabel;
            }
            set
            {
                _maxLinesForValueLabel = value;
                OnPropertyChanged();
            }
        }

        private bool _expandAllowed;
        public bool ExpandAllowed
        {
            get
            {
                return _expandAllowed;
            }
            set
            {
                _expandAllowed = value;
                OnPropertyChanged();
            }
        }

        public bool IsExpanded { get; set; }

        public bool EditAllowed { get; set; }

        public Visibility EditFeaturesVisibility => EditAllowed ? Visibility.Visible : Visibility.Collapsed;

        private Visibility _valueVisible = Visibility.Visible;
        public Visibility ValueVisibility
        {
            get
            {
                return _valueVisible;
            }
            set
            {
                _valueVisible = value;
                OnPropertyChanged();
            }
        }

        private Visibility _editVisible = Visibility.Collapsed;
        public Visibility EditVisibility
        {
            get
            {
                return _editVisible;
            }
            set
            {
                _editVisible = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void EditRequested(object sender, object e)
        {
            if (EditAllowed)
            {
                // Update
                if (EditEnabled)
                {
                    UpdateParent(ValueLabel);
                }

                ToggleEditButton();
            }
        }

        public void InputBoxKeyUp(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                // Update
                case VirtualKey.Enter:
                    UpdateParent((sender as TextBox)?.Text);
                    ToggleEditButton();
                    e.Handled = true;
                    break;
            }
        }

        public void ExpandRequested(object sender, object e)
        {
            ToggleExpand();
        }

        private void ToggleExpand()
        {

            IsExpanded = !IsExpanded;
            MaxLinesForValueLabel = IsExpanded ? ExpandedMaxLines : ContractedMaxLines;
            ExpandButtonLabel = IsExpanded ? ContractButtonLabelValue : ExpandButtonLabelValue;
        }

        private int EstimateNumberOfLines(string content)
        {
            return content.Split('\n').Length - 1;
        }

        private void ToggleEditButton()
        {
            EditEnabled = !EditEnabled;
            EditVisibility = EditEnabled ? Visibility.Visible : Visibility.Collapsed;
            ValueVisibility = EditEnabled ? Visibility.Collapsed : Visibility.Visible;
            EditButtonLabel = EditEnabled ? SaveButtonLabelValue : EditButtonLabelValue;
        }

        private enum JsonTypes
        {
            Null,
            String,
            Boolean,
            Array,
            Object
        }

        private void UpdateParent(object newValue, JsonTypes desiredType = JsonTypes.String)
        {
            if (desiredType != JsonTypes.Null && newValue == null)
            {
                return;
            }

            var parentObject = Parent as JObject;
            if (parentObject != null)
            {
                var prop = Value.Parent as JProperty;
                if (prop != null)
                {
                    switch (desiredType)
                    {
                        case JsonTypes.Null:
                            parentObject[prop.Name] = null;
                            break;
                        case JsonTypes.String:
                            parentObject[prop.Name] = (string)newValue;
                            break;
                        case JsonTypes.Boolean:
                            parentObject[prop.Name] = (bool)newValue;
                            break;
                        case JsonTypes.Array:
                            parentObject[prop.Name] = (JArray)newValue;
                            break;
                        case JsonTypes.Object:
                            parentObject[prop.Name] = (JObject)newValue;
                            break;
                    }
                    ObjectUpdated?.Invoke(this, new ObjectUpdatedEventArgs());
                }

                return;
            }

            var parentArray = Parent as JArray;
            if (parentArray != null)
            {
                var index = parentArray.IndexOf(Value);
                if (index != -1)
                {
                    switch (desiredType)
                    {
                        case JsonTypes.Null:
                            parentArray[index] = null;
                            break;
                        case JsonTypes.String:
                            parentArray[index] = (string)newValue;
                            break;
                        case JsonTypes.Boolean:
                            parentArray[index] = (bool)newValue;
                            break;
                        case JsonTypes.Array:
                            parentArray[index] = (JArray)newValue;
                            break;
                        case JsonTypes.Object:
                            parentArray[index] = (JObject)newValue;
                            break;
                    }
                    ObjectUpdated?.Invoke(this, new ObjectUpdatedEventArgs());
                }
                return;
            }
        }

        public void ContentSetToTrue(object sender, RoutedEventArgs e)
        {
            ValueLabel = "true";
            ClickEnabled = false;
            UpdateParent(true, JsonTypes.Boolean);
            ToggleEditButton();
        }

        public void ContentSetToFalse(object sender, RoutedEventArgs e)
        {
            ValueLabel = "false";
            ClickEnabled = false;
            UpdateParent(false, JsonTypes.Boolean);
            ToggleEditButton();
        }

        public void ContentSetToNull(object sender, RoutedEventArgs e)
        {
            ValueLabel = "null";
            ClickEnabled = false;
            UpdateParent(null, JsonTypes.Null);
            ToggleEditButton();
        }

        public void ContentSetToEmptyArray(object sender, RoutedEventArgs e)
        {
            ValueLabel = "[]";
            ClickEnabled = true;
            UpdateParent(new JArray(), JsonTypes.Array);
            ToggleEditButton();
        }

        public void ContentSetToEmptyObject(object sender, RoutedEventArgs e)
        {
            ValueLabel = "{}";
            ClickEnabled = true;
            UpdateParent(new JObject(), JsonTypes.Object);
            ToggleEditButton();
        }
    }

    public class JObjectViewerViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<JObjectRow> JObjectCurrentListing { get; set; } = new ObservableCollection<JObjectRow>();

        private bool _backEnabled;

        public bool BackEnabled
        {
            get { return _backEnabled; }
            set
            {
                _backEnabled = value;
                OnPropertyChanged();
            }
        }

        private string _currentPath;

        public string CurrentPath
        {
            get { return _currentPath; }
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
