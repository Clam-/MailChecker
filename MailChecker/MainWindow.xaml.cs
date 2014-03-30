using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Google.Apis.Auth.OAuth2;
using System.Reflection;

namespace MailChecker
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		//ghetto flag to see if exiting from menu
		bool closing = false;

		System.ComponentModel.Container components;
		internal System.Windows.Forms.NotifyIcon notifyIcon;
		CancellationTokenSource workerCancel = new CancellationTokenSource();
		Settings settings = new Settings();
		private static TraceSource logging =
			new TraceSource("MailChecker");
		bool isNewMail = false;

		internal System.Drawing.Icon[] icons = new System.Drawing.Icon[2];
		CancellationTokenSource blinkingCTS = null;
		Thread blinkingThread = null;
		
		public MainWindow()
		{
			logging.TraceEvent(TraceEventType.Information, 1, "Starting up...");

			settings.LoadSettings();
			//setup icons
			icons[0] = System.Drawing.Icon.ExtractAssociatedIcon(Application.ResourceAssembly.Location);
			using (System.IO.Stream stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("MailChecker.Resources.blankicon.ico"))
			{
				icons[1] = new System.Drawing.Icon(stream);
			}
			logging.TraceEvent(TraceEventType.Information, 1, "Systray...");
			InitializeSystray();
			Resources["Accounts"] = settings.accountsDict;
			logging.TraceEvent(TraceEventType.Information, 1, "UI...");
			InitializeComponent();

			Worker workerObj = new Worker(settings, workerCancel.Token, this);
			Thread workerThread =
				new Thread(new ThreadStart(workerObj.processWork));
			logging.TraceEvent(TraceEventType.Information, 1, "Worker...");
			workerThread.Start();
			settingsWindow.Icon = null;
			
		}

		private void InitializeSystray()
		{
			// http://msdn.microsoft.com/en-us/library/1by05f8d(v=vs.110).aspx

			// create systray stuff
			components = new System.ComponentModel.Container();
			System.Windows.Forms.ContextMenu contextMenu1 = new System.Windows.Forms.ContextMenu();
			System.Windows.Forms.MenuItem exitItem = new System.Windows.Forms.MenuItem();
			System.Windows.Forms.MenuItem settingsItem = new System.Windows.Forms.MenuItem();

			// Initialize contextMenu1 
			contextMenu1.MenuItems.AddRange(
						new System.Windows.Forms.MenuItem[] { exitItem, settingsItem });

			// Initialize Items
			exitItem.Index = 1;
			exitItem.Text = "E&xit";
			exitItem.Click += new System.EventHandler(exitCick);
			settingsItem.Index = 0;
			settingsItem.Text = "&Settings...";
			settingsItem.Click += new System.EventHandler(settingsCick);

			// Create the NotifyIcon. 
			notifyIcon = new System.Windows.Forms.NotifyIcon(components);
			notifyIcon.Icon = icons[0];
			notifyIcon.ContextMenu = contextMenu1;
			notifyIcon.Text = "Loading...";
			notifyIcon.Visible = true;

			notifyIcon.DoubleClick += new System.EventHandler(settingsCick);
		}

		internal void doneLoading()
		{
			Dispatcher.Invoke(new Action(() => 
			{
				notifyIcon.Text = "No mail.";
			}));
		}
		
		
		public void newMail(string s)
		{
			Dispatcher.BeginInvoke(new Action(() => 
			{
				notifyIcon.BalloonTipTitle = "New Mail";
				notifyIcon.BalloonTipText = s;
				notifyIcon.ShowBalloonTip(30);
				notifyIcon.Text = "New mail.";
				isNewMail = true;
				// start flashing
				blinkingCTS = new CancellationTokenSource();
				AnimateWorker aw = new AnimateWorker(blinkingCTS.Token, this);
				blinkingThread = new Thread(new ThreadStart(aw.animateIcon));
				blinkingThread.Start();
			}));
		}

		internal void remind(string s)
		{
			Dispatcher.BeginInvoke(new Action(() => 
			{
				notifyIcon.BalloonTipText = s;
				notifyIcon.ShowBalloonTip(30);
			}));
		}

		internal void noMail()
		{
			notifyIcon.Text = "No mail.";
			if (isNewMail)
			{
				// revert Icon
				if (blinkingCTS != null)
				{
					blinkingCTS.Cancel();
					blinkingCTS = null;
				}
				isNewMail = false;
			}
		}

		private void exitCick(object sender, EventArgs e)
		{
			//exit
			if (blinkingCTS != null)
			{
				blinkingCTS.Cancel();
			}
			if ((blinkingThread != null) && blinkingThread.IsAlive)
			{
				blinkingThread.Join();
			}
			closing = true;
			// stop mail checking routines
			workerCancel.Cancel();

			//get rid of Systray icon
			if (components != null)
			{
				components.Dispose();
			}
			Close();
		}

		private void settingsCick(object sender, EventArgs e)
		{
			// populate polling period
			pollingPeriod.Text = settings.pollperiod.ToString();
			reminderPeriod.Text = settings.reminderperiod.ToString();
			Visibility = System.Windows.Visibility.Visible;
			ShowInTaskbar = true;
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (!closing)
			{
				e.Cancel = true;
				
				ShowInTaskbar = false;
				Visibility = System.Windows.Visibility.Hidden;
			}
		}

		private void addaccountButton_Click(object sender, RoutedEventArgs e)
		{
			Console.WriteLine("Adding (" + emailaddr.Text + "):" +  settings.addAccount(emailaddr.Text));
		}

		private void cancelButton_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void savecloseButton_Click(object sender, RoutedEventArgs e)
		{
			// check value(s?)
			int tempi;
			if (int.TryParse(pollingPeriod.Text, out tempi))
			{
				if (tempi < 30)
				{
					statusLabel.Content = "Invalid period. (must be <30.)";
					return;
				}
				else
				{
					settings.pollperiod = tempi;
				}
			}
			else
			{
				statusLabel.Content = "Invalid period. (must be <30.)";
				return;
			}
			if (int.TryParse(reminderPeriod.Text, out tempi))
			{
				if (tempi < 0 )
				{
					statusLabel.Content = "Invalid period (must be 0 or higher.)";
					return;
				}
				else
				{
					settings.reminderperiod = tempi;
				}
			}
			else
			{
				statusLabel.Content = "Invalid period. (must be > 0 and > poll period.)";
				return;
			}
			// save settings
			settings.SaveSettings();
			Close();
		}

		private void accountList_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			// http://stackoverflow.com/a/14674830
			ListView _ListView = sender as ListView;
			GridView _GridView = _ListView.View as GridView;
			var _ActualWidth = _ListView.ActualWidth - SystemParameters.VerticalScrollBarWidth;
			_GridView.Columns[0].Width = _ActualWidth;
		}

		private void removeAccountButton_Click(object sender, RoutedEventArgs e)
		{
			List<KeyValuePair<String, UserCredential>> dr = new List<KeyValuePair<String, UserCredential>>();
			foreach (KeyValuePair<String, UserCredential> d in accountList.SelectedItems)
			{
				dr.Add(d);
			}
			foreach (var d in dr)
			{
				((IDictionary<String, UserCredential>)accountList.ItemsSource).Remove(d.Key);
			}
		}

	}

	class AnimateWorker
	{
		MainWindow mainWindow;
		CancellationToken cancelToken;

		internal AnimateWorker(CancellationToken ct, MainWindow mw)
		{
			cancelToken = ct;
			mainWindow = mw;
		}

		internal void animateIcon()
		{
			bool toggle = true;
			while (true)
			{
				cancelToken.WaitHandle.WaitOne(1000 * 2);
				if (cancelToken.IsCancellationRequested) { break; }
				if (toggle)
				{
					mainWindow.Dispatcher.BeginInvoke(new Action(() =>
					{
						mainWindow.notifyIcon.Icon = mainWindow.icons[1];
					}));
					toggle = false;
				}
				else
				{
					mainWindow.Dispatcher.BeginInvoke(new Action(() =>
					{
						mainWindow.notifyIcon.Icon = mainWindow.icons[0];
					}));
					toggle = true;
				}
			}
			if (cancelToken.IsCancellationRequested) { return; }
			//reset icon
			mainWindow.Dispatcher.BeginInvoke(new Action(() =>
			{
				mainWindow.notifyIcon.Icon = mainWindow.icons[0];
			}));
		}
	}
}

