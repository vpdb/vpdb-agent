﻿using System;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Controls;
using NLog;
using ReactiveUI;
using VpdbAgent.Application;
using VpdbAgent.Models;
using VpdbAgent.Vpdb;

namespace VpdbAgent.ViewModels.Games
{
	public class GamesViewModel : ReactiveObject
	{
		// dependencies
		private readonly IGameManager _gameManager;
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		// data
		public IReactiveDerivedList<Platform> Platforms { get; }
		public IReactiveDerivedList<GameItemViewModel> Games { get; }

		// commands
		public ReactiveCommand<object> FilterPlatforms { get; protected set; } = ReactiveCommand.Create();

		// privates
		private readonly ReactiveList<string> _platformFilter = new ReactiveList<string>();
		private readonly IReactiveDerivedList<GameItemViewModel> _allGames;

		public GamesViewModel(IGameManager gameManager)
		{
			_gameManager = gameManager;

			// setup init listener
			_gameManager.Initialized.Subscribe(_ => SetupSubscriptions());

			// create platforms, filtered and sorted
			Platforms = _gameManager.Platforms.CreateDerivedCollection(
				platform => platform,
				platform => platform.IsEnabled,
				(x, y) => string.Compare(x.Name, y.Name, StringComparison.Ordinal)
			);

			// push all games into AllGames as view models and sorted
			_allGames = _gameManager.Games.CreateDerivedCollection(
				game => new GameItemViewModel(game),
				gameViewModel => true,
				(x, y) => string.Compare(x.Game.Id, y.Game.Id, StringComparison.Ordinal)
			);

			// push filtered game view models into Games
			Games = _allGames.CreateDerivedCollection(
				gameViewModel => gameViewModel, 
				gameViewModel => gameViewModel.IsVisible);
		}

		private void SetupSubscriptions()
		{
			// update platform filter when platforms change
			Platforms.Changed
				.Select(_ => Unit.Default)
				.StartWith(Unit.Default)
				.Subscribe(UpdatePlatforms);
			
			_allGames.ChangeTrackingEnabled = true;

			// just print that we're happy
			_allGames.Changed.Subscribe(_ =>
			{
				Logger.Info("We've got {0} games, {1} in total.", Games.Count, _allGames.Count);
			});

			// update games view models when platform filter changes
			_platformFilter.Changed
				.Select(_ => Unit.Default)
				.StartWith(Unit.Default)
				.Subscribe(UpdatePlatformFilter);
		}

		/// <summary>
		/// The click event from the view that toggles a given platform filter.
		/// </summary>
		/// <param name="platformName">Name of the platform that was toggled</param>
		/// <param name="isChecked">True if enabled, false otherwise.</param>
		public void OnPlatformFilterChanged(string platformName, bool isChecked)
		{
			if (isChecked) {
				_platformFilter.Add(platformName);
			} else {
				_platformFilter.Remove(platformName);
			}
		}

		/// <summary>
		/// Updates the IsVisible flag on all games in order to filter
		/// depending on the selected platforms.
		/// </summary>
		/// <param name="args">Change arguments from ReactiveList</param>
		private void UpdatePlatformFilter(Unit args)
		{
			using (_allGames.SuppressChangeNotifications()) {
				foreach (var gameViewModel in _allGames) {
					gameViewModel.IsVisible =
						gameViewModel.Game.Platform.IsEnabled &&
						_platformFilter.Contains(gameViewModel.Game.Platform.Name);
				}
			}
		}

		/// <summary>
		/// Updates the platform filter when platforms change.
		/// </summary>
		/// <param name="args">Change arguments from ReactiveList</param>
		private void UpdatePlatforms(Unit args)
		{
			// populate filter
			using (_platformFilter.SuppressChangeNotifications()) {
				_platformFilter.Clear();
				_platformFilter.AddRange(Platforms.Select(p => p.Name));
			};
			Logger.Info("We've got {0} platforms, {2} visible, {1} in total.", Platforms.Count, _gameManager.Platforms.Count, _platformFilter.Count);

		}
	}
}
