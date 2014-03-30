using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MailChecker
{
	/// <summary>
	/// Interaction logic for Window1.xaml
	/// </summary>
	public partial class AuthWaitDialog : Window
	{

		public AuthWaitDialog(string s)
		{
			InitializeComponent();
			msgLabel.Content = String.Format((string)msgLabel.Content, s);
		}

		public void completedClose()
		{
			DialogResult = true;
			// aaaaaaaaaaaaaa
			Close();
		}
	}
}
