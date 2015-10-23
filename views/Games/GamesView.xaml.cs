﻿using System.Windows;
using System.Windows.Controls;
using NLog;
using ReactiveUI;
using VpdbAgent.ViewModels.Games;

namespace VpdbAgent.Views.Games
{
	/// <summary>
	/// Interaction logic for MainPage.xaml
	/// </summary>
	public partial class GamesView : UserControl, IViewFor<GamesViewModel>
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public GamesView()
		{
			InitializeComponent();

			//this.WhenAnyValue(x => x.ViewModel).BindTo(this, x => x.DataContext);
			this.WhenActivated(d =>
			{
				d(this.OneWayBind(ViewModel, vm => vm.Platforms, v => v.PlatformList.ItemsSource));
				d(this.OneWayBind(ViewModel, vm => vm.Games, v => v.GameList.ItemsSource));
			});
		}

		public void OnPlatformFilterChanged(object sender, object e)
		{
			ViewModel.OnPlatformFilterChanged(sender, e);
		}

		#region ViewModel
		public GamesViewModel ViewModel
		{
			get { return (GamesViewModel)this.GetValue(ViewModelProperty); }
			set { this.SetValue(ViewModelProperty, value); }
		}

		public static readonly DependencyProperty ViewModelProperty =
		   DependencyProperty.Register("ViewModel", typeof(GamesViewModel), typeof(GamesView), new PropertyMetadata(null));

		object IViewFor.ViewModel
		{
			get { return ViewModel; }
			set { ViewModel = (GamesViewModel)value; }
		}
		#endregion
	}
}