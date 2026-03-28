using System.Collections.ObjectModel;
using System.Text.Json;
using PipesPuzzle.Game;

namespace PipesPuzzle;

public partial class MainPage : ContentPage
{
	private static readonly int[] SupportedSizes = [4, 5, 6, 8, 10, 12, 15, 20, 25];
	private const string LeaderboardKey = "pipes.leaderboard.v1";

	private readonly Random _rng = new();
	private readonly PipeGameBoard _game;
	private readonly PipeBoardDrawable _drawable;
	private readonly IDispatcherTimer _timer;
	private readonly ObservableCollection<LeaderboardEntry> _leaderboardItems = [];
	private Dictionary<int, List<int>> _leaderboard = [];

	private int _selectedSize = 8;
	private int _leaderboardSize = 8;
	private double _zoom = 1.0;
	private double _baseCell = 28;
	private TimeSpan _elapsed = TimeSpan.Zero;
	private bool _isCompleted;
	private bool _isPlaying;

	public MainPage()
	{
		InitializeComponent();

		_game = new PipeGameBoard(_rng);
		_drawable = new PipeBoardDrawable { Board = _game };
		BoardView.Drawable = _drawable;
		LeaderboardList.ItemsSource = _leaderboardItems;

		_timer = Dispatcher.CreateTimer();
		_timer.Interval = TimeSpan.FromSeconds(1);
		_timer.Tick += OnTimerTick;

		LoadLeaderboard();
		BuildLandingSizeButtons();
		BuildLeaderboardSizeButtons();
		ShowLandingView();
	}

	private void BuildLandingSizeButtons()
	{
		LandingSizeButtonsHost.Children.Clear();

		foreach (var size in SupportedSizes)
		{
			var button = new Button
			{
				Text = $"{size}x{size}",
				Margin = new Thickness(0, 0, 8, 8),
				BackgroundColor = size == _selectedSize ? Color.FromArgb("#111111") : Color.FromArgb("#F1F1F1"),
				TextColor = size == _selectedSize ? Colors.White : Color.FromArgb("#111111"),
				BorderColor = Color.FromArgb("#222222"),
				BorderWidth = 1,
				CornerRadius = 10,
				Padding = new Thickness(12, 8)
			};

			button.Clicked += (_, _) =>
			{
				_selectedSize = size;
				BuildLandingSizeButtons();
				BuildLeaderboardSizeButtons();
				StartPuzzle(size);
				ShowGameView();
			};

			LandingSizeButtonsHost.Children.Add(button);
		}
	}

	private void BuildLeaderboardSizeButtons()
	{
		LeaderboardSizeButtonsHost.Children.Clear();

		foreach (var size in SupportedSizes)
		{
			var button = new Button
			{
				Text = $"{size}x{size}",
				Margin = new Thickness(0, 0, 8, 8),
				BackgroundColor = size == _leaderboardSize ? Color.FromArgb("#111111") : Color.FromArgb("#F1F1F1"),
				TextColor = size == _leaderboardSize ? Colors.White : Color.FromArgb("#111111"),
				BorderColor = Color.FromArgb("#222222"),
				BorderWidth = 1,
				CornerRadius = 10,
				Padding = new Thickness(12, 8)
			};

			button.Clicked += (_, _) =>
			{
				_leaderboardSize = size;
				BuildLeaderboardSizeButtons();
				RefreshLeaderboardForSize(size);
			};

			LeaderboardSizeButtonsHost.Children.Add(button);
		}
	}

	private void StartPuzzle(int size)
	{
		_game.NewPuzzle(size);
		_isCompleted = false;
		_isPlaying = true;
		_elapsed = TimeSpan.Zero;

		GameTitleLabel.Text = $"{size}x{size} Puzzle";
		StatusLabel.Text = "Water is always flowing from the red valve. Only connected pipes fill with water.";
		TimerLabel.Text = FormatTime(_elapsed);

		RefreshBoardSizing();
		_game.UpdateWater();

		_timer.Stop();
		_timer.Start();
		BoardView.Invalidate();
	}

	private void ShowLandingView()
	{
		LandingView.IsVisible = true;
		GameView.IsVisible = false;
		LeaderboardView.IsVisible = false;
		_isPlaying = false;
		_timer.Stop();
	}

	private void ShowGameView()
	{
		LandingView.IsVisible = false;
		GameView.IsVisible = true;
		LeaderboardView.IsVisible = false;
		_isPlaying = true;
	}

	private void ShowLeaderboardView()
	{
		LandingView.IsVisible = false;
		GameView.IsVisible = false;
		LeaderboardView.IsVisible = true;
		_isPlaying = false;
		_timer.Stop();
		RefreshLeaderboardForSize(_leaderboardSize);
	}

