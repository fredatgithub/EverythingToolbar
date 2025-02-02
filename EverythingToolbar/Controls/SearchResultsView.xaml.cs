using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using EverythingToolbar.Behaviors;
using EverythingToolbar.Data;
using EverythingToolbar.Helpers;
using EverythingToolbar.Properties;

namespace EverythingToolbar.Controls
{
    public partial class SearchResultsView
    {
        private SearchResult SelectedItem => SearchResultsListView.SelectedItem as SearchResult;
        private Point dragStart;

        public SearchResultsView()
        {
            InitializeComponent();

            SearchResultsListView.ItemsSource = EverythingSearch.Instance.SearchResults;
            ((INotifyCollectionChanged)SearchResultsListView.Items).CollectionChanged += OnCollectionChanged;
            EventDispatcher.Instance.KeyPressed += OnKeyPressed;

            // Mouse events and context menu must be added to the ItemContainerStyle each time it gets updated
            Loaded += (s, e) =>
            {
                RegisterItemContainerStyleProperties(null, null);
                ThemeAwareness.ResourceChanged += RegisterItemContainerStyleProperties;
            };
        }

        private void RegisterItemContainerStyleProperties(object sender, ResourcesChangedEventArgs e)
        {
            SearchResultsListView.ItemContainerStyle.Setters.Add(new EventSetter
            {
                Event = PreviewMouseLeftButtonUpEvent,
                Handler = new MouseButtonEventHandler(SingleClickSearchResult)
            });
            SearchResultsListView.ItemContainerStyle.Setters.Add(new EventSetter
            {
                Event = PreviewMouseDoubleClickEvent,
                Handler = new MouseButtonEventHandler(DoubleClickSearchResult)
            });
            SearchResultsListView.ItemContainerStyle.Setters.Add(new EventSetter
            {
                Event = PreviewMouseDownEvent,
                Handler = new MouseButtonEventHandler(OnListViewItemMouseDown)
            });
            SearchResultsListView.ItemContainerStyle.Setters.Add(new EventSetter
            {
                Event = MouseMoveEvent,
                Handler = new MouseEventHandler(OnListViewItemMouseMove)
            });
            SearchResultsListView.ItemContainerStyle.Setters.Add(new Setter()
            {
                Property = ContextMenuProperty,
                Value = new Binding() { Source = Resources["ListViewItemContextMenu"] }
            });
        }

        private void OnKeyPressed(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Up)
            {
                EventDispatcher.Instance.InvokeSearchTermReplaced(this, HistoryManager.Instance.GetPreviousItem());
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Down)
            {
                EventDispatcher.Instance.InvokeSearchTermReplaced(this, HistoryManager.Instance.GetNextItem());
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                SelectPreviousSearchResult();
            }
            else if (e.Key == Key.Down)
            {
                SelectNextSearchResult();
            }
            else if (Keyboard.Modifiers == ModifierKeys.Shift && e.Key == Key.Enter)
            {
                if (SearchResultsListView.SelectedItem == null)
                    return;

                var path = ((SearchResult)SearchResultsListView.SelectedItem).FullPathAndFileName;
                EverythingSearch.Instance.OpenLastSearchInEverything(path);
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Enter)
            {
                OpenFilePath(null, null);
            }
            else if (e.Key == Key.C && Keyboard.Modifiers == (ModifierKeys.Shift | ModifierKeys.Control))
            {
                SelectedItem?.CopyPathToClipboard();
            }
            else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.Enter)
            {
                RunAsAdmin(null, null);
            }
            else if (e.Key == Key.Enter)
            {
                if (SearchResultsListView.SelectedIndex >= 0)
                    OpenSelectedSearchResult();
                else
                    SelectNextSearchResult();
            }
            else if (e.Key == Key.Space)
            {
                if (SearchWindow.Instance.SearchBox.IsKeyboardFocusWithin)
                    return;

                e.Handled = true;
                PreviewSelectedFile();
            }
            else if (e.Key >= Key.D0 && e.Key <= Key.D9 && Keyboard.Modifiers == ModifierKeys.Control)
            {
                int index = e.Key == Key.D0 ? 9 : e.Key - Key.D1;
                EverythingSearch.Instance.SelectFilterFromIndex(index);
            }
            else if (e.Key == Key.Escape)
            {
                SearchWindow.Instance.Hide();
                Keyboard.ClearFocus();
            }
            else if (e.Key == Key.PageUp)
            {
                PageUp();
            }
            else if (e.Key == Key.PageDown)
            {
                PageDown();
            }
            else if (e.Key == Key.Home)
            {
                if (!SearchWindow.Instance.SearchBox.IsKeyboardFocusWithin)
                    SelectFirstSearchResult();
            }
            else if (e.Key == Key.End)
            {
                if (!SearchWindow.Instance.SearchBox.IsKeyboardFocusWithin)
                    SelectLastSearchResult();
            }
            else if (e.Key == Key.Tab)
            {
                var offset = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1;
                EverythingSearch.Instance.CycleFilters(offset);
                e.Handled = true;
            }
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (!Settings.Default.isAutoSelectFirstResult)
                return;

