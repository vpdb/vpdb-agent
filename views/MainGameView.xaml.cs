﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.RightsManagement;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using NLog;
using ReactiveUI;
using Splat;
using VpdbAgent.Common;
using VpdbAgent.Controls;
using VpdbAgent.ViewModels;
using VpdbAgent.ViewModels.TypeConverters;
using VpdbAgent.Vpdb;
using VpdbAgent.Vpdb.Models;
using Game = VpdbAgent.Models.Game;

namespace VpdbAgent.Views
{
	/// <summary>
	/// Interaction logic for GameTemplate.xaml
	/// </summary>
	public partial class MainGameView : UserControl, IViewFor<MainGameViewModel>
	{
		public MainGameView()
		{
			InitializeComponent();

			// game data from .xml
			this.OneWayBind(ViewModel, vm => vm.Game.Id, v => v.Title.Text);
			this.OneWayBind(ViewModel, vm => vm.Game.Filename, v => v.Filename.Text);
			this.OneWayBind(ViewModel, vm => vm.Game.Exists, v => v.Filename.Background, null, new BooleanToBrushHint(Brushes.Transparent, Brushes.DarkRed));
			this.OneWayBind(ViewModel, vm => vm.Game.Exists, v => v.IdentifyButton.IsEnabled);

			// visibilities
			this.OneWayBind(ViewModel, vm => vm.Game.HasRelease, v => v.ReleaseNameWrapper.Visibility);
			this.OneWayBind(ViewModel, vm => vm.Game.HasRelease, v => v.Toggles.Visibility);
			this.OneWayBind(ViewModel, vm => vm.Game.HasRelease, v => v.IdentifyButton.Visibility, null, BooleanToVisibilityHint.Inverse);
			this.OneWayBind(ViewModel, vm => vm.IsExecuting, v => v.Spinner.Visibility);

			// vpdb data
			this.OneWayBind(ViewModel, vm => vm.Game.Release.Name, v => v.ReleaseName.Text);
			this.OneWayBind(ViewModel, vm => vm.Game.Release.Starred, v => v.Star.Foreground, null, new BooleanToBrushHint(
				(Brush)FindResource("PrimaryColorBrush"),
				(Brush)FindResource("LabelTextBrush")
			));
			this.OneWayBind(ViewModel, vm => vm.Game.Release.LatestVersion.Thumb.Image, v => v.Thumb.UrlSource);

			// commands
			this.BindCommand(ViewModel, vm => vm.IdentifyRelease, v => v.IdentifyButton);

			// inner views
			this.OneWayBind(ViewModel, vm => vm.ReleaseResults, v => v.ReleaseResultView.ViewModel);
			this.OneWayBind(ViewModel, vm => vm.HasExecuted, v => v.ReleaseResultView.Visibility);
		}

		#region ViewModel
		public MainGameViewModel ViewModel
		{
			get { return (MainGameViewModel)this.GetValue(ViewModelProperty); }
			set { this.SetValue(ViewModelProperty, value); }
		}

		public static readonly DependencyProperty ViewModelProperty =
		   DependencyProperty.Register("ViewModel", typeof(MainGameViewModel), typeof(MainGameView), new PropertyMetadata(null));

		object IViewFor.ViewModel
		{
			get { return ViewModel; }
			set { ViewModel = (MainGameViewModel)value; }
		}
		#endregion
	}
}