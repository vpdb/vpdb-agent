﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NLog;
using PusherClient;
using ReactiveUI;
using VpdbAgent.PinballX;
using VpdbAgent.PinballX.Models;
using VpdbAgent.Vpdb;
using VpdbAgent.Vpdb.Download;
using VpdbAgent.Vpdb.Models;
using Game = VpdbAgent.Models.Game;

namespace VpdbAgent.Application
{
	/// <summary>
	/// Our internal game API.
	/// 
	/// It manages the games that are defined in the user's XML database of
	/// PinballX. However, since we're dealing with more data than what's in
	/// those XMLs, we keep our own data structure in JSON files.
	/// 
	/// JSON files are system-specific, meaning for every system defined in
	/// PinballX (we call them "Platforms"), there is one JSON file. Games
	/// are always linked to the respective system so we know where to retrieve
	/// table files, media etc.
	/// 
	/// This is how the JSON files are generated:
	///   1. GameManager instantiates MenuManager which parses PinballX.ini
	///   2. GameManager loops through parsed systems and retrieves local vpdb.jsons
	///   3. GameManager merges games from MenuManager and vpdb.json to new vpdb.jsons
	///   4. GameManager dumps new vpdb.jsons
	/// 
	/// Everything is event- or subscription based, since we want to automatically
	/// repeat the process when relevant files change. 
	/// </summary>
	public interface IGameManager
	{
		/// <summary>
		/// Games are 2-way mapped to <see cref="Game"/>, where downstream 
		/// changes (e.g. XMLs in PinballX's database folder change) come from
		/// <see cref="PinballXGame"/> and upstream changes are written
		/// to the .json file sitting in the system's database folder.
		/// </summary>
		ReactiveList<Game> Games { get; }

		/// <summary>
		/// A one-time message fired when everything has been initialized and
		/// GUI can start adding its own subscriptions without re-updating
		/// during initialization.
		/// </summary>
		IObservable<Unit> Initialized { get; }

		/// <summary>
		/// Initializes the menu manager, which basically starts watching
		/// relevant files.
		/// </summary>
		/// <returns>This instance</returns>
		IGameManager Initialize();

		/// <summary>
		/// Links a release from VPDB to a game.
		/// </summary>
		/// <param name="game">Local game to link to</param>
		/// <param name="release">Release from VPDB</param>
		/// <param name="fileId">File ID at VPDB</param>
		/// <returns>This instance</returns>
		IGameManager LinkRelease(Game game, Release release, string fileId);

		/// <summary>
		/// Explicitly enables syncing of a game.
		/// </summary>
		/// <remarks>
		/// Results in an immediate update if available.
		/// </remarks>
		/// <param name="game">Game to synchronize</param>
		/// <returns>This instance</returns>
		IGameManager Sync(Game game);
	}

	/// <summary>
	/// Application logic for <see cref="IGameManager"/>.
	/// </summary>
	public class GameManager : IGameManager
	{
		// deps
		private readonly IMenuManager _menuManager;
		private readonly IVpdbClient _vpdbClient;
		private readonly ISettingsManager _settingsManager;
		private readonly IDownloadManager _downloadManager;
		private readonly IDatabaseManager _databaseManager;
		private readonly IVersionManager _versionManager;
		private readonly IPlatformManager _platformManager;
		private readonly IMessageManager _messageManager;
		private readonly Logger _logger;

		// props
		public ReactiveList<Game> Games { get; } = new ReactiveList<Game>();
		public IObservable<Unit> Initialized => _initialized;

		// privates
		private readonly Subject<Unit> _initialized = new Subject<Unit>();
		private bool _isInitialized;
		private readonly List<Tuple<string, string, string>> _gamesToLink = new List<Tuple<string, string, string>>();