            if (SearchResultsListView.SelectedIndex == -1 && SearchResultsListView.Items.Count > 0)
                SearchResultsListView.SelectedIndex = 0;
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange > 0)
            {
                if (e.VerticalOffset > e.ExtentHeight - 2 * e.ViewportHeight)
                {
                    EverythingSearch.Instance.QueryBatch(append: true);
                    ScrollToVerticalOffset(e.VerticalOffset);
                }
            }
        }

        private void ScrollToVerticalOffset(double verticalOffset)
        {
            Dispatcher.Invoke(() =>
            {
                GetScrollViewer().ScrollToVerticalOffset(verticalOffset);
            }, DispatcherPriority.ContextIdle);
        }

        private void SelectNextSearchResult()
        {
            if (SearchResultsListView.SelectedIndex + 1 < SearchResultsListView.Items.Count)
            {
                SearchResultsListView.SelectedIndex++;
                SearchResultsListView.ScrollIntoView(SearchResultsListView.SelectedItem);
                FocusSearchWindow();  // Remove keyboard focus from any controls (in particular the search box)
            }
        }

        private void SelectPreviousSearchResult()
        {
            if (SearchResultsListView.SelectedIndex > 0)
            {
                SearchResultsListView.SelectedIndex--;
                SearchResultsListView.ScrollIntoView(SelectedItem);
                FocusSearchWindow();  // Remove keyboard focus from any controls (in particular the search box)
                return;
            }

            SearchResultsListView.SelectedIndex = -1;
            SearchWindow.Instance.SearchBox.Focus();
            SearchWindow.Instance.SearchBox.RestoreCaretIndex();
        }

        private void SelectFirstSearchResult()
        {
            SearchResultsListView.SelectedIndex = 0;
            SearchResultsListView.ScrollIntoView(SelectedItem);
        }

        private void SelectLastSearchResult()
        {
            SearchResultsListView.SelectedIndex = SearchResultsListView.Items.Count - 1;
            SearchResultsListView.ScrollIntoView(SelectedItem);
        }

        private int GetPageSize()
        {
            var itemIndex = Math.Max(SearchResultsListView.SelectedIndex, 0);
            var item = SearchResultsListView.ItemContainerGenerator.ContainerFromIndex(itemIndex) as ListViewItem;
            return (int)(SearchResultsListView.ActualHeight / item.ActualHeight);
        }

        private void PageUp()
        {
            if (SearchResultsListView.SelectedIndex < 0)
                return;

            var stepSize = Math.Max(0, GetPageSize() - 1);
            var newIndex = Math.Max(SearchResultsListView.SelectedIndex - stepSize, 0);
            SearchResultsListView.SelectedIndex = newIndex;
            SearchResultsListView.ScrollIntoView(SelectedItem);
        }

        private void PageDown()
        {
            if (SearchResultsListView.SelectedIndex < 0)
                return;

            var stepSize = Math.Max(0, GetPageSize() - 1);
            var newIndex = Math.Min(SearchResultsListView.SelectedIndex + stepSize, SearchResultsListView.Items.Count - 1);
            SearchResultsListView.SelectedIndex = newIndex;
            SearchResultsListView.ScrollIntoView(SelectedItem);
        }

        private void OpenSelectedSearchResult()
        {
            if (SearchResultsListView.SelectedIndex == -1)
                return;

            if (Rules.HandleRule(SelectedItem))
                return;

            SelectedItem?.Open();
        }

        private void OpenFilePath(object sender, RoutedEventArgs e)
        {
            SelectedItem?.OpenPath();
        }

        private void PreviewSelectedFile()
        {
            SelectedItem?.PreviewInQuickLook();
        }

        private ScrollViewer GetScrollViewer()
        {
            var listViewBorder = VisualTreeHelper.GetChild(SearchResultsListView, 0) as Decorator;
            return listViewBorder?.Child as ScrollViewer;
        }

        private void CopyPathToClipBoard(object sender, RoutedEventArgs e)
        {
            SelectedItem?.CopyPathToClipboard();
            EverythingSearch.Instance.Reset();
        }

        private void OpenWith(object sender, RoutedEventArgs e)
        {
            SelectedItem?.OpenWith();
        }

        private void ShowInEverything(object sender, RoutedEventArgs e)
        {
            SelectedItem?.ShowInEverything();
        }

        private void CopyFile(object sender, RoutedEventArgs e)
        {
            SelectedItem?.CopyToClipboard();
            EverythingSearch.Instance.Reset();
        }

        private void SingleClickSearchResult(object sender, MouseEventArgs e)
        {
            if (!Settings.Default.isDoubleClickToOpen)
                Open(sender, e);
        }

        private void DoubleClickSearchResult(object sender, MouseEventArgs e)
        {
            if (Settings.Default.isDoubleClickToOpen)
                Open(sender, e);
        }

        private void Open(object sender, RoutedEventArgs e)
        {
            OpenSelectedSearchResult();
        }

        private void Open(object sender, MouseEventArgs e)
        {
            switch (Keyboard.Modifiers)
            {
                case ModifierKeys.Alt:
                    SelectedItem?.ShowProperties();
                    break;
                case ModifierKeys.Control:
                    SelectedItem?.OpenPath();
                    break;
                case ModifierKeys.Shift:
                    SelectedItem?.ShowInEverything();
                    break;
                default:
                    OpenSelectedSearchResult();
                    break;
            }
        }

        public void RunAsAdmin(object sender, RoutedEventArgs e)
        {
            SelectedItem?.RunAsAdmin();
        }

        public void ShowFileProperties(object sender, RoutedEventArgs e)
        {
            SelectedItem?.ShowProperties();
        }

        public void ShowFileWindowsContextMenu(object sender, RoutedEventArgs e)
        {
            SelectedItem?.ShowWindowsContextMenu();
        }

        private void OnOpenWithMenuLoaded(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;

            while (mi.Items.Count > 3)
                mi.Items.RemoveAt(0);

            List<Rule> rules = Rules.LoadRules();

            if (rules.Count > 0)
                (mi.Items[0] as MenuItem).Visibility = Visibility.Collapsed;
            else
                (mi.Items[0] as MenuItem).Visibility = Visibility.Visible;

            for (int i = rules.Count - 1; i >= 0; i--)
            {
                Rule rule = rules[i];
                MenuItem ruleMenuItem = new MenuItem() { Header = rule.Name, Tag = rule.Command };
                ruleMenuItem.Click += OpenWithRule;
                mi.Items.Insert(0, ruleMenuItem);
            }
        }

        private void OpenWithRule(object sender, RoutedEventArgs e)
        {
            if (SearchResultsListView.SelectedItem == null)
                return;
            
            var searchResult = SearchResultsListView.SelectedItem as SearchResult;
            var command = (sender as MenuItem).Tag?.ToString() ?? "";
            Rules.HandleRule(searchResult, command);
        }

        private void OnListViewItemMouseDown(object sender, MouseButtonEventArgs e)
        {
            dragStart = PointToScreen(Mouse.GetPosition(this));
        }

        private void OnListViewItemMouseMove(object sender, MouseEventArgs e)
        {
            var diff = dragStart - PointToScreen(Mouse.GetPosition(this));

            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                if (SearchResultsListView.SelectedItems.Count == 0)
                    return;

                string[] files = { SelectedItem?.FullPathAndFileName };
                var data = new DataObject(DataFormats.FileDrop, files);
                data.SetData(DataFormats.Text, files[0]);
                DragDrop.DoDragDrop(SearchResultsListView, data, DragDropEffects.All);
            }
        }

        private void OnContextMenuOpened(object sender, RoutedEventArgs e)
        {
            var cm = sender as ContextMenu;
            var mi = cm.Items[2] as MenuItem;

            string[] extensions = { ".exe", ".bat", ".cmd" };
            var isExecutable = SelectedItem.IsFile && extensions.Any(ext => SelectedItem.FullPathAndFileName.EndsWith(ext));

            mi.Visibility = isExecutable ? Visibility.Visible : Visibility.Collapsed;
        }

        private void FocusSearchWindow()
        {
            var scope = FocusManager.GetFocusScope(this);
            FocusManager.SetFocusedElement(scope, SearchWindow.Instance);
        }
    }
}
