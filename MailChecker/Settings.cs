using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.IO;
using Google.Apis.Auth.OAuth2;
using System.Threading;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Util.Store;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using System.Diagnostics;

namespace MailChecker
{
	class StoredSettings
	{
		public int pollperiod = -1;
		public int reminderperiod = -1;

		public string[] accounts = null;
	}
	
	class Settings
	{

		public ObservableConcurrentDictionary<string, UserCredential> accountsDict;

		public int pollperiod = 60;
		public int reminderperiod = 0;

		private static TraceSource logging =
			new TraceSource("MailChecker.Settings");

		public Settings()
		{
			accountsDict = new ObservableConcurrentDictionary<string, UserCredential>();
		}

		

		public bool addAccount(string user)
		{
			CancellationTokenSource cts = new CancellationTokenSource();

			Task<UserCredential> ucTask = SAuthorizeAsync(user, cts.Token);

			AuthWaitDialog authdiag = new AuthWaitDialog(user);
			ucTask.ContinueWith(t => PostKeyRetrieve(t, authdiag, user));

			bool? result = authdiag.ShowDialog();
			if (!result.GetValueOrDefault(false))
			{
				cts.Cancel();
			}

			return true;
		}

		private static async Task<UserCredential> SAuthorizeAsync(string user, CancellationToken token)
		{
			Stream stream;
			try
			{
				using (stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("MailChecker.Resources.clientsecrets.json"))
				{
					var initializer = new GoogleAuthorizationCodeFlow.Initializer
					{
						ClientSecretsStream = stream,
					};

					initializer.Scopes = new[] { "https://mail.google.com" };

					var flow = new GoogleAuthorizationCodeFlow(initializer);

					return await new AuthorizationCodeInstalledApp(flow, new LocalServerCodeReceiver()).AuthorizeAsync
						(user, token).ConfigureAwait(false);
				}
			}
			catch (Exception e)
			{
				logging.TraceEvent(TraceEventType.Error, 1, "Error in SAuthorizeAsync: " + e);
				return null;
			}
		}

		public void PostKeyRetrieve(Task<UserCredential> doneTask, AuthWaitDialog authwin, string id)
		{
			try
			{
				UserCredential credential = doneTask.Result;
				accountsDict[id] = credential;
			}
			catch (Exception e)
			{
				logging.TraceEvent(TraceEventType.Error, 1, "Error in PostKeyRetrieve: " + e);
			}
			authwin.authWindow.Dispatcher.BeginInvoke(new Action(() => authwin.completedClose() ) );
		}

		internal void LoadSettings()
		{
			if (File.Exists("settings.json"))
			{
				using (StreamReader sr = new StreamReader("settings.json"))
				using (JsonReader reader = new JsonTextReader(sr))
				{
					JsonSerializer serializer = new JsonSerializer();
					// read the json from a stream

					// json size doesn't matter because only a small piece is read at a time from the HTTP request

					StoredSettings s = serializer.Deserialize<StoredSettings>(reader);
					if (s.pollperiod > 0) {
						pollperiod = s.pollperiod;
					}
					if (s.reminderperiod > 0) {
						reminderperiod = s.reminderperiod;
					}
					if (s.accounts != null)
					{
						foreach (string user in s.accounts)
						{
							accountsDict.Add(user, null);
						}
					}

				}
			}
		}

		internal void SaveSettings()
		{
			using (StreamWriter sw = new StreamWriter("settings.json"))
			using (JsonWriter writer = new JsonTextWriter(sw))
			{
				JsonSerializer serializer = new JsonSerializer();
				// read the json from a stream

				// json size doesn't matter because only a small piece is read at a time from the HTTP request

				StoredSettings s = new StoredSettings();
				s.pollperiod = pollperiod;
				s.reminderperiod = reminderperiod;
				s.accounts = accountsDict.Keys.ToArray();
				serializer.Serialize(writer, s);
			}
		}
	}
}