		public GameManager(IMenuManager menuManager, IVpdbClient vpdbClient, ISettingsManager 
			settingsManager, IDownloadManager downloadManager, IDatabaseManager databaseManager,
			IVersionManager versionManager, IPlatformManager platformManager, IMessageManager messageManager,
			Logger logger)
		{
			_menuManager = menuManager;
			_vpdbClient = vpdbClient;
			_settingsManager = settingsManager;
			_downloadManager = downloadManager;
			_databaseManager = databaseManager;
			_versionManager = versionManager;
			_platformManager = platformManager;
			_messageManager = messageManager;
			_logger = logger;

			// setup game change listener once all games are fetched.
			_menuManager.Initialized.Subscribe(_ => SetupGameChanges());

			// update releases from VPDB on the first run, but delay it a bit so it 
			// doesn't do all that shit at the same time!
			_settingsManager.ApiAuthenticated
				.Where(user => user != null)
				.Take(1)
				.Delay(TimeSpan.FromSeconds(2))
				.Subscribe(_ => UpdateReleases());

			// subscribe to pusher
			_vpdbClient.UserChannel.Subscribe(OnChannelJoined);

			// subscribe to downloaded releases
			_downloadManager.WhenReleaseDownloaded.Subscribe(OnReleaseDownloaded);

			// link games if new games are added 
			Games.Changed.Subscribe(_ => CheckGameLinks());

			// setup handlers for table file changes
			_menuManager.TableFileChanged.Subscribe(OnTableFileChanged);
			_menuManager.TableFileRemoved.Subscribe(OnTableFileRemoved);
		}

		public IGameManager Initialize()
		{
			// settings must be initialized before doing this.
			if (string.IsNullOrEmpty(_settingsManager.Settings.ApiKey)) {
				throw new InvalidOperationException("Must initialize settings before game manager.");
			}

			_databaseManager.Initialize();
			_menuManager.Initialize();
			_vpdbClient.Initialize();
			_versionManager.Initialize();

			// validate settings and retrieve profile
			Task.Run(async () => await _settingsManager.Validate(_settingsManager.Settings, _messageManager));

			return this;
		}

		public IGameManager LinkRelease(Game game, Release release, string fileId)
		{
			_logger.Info("Linking {0} to {1} ({2})", game, release, fileId);
			AddRelease(release);
			game.ReleaseId = release.Id;
			game.FileId = fileId;
			game.Release = release;

			// also update in case we didn't catch the last version.
			_vpdbClient.Api.GetRelease(release.Id).Subscribe(updatedRelease => {
				AddRelease(updatedRelease);
				game.Release = updatedRelease;
			}, exception => _vpdbClient.HandleApiError(exception, "retrieving release details during linking"));

			return this;
		}

		public IGameManager Sync(Game game)
		{
			_vpdbClient.Api.GetRelease(game.ReleaseId).Subscribe(release => {
				var latestFile = game.FindUpdate(release);
				if (latestFile != null) {
					_logger.Info("Found updated file {0} for {1}, adding to download queue.", latestFile, release);
					_downloadManager.DownloadRelease(release, latestFile);
				} else {
					_logger.Info("No update found for {0}", release);
				}
			}, exception => _vpdbClient.HandleApiError(exception, "retrieving release details during sync"));
			return this;
		}

		/// <summary>
		/// Sets up a listener that updates our global game list when either games
		/// added or removed, or platforms are added or removed.
		/// </summary>
		/// <seealso cref="http://stackoverflow.com/questions/15254708/"/>
		/// <remarks>
		/// This 
		/// </remarks>
		private void SetupGameChanges()
		{
			// here we push all games in all platforms into the Games list.
			var whenPlatformsOrGamesInThosePlatformsChange = Observable.Merge(
				_platformManager.Platforms.Changed                                                      // one of the games changes
					.SelectMany(_ => _platformManager.Platforms.Select(x => x.Games.Changed).Merge())
					.Select(_ => Unit.Default),
				_platformManager.Platforms.Changed.Select(_ => Unit.Default));                          // one of the platforms changes

			whenPlatformsOrGamesInThosePlatformsChange
				.StartWith(Unit.Default)
				.Select(_ => _platformManager.Platforms.SelectMany(x => x.Games).ToList())
				.Where(games => games.Count > 0)
				.Subscribe(games => {
					System.Windows.Application.Current.Dispatcher.Invoke(delegate {
						// TODO better logic
						using (Games.SuppressChangeNotifications()) {
							Games.Clear();
							Games.AddRange(games);
						}
						_logger.Info("Set {0} games.", games.Count);

						if (!_isInitialized) {
							_initialized.OnNext(Unit.Default);
							_initialized.OnCompleted();
							_isInitialized = true;
						}
					});
				});
		}

