using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MailChecker
{
	class Worker
	{
		CancellationToken token;
		//items
		Settings settings;

		MainWindow mainWindow;

		Dictionary<string, ImapClient> clients = new Dictionary<string, ImapClient>();

		private static TraceSource logging =
			new TraceSource("MailChecker.Worker");

		public Worker(Settings s, CancellationToken ct, MainWindow mw)
		{
			//setup items
			token = ct;
			settings = s;
			mainWindow = mw;
		}

		public void processWork()
		{
			// put credentials into dict
			logging.TraceEvent(TraceEventType.Information, 1, "Adding users");
			foreach (string user in settings.accountsDict.Keys) {
				if (settings.accountsDict[user] == null) {
					mainWindow.Dispatcher.Invoke(new Action(() =>
					{
						settings.addAccount(user);
					}));
				} 
			}
			mainWindow.doneLoading();

			logging.TraceEvent(TraceEventType.Information, 1, "Done.");

			List<string> newMailAccts = null;

			bool notified = false;
			System.Diagnostics.Stopwatch pollsw = new System.Diagnostics.Stopwatch();
			System.Diagnostics.Stopwatch remindsw = null;
			pollsw.Start();
			if (settings.reminderperiod != 0) {
				remindsw = new System.Diagnostics.Stopwatch();
			}
			while (true)
			{
				token.WaitHandle.WaitOne(1000 * 5);
				if (token.IsCancellationRequested) { break; }
				// check if reminder needs to be turned off or enabled
				if (remindsw == null && settings.reminderperiod != 0)
				{
					remindsw = new System.Diagnostics.Stopwatch();
					remindsw.Start();
				}
				if (remindsw != null && settings.reminderperiod == 0)
				{
					//disable remind
					remindsw.Stop();
					remindsw = null;
				}

				if (pollsw.Elapsed.Seconds+1 > settings.pollperiod)
				{
					newMailAccts = checkMail();
					pollsw.Restart();
				}
				
				// do notification stuff after checking for cancel.
				if (token.IsCancellationRequested) { break; }
				if (newMailAccts != null)
				{
					if (!notified)
					{
						logging.TraceEvent(TraceEventType.Information, 1, "New mail.");
						mainWindow.newMail(String.Join("\n", newMailAccts));
						notified = true;
						remindsw.Restart();
					}
					else
					{
						if (remindsw != null && (remindsw.Elapsed.Seconds+1 > settings.reminderperiod))
						{
							mainWindow.remind(String.Join("\n", newMailAccts));
							remindsw.Restart();
						}
					}
				} else {
					mainWindow.noMail();
					notified = false;
					if (remindsw != null)
					{
						remindsw.Stop();
					}
				}
				throw new Exception();
			}
			logging.TraceEvent(TraceEventType.Information, 1, "Finishing worker thread.");
		}

		private List<string> checkMail()
		{
			var users = settings.accountsDict.Keys.ToArray();
			var clientkeys = clients.Keys;
			var intersect = users.Intersect(clientkeys);

			List<string> mails = new List<string>(users.Count());

			foreach (var item in clientkeys)
			{
				if (!intersect.Contains(item))
				{
					ImapClient c = clients[item];
					c.Disconnect(true, token);
					c.Dispose();
					clients.Remove(item);
				}
				if (token.IsCancellationRequested) { break; }
			}
			if (token.IsCancellationRequested) { return null; }

			foreach (var user in users)
			{
				if (!clients.ContainsKey(user))
				{
					//create client
					ImapClient c = new ImapClient();
					if (settings.accountsDict[user] != null)
					{
						var credentials = new NetworkCredential(user, settings.accountsDict[user].Token.AccessToken);
						var uri = new Uri("imaps://imap.gmail.com");

						c.Connect(uri, token);
						c.Authenticate(credentials, token);
						c.Inbox.Open(FolderAccess.ReadOnly, token);
						clients[user] = c;
					}
					else
					{
						logging.TraceEvent(TraceEventType.Error, 1, "User credentials is null for: " + user);
					}
				}

				if (token.IsCancellationRequested) { break; }
				if (clients.ContainsKey(user))
				{
					ImapClient client = clients[user];
					int unread;
					try
					{
						unread = client.Inbox.Search(SearchQuery.NotSeen, token).Length;
					}
					catch (ImapProtocolException)
					{
						logging.TraceEvent(TraceEventType.Error, 1, "IMAP not enabled for: " + user);
						continue;
					}
					//inbox.Open(FolderAccess.ReadOnly, token);
					if (unread != 0)
					{
						mails.Add(user + "(" + unread + ")");
					}
				}
				else
				{
					logging.TraceEvent(TraceEventType.Error, 1, "No client for user: " + user);
				}
				if (token.IsCancellationRequested) { break; }
			}
			
			if (mails.Count == 0) return null;
			else return mails;
		}
	}
}
