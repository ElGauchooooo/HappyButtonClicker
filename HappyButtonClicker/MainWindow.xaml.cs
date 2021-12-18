using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace HappyButtonClicker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Dispatcher _dispatcher;
        private bool _isClicking;

        public MainWindow()
        {
            InitializeComponent();
            _dispatcher = Application.Current.Dispatcher;
            ResetButton();

            GithubLink.RequestNavigate += HandleRequestNavigate;
        }

        private void HandleRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            var url = e.Uri.ToString();
            Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true
            });
        }

        private async void ClickButtons(object sender, RoutedEventArgs e)
        {
            if (_isClicking)
                return;

            _isClicking = true;

            _dispatcher.Invoke(() => ClickButton.Content = "...clicking...");

            var happyButtonClicker = new ClickerBot();
            var result = await happyButtonClicker.StartClicking(RootUrl.Text);

            _isClicking = false;
            _dispatcher.Invoke(() =>
            {
                ResetButton();
                this.Activate();
            });

            switch (result)
            {
                case ClickResult.NetworkIssue:
                    _dispatcher.Invoke(() => MessageBox.Show("There was a network issue :( couldn't open that page."));
                    break;
                case ClickResult.UserAbort:
                    _dispatcher.Invoke(() => MessageBox.Show("User aborted... :("));
                    break;
                case ClickResult.Success:
                    break;
                case ClickResult.NoButtonToClick:
                    _dispatcher.Invoke(() => MessageBox.Show("I couldn't find a happy button to click after a while..."));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

        }

        private void ResetButton()
        {
            ClickButton.Content = "Let me click them!";
        }
    }
}