		/// <summary>
		/// Checks if any games are to be linked to a release. Executed each time
		/// games change.
		/// </summary>
		/// See <see cref="AddGame"/> for an explanation.
		private void CheckGameLinks()
		{
			if (_gamesToLink.Count > 0) {
				for (var i = _gamesToLink.Count - 1; i >= 0; i--) {
					var x = _gamesToLink[i];
					var game = Games.FirstOrDefault(g => g.Id.Equals(x.Item1));
					var release = _databaseManager.Database.Releases[x.Item2];
					LinkRelease(game, release, x.Item3);
					_gamesToLink.RemoveAt(i);
				}
			}
		}

		/// <summary>
		/// Adds a release to the global database. If already added, the release
		/// is updated.
		/// </summary>
		/// <param name="release">Release to add or update</param>
		private void AddRelease(Release release)
		{
			if (!_databaseManager.Database.Releases.ContainsKey(release.Id)) {
				_databaseManager.Database.Releases.Add(release.Id, release);
			} else {
				_databaseManager.Database.Releases[release.Id].Update(release);
			}
			_databaseManager.Save();
		}

		/// <summary>
		/// Retrieves all known releases from VPDB and updates them locally.
		/// 
		/// todo include file id when searching. (also needs update on backend.)
		/// </summary>
		private void UpdateReleases()
		{
			var releaseIds = Games.Where(g => g.HasRelease).Select(g => g.ReleaseId).ToList();
			if (releaseIds.Count > 0) {
				_logger.Info("Updating {0} release(s)", releaseIds.Count);
				_vpdbClient.Api.GetReleasesByIds(string.Join(",", releaseIds))
					.SubscribeOn(Scheduler.Default)
					.Subscribe(releases => {
						// update releases
						foreach (var release in releases) {
							if (!_databaseManager.Database.Releases.ContainsKey(release.Id)) {
								_databaseManager.Database.Releases.Add(release.Id, release);
								
							} else {
								_databaseManager.Database.Releases[release.Id].Update(release);
							}
							// also update the game's release link. todo check perf and use a map if too slow
							var game = Games.FirstOrDefault(g => release.Id.Equals(g.ReleaseId));
							if (game != null) {
								game.Release = _databaseManager.Database.Releases[release.Id];
							}
						}
						// save
						_databaseManager.Save();
					}, exception => _vpdbClient.HandleApiError(exception, "retrieving all known releases by ID"));
			} else {
				_logger.Info("Skipping release update, no linked releases found.");
			}
		}

		private void OnReleaseDownloaded(Job job)
		{
			// find release locally
			var game = Games.FirstOrDefault(g => job.Release.Id.Equals(g.ReleaseId));

			// add release
			AddRelease(job.Release);

			// add or update depending if found or not
			if (game == null) {
				AddGame(job);

			} else {
				UpdateGame(game, job);
			}
		}

		/// <summary>
		/// A table file has been changed or added (or renamed to given path).
		/// </summary>
		/// <param name="path">Absolute path of the file</param>
		private void OnTableFileChanged(string path)
		{
			Games
				.Where(g => g.Filename != null)
				.Where(g => Path.GetFileNameWithoutExtension(g.Filename).Equals(Path.GetFileNameWithoutExtension(path)))
				.ToList()
				.ForEach(g => { g.Exists = true; });
		}

