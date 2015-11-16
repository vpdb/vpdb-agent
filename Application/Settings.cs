﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Akavache;
using ReactiveUI;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent.Application
{
	public class Settings : ReactiveObject
	{
		/// <summary>
		/// VPDB's API key
		/// </summary>
		public string ApiKey { get { return _apiKey; } set { this.RaiseAndSetIfChanged(ref _apiKey, value); } }

		/// <summary>
		/// If HTTP Basic authentication is enabled on VPDB, this is the user name.
		/// </summary>
		public string AuthUser { get { return _authUser; } set { this.RaiseAndSetIfChanged(ref _authUser, value); } }

		/// <summary>
		/// If HTTP Basic authentication is enabled on VPDB, this is the password.
		/// </summary>
		public string AuthPass { get { return _authPass; } set { this.RaiseAndSetIfChanged(ref _authPass, value); } }

		/// <summary>
		/// The endpoint of the VPDB API.
		/// </summary>
		public string Endpoint { get { return _endpoint; } set { this.RaiseAndSetIfChanged(ref _endpoint, value); } }

		/// <summary>
		/// The local folder where the user installed PinballX
		/// </summary>
		public string PbxFolder { get { return _pbxFolder; } set { this.RaiseAndSetIfChanged(ref _pbxFolder, value); } }

		/// <summary>
		/// If true, starring a release on vpdb.io will make it synced here.
		/// </summary>
		public bool SyncStarred { get { return _syncStarred; } set { this.RaiseAndSetIfChanged(ref _syncStarred, value); } }

		/// <summary>
		/// If true, download all starred/synced releases on startup.
		/// </summary>
		public bool DownloadOnStartup { get { return _downloadOnStartup; } set { this.RaiseAndSetIfChanged(ref _downloadOnStartup, value); } }

		/// <summary>
		/// Primary orientation when downloading a release
		/// </summary>
		public SettingsManager.Orientation DownloadOrientation { get { return _downloadOrientation; } set { this.RaiseAndSetIfChanged(ref _downloadOrientation, value); } }

		/// <summary>
		/// If primary orientation is not available, use this if available (otherwise, ignore)
		/// </summary>
		public SettingsManager.Orientation DownloadOrientationFallback { get { return _downloadOrientationFallback; } set { this.RaiseAndSetIfChanged(ref _downloadOrientationFallback, value); } }

		/// <summary>
		/// Primary lighting flavor when downloading a release
		/// </summary>
		public SettingsManager.Lighting DownloadLighting { get { return _downloadLighting; } set { this.RaiseAndSetIfChanged(ref _downloadLighting, value); } }

		/// <summary>
		/// If primary lighting is not available, use this if available (otherwise, ignore)
		/// </summary>
		public SettingsManager.Lighting DownloadLightingFallback { get { return _dDownloadLightingFallback; } set { this.RaiseAndSetIfChanged(ref _dDownloadLightingFallback, value); } }

		/// <summary>
		/// Only true until settings are saved for the first time.
		/// </summary>
		public bool IsFirstRun { get { return _isFirstRun; } set { this.RaiseAndSetIfChanged(ref _isFirstRun, value); } }

		/// <summary>
		/// True if validated, false otherwise.
		/// </summary>
		/// <remarks>
		/// The only property that is obviously not persisted.
		/// </remarks>
		public bool IsValidated { get; protected internal set; } = false;

		private string _apiKey;
		private string _authUser;
		private string _authPass;
		private string _endpoint;
		private string _pbxFolder;
		private bool _syncStarred;
		private bool _downloadOnStartup;
		private SettingsManager.Orientation _downloadOrientation;
		private SettingsManager.Orientation _downloadOrientationFallback;
		private SettingsManager.Lighting _downloadLighting;
		private SettingsManager.Lighting _dDownloadLightingFallback;
		private bool _isFirstRun;

		public Settings Copy()
		{
			return Copy(this, new Settings());
		}

		public async Task ReadFromStorage(IBlobCache storage)
		{
			ApiKey = await storage.GetOrCreateObject("ApiKey", () => "");
			AuthUser = await storage.GetOrCreateObject("AuthUser", () => "");
			AuthPass = await storage.GetOrCreateObject("AuthPass", () => "");
			Endpoint = await storage.GetOrCreateObject("Endpoint", () => "https://staging.vpdb.io");
			PbxFolder = await storage.GetOrCreateObject("PbxFolder", () => "");
			SyncStarred = await storage.GetOrCreateObject("SyncStarred", () => true);
			DownloadOnStartup = await storage.GetOrCreateObject("DownloadOnStartup", () => false);
			DownloadOrientation = await storage.GetOrCreateObject("DownloadOrientation", () => SettingsManager.Orientation.Portrait);
			DownloadOrientationFallback = await storage.GetOrCreateObject("DownloadOrientationFallback", () => SettingsManager.Orientation.Same);
			DownloadLighting = await storage.GetOrCreateObject("DownloadLighting", () => SettingsManager.Lighting.Day);
			DownloadLightingFallback = await storage.GetOrCreateObject("DownloadLightingFallback", () => SettingsManager.Lighting.Any);
			IsFirstRun = await storage.GetOrCreateObject("IsFirstRun", () => true);
		}

		public async Task WriteToStorage(IBlobCache storage)
		{
			await storage.InsertObject("ApiKey", ApiKey);
			await storage.InsertObject("AuthUser", AuthUser);
			await storage.InsertObject("AuthPass", AuthPass);
			await storage.InsertObject("Endpoint", Endpoint);
			await storage.InsertObject("PbxFolder", PbxFolder);
			await storage.InsertObject("SyncStarred", SyncStarred);
			await storage.InsertObject("DownloadOnStartup", DownloadOnStartup);
			await storage.InsertObject("DownloadOrientation", DownloadOrientation);
			await storage.InsertObject("DownloadOrientationFallback", DownloadOrientationFallback);
			await storage.InsertObject("DownloadLighting", DownloadLighting);
			await storage.InsertObject("DownloadLightingFallback", DownloadLightingFallback);
			await storage.InsertObject("IsFirstRun", false);
			IsFirstRun = false;
		}

		protected internal static Settings Copy(Settings from, Settings to)
		{
			to.ApiKey = from.ApiKey;
			to.AuthUser = from.AuthUser;
			to.AuthPass = from.AuthPass;
			to.Endpoint = from.Endpoint;
			to.PbxFolder = from.PbxFolder;
			to.SyncStarred = from.SyncStarred;
			to.DownloadOnStartup = from.DownloadOnStartup;
			to.DownloadOrientation = from.DownloadOrientation;
			to.DownloadOrientationFallback = from.DownloadOrientationFallback;
			to.DownloadLighting = from.DownloadLighting;
			to.DownloadLightingFallback = from.DownloadLightingFallback;
			return to;
		}
	}
}