using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MailChecker
{
	class Startup
	{
		// The following code is based on:
		// http://stackoverflow.com/q/9611154
		// the autoembedding is based on:
		// http://blogs.interknowlogy.com/2011/07/13/merging-a-wpf-application-into-a-single-exe/
		
		static private Dictionary<String, Assembly> assemblyDict = new Dictionary<String, Assembly>();
		private static TraceSource logging =
			new TraceSource("MailChecker.Startup");
		[STAThread]
		public static void Main()
		{
			AppDomain currentDomain = AppDomain.CurrentDomain;
			currentDomain.UnhandledException += new UnhandledExceptionEventHandler(ExceptionHandler);

			currentDomain.AssemblyResolve += OnResolveAssembly;
			Assembly executingAssembly = Assembly.GetExecutingAssembly();
			string[] resources = executingAssembly.GetManifestResourceNames();
			// load all dll resources into assembly dict
			foreach (string resource in resources)
			{
				if (resource.EndsWith(".dll"))
				{
					using (Stream stream = executingAssembly.GetManifestResourceStream(resource))
					{
						if (stream == null)
							continue;

						byte[] assemblyRawBytes = new byte[stream.Length];
						stream.Read(assemblyRawBytes, 0, assemblyRawBytes.Length);
						try
						{
							assemblyDict.Add(resource, Assembly.Load(assemblyRawBytes));
						}
						catch (Exception ex)
						{
							System.Diagnostics.Debug.Print("Failed to load: " + resource + " Exception: " + ex.Message);
						}
					}
				}
			}
				
			App.Main(); // Run WPF startup code.

		}

		private static void ExceptionHandler(object sender, UnhandledExceptionEventArgs e)
		{
			logging.TraceEvent(TraceEventType.Critical, 1, "CRASH: " + e.ExceptionObject);
		}

		private static Assembly OnResolveAssembly(object sender, ResolveEventArgs args)
		{
			Assembly executingAssembly = Assembly.GetExecutingAssembly();
			AssemblyName assemblyName = new AssemblyName(args.Name);

			string path = assemblyName.Name + ".dll";

			if (assemblyDict.ContainsKey(path))
			{
				return assemblyDict[path];
			}
			return null;
		}
	}
}