		/// <summary>
		/// A table file has been deleted (or renamed from given path).
		/// </summary>
		/// <param name="path">Absolute path of the file</param>
		private void OnTableFileRemoved(string path)
		{
			Games
				.Where(g => g.Filename != null)
				.Where(g => g.Filename.Equals(Path.GetFileName(path)))
				.ToList()
				.ForEach(g => { g.Exists = false; });
		}

		/// <summary>
		/// Adds a downloaded game to the PinballX database.
		/// </summary>
		/// <param name="job">Job of the downloaded game</param>
		private void AddGame(Job job)
		{
			_logger.Info("Adding {0} to PinballX database...", job.Release);

			var platform = _platformManager.FindPlatform(job.TableFile);
			if (platform == null) {
				_logger.Warn("Cannot find platform for release {0} ({1}), aborting.", job.Release.Id, string.Join(",", job.TableFile.Compatibility));
				return;
			}
			var newGame = _menuManager.NewGame(job);

			// adding the game (updating the xml) forces a new rescan. but it's
			// async so in order to avoid race conditions, we put this into a 
			// "linking" queue, meaning on the next update, it will also get 
			// linked.
			_gamesToLink.Add(new Tuple<string, string, string>(newGame.Description, job.Release.Id, job.File.Id));

			// save new game to Vpdb.xml (and trigger rescan)
			_menuManager.AddGame(newGame, platform.DatabasePath);
		}

		/// <summary>
		/// Updates an existing game in the PinballX database
		/// </summary>
		/// <remarks>
		/// Usually happends when a game is updated to a new version.
		/// </remarks>
		/// <param name="game">Game to be updated</param>
		/// <param name="job">Job of downloaded game</param>
		private void UpdateGame(Game game, Job job)
		{
			_logger.Info("Updating {0} in PinballX database...", job.Release);

			var oldFileName = Path.GetFileNameWithoutExtension(game.Filename);
				
			// update and save json
			game.FileId = Path.GetFileNameWithoutExtension(job.FilePath);

			// update and save xml
			_menuManager.UpdateGame(oldFileName, game);
		}

		/// <summary>
		/// Executed when the pusher connection with the private user channel
		/// is established and we can subscribe to messages.
		/// </summary>
		/// <param name="userChannel">User channel object</param>
		private void OnChannelJoined(Channel userChannel)
		{
			if (userChannel == null) {
				return;
			}

			// subscribe through a subject so we can do more fun stuff with it
			var star = new Subject<JObject>();
			var unstar = new Subject<JObject>();
			userChannel.Bind("star", data =>
			{
				star.OnNext(data as JObject);
			});
			userChannel.Bind("unstar", data =>
			{
				unstar.OnNext(data as JObject);
			});

			// star
			star.ObserveOn(RxApp.MainThreadScheduler).Subscribe(data => {
				var id = data.GetValue("id").Value<string>();
				if ("release".Equals(data.GetValue("type").Value<string>())) {
					var game = OnStarRelease(id, true);
					if (game == null) {
						if (_settingsManager.Settings.SyncStarred) {
							_downloadManager.DownloadRelease(id);
						} else {
							_logger.Info("Sync starred not enabled, ignoring starred release.");
						}
					}
				}
			});

			// unstar
			unstar.ObserveOn(RxApp.MainThreadScheduler).Subscribe(data => {
				if ("release".Equals(data.GetValue("type").Value<string>())) {
					OnStarRelease(data.GetValue("id").Value<string>(), false);
				}
			});
		}

		private Game OnStarRelease(string id, bool star) {
			var game = Games.FirstOrDefault(g => !string.IsNullOrEmpty(g.ReleaseId) && g.ReleaseId.Equals(id));
			if (game != null) {
				var release = _databaseManager.Database.Releases[id];
				release.Starred = star;
				game.Release.Starred = star;
				if (_settingsManager.Settings.SyncStarred) {
					game.IsSynced = star;
				}
				_databaseManager.Save();
				_logger.Info("Toggled star on release {0} [{1}]", release.Name, star ? "on" : "off");
			}
			return game;
		}

	}
}