	private void RefreshBoardSizing()
	{
		if (_game.Size == 0 || BoardFrame.Width <= 1 || BoardFrame.Height <= 1)
		{
			return;
		}

		var available = Math.Min(BoardFrame.Width - 20, BoardFrame.Height - 20);
		_baseCell = Math.Max(12, available / _game.Size);

		var boardPixel = _game.Size * _baseCell * _zoom;
		BoardView.WidthRequest = boardPixel;
		BoardView.HeightRequest = boardPixel;
		_drawable.CellSize = (float)(_baseCell * _zoom);
	}

	private void OnTimerTick(object? sender, EventArgs e)
	{
		if (_isCompleted || !_isPlaying)
		{
			return;
		}

		_elapsed = _elapsed.Add(TimeSpan.FromSeconds(1));
		TimerLabel.Text = FormatTime(_elapsed);
	}

	private void OnNewPuzzleClicked(object? sender, EventArgs e)
	{
		StartPuzzle(_selectedSize);
	}

	private void OnLandingLeaderboardClicked(object? sender, EventArgs e)
	{
		_leaderboardSize = _selectedSize;
		BuildLeaderboardSizeButtons();
		ShowLeaderboardView();
	}

	private void OnGameLeaderboardClicked(object? sender, EventArgs e)
	{
		_leaderboardSize = _selectedSize;
		BuildLeaderboardSizeButtons();
		ShowLeaderboardView();
	}

	private void OnBackToLandingClicked(object? sender, EventArgs e)
	{
		ShowLandingView();
	}

	private void OnZoomChanged(object? sender, ValueChangedEventArgs e)
	{
		_zoom = e.NewValue;
		RefreshBoardSizing();
		BoardView.Invalidate();
	}

	private void OnBoardFrameSizeChanged(object? sender, EventArgs e)
	{
		RefreshBoardSizing();
		BoardView.Invalidate();
	}

	private void OnBoardStartInteraction(object? sender, TouchEventArgs e)
	{
		if (_isCompleted || _game.Size == 0 || !e.Touches.Any())
		{
			return;
		}

		var point = e.Touches.First();
		var cell = _baseCell * _zoom;
		if (cell <= 0)
		{
			return;
		}

		var col = (int)(point.X / cell);
		var row = (int)(point.Y / cell);

		if (_game.RotateAt(row, col))
		{
			BoardView.Invalidate();
			TryCompletePuzzle();
		}
	}

	private void TryCompletePuzzle()
	{
		if (!_game.IsSolved())
		{
			return;
		}

		_isCompleted = true;
		_timer.Stop();

		SaveScore(_selectedSize, (int)_elapsed.TotalSeconds);
		StatusLabel.Text = $"Solved {_selectedSize}x{_selectedSize} in {FormatTime(_elapsed)}";
	}

	private void LoadLeaderboard()
	{
		var json = Preferences.Default.Get(LeaderboardKey, "");
		if (string.IsNullOrWhiteSpace(json))
		{
			_leaderboard = [];
			return;
		}

		try
		{
			_leaderboard = JsonSerializer.Deserialize<Dictionary<int, List<int>>>(json) ?? [];
		}
		catch
		{
			_leaderboard = [];
		}
	}

	private void SaveScore(int size, int seconds)
	{
		if (!_leaderboard.TryGetValue(size, out var list))
		{
			list = [];
			_leaderboard[size] = list;
		}

		list.Add(seconds);
		list.Sort();
		if (list.Count > 10)
		{
			list.RemoveRange(10, list.Count - 10);
		}

		var json = JsonSerializer.Serialize(_leaderboard);
		Preferences.Default.Set(LeaderboardKey, json);
	}

	private void RefreshLeaderboardForSize(int size)
	{
		_leaderboardItems.Clear();
		LeaderboardTitle.Text = $"Top 10 for {size}x{size}";

		if (!_leaderboard.TryGetValue(size, out var list) || list.Count == 0)
		{
			_leaderboardItems.Add(new LeaderboardEntry { Rank = "-", Label = "No recorded times yet", TimeText = "" });
			return;
		}

		for (var i = 0; i < list.Count && i < 10; i++)
		{
			_leaderboardItems.Add(new LeaderboardEntry
			{
				Rank = (i + 1).ToString(),
				Label = $"{size}x{size}",
				TimeText = FormatTime(TimeSpan.FromSeconds(list[i]))
			});
		}
	}

	private static string FormatTime(TimeSpan span)
	{
		return $"{(int)span.TotalMinutes:00}:{span.Seconds:00}";
	}
}

public sealed class LeaderboardEntry
{
	public string Rank { get; init; } = string.Empty;
	public string Label { get; init; } = string.Empty;
	public string TimeText { get; init; } = string.Empty;
}
