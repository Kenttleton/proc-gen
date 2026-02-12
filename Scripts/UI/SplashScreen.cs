using Godot;
using System.Threading.Tasks;

/// <summary>
/// LEARNING: The splash screen shows while we check for world data.
/// If world exists, we go to main menu. If not, we can generate here or in menu.
/// </summary>
public partial class SplashScreen : Control
{
	private ColorRect _background;
	private CenterContainer _centerContainer;
	private TextureRect _studioLogo;
	private TextureRect _gameLogo;

	private const string WorldDataIndexPath = "res://world_data/index.dat";

	public override async void _Ready()
	{
		var viewportSize = GetViewportRect().Size;
		// Get background and set to screen size
		_background = new ColorRect();
		_background.Color = new Color(0.1f, 0.1f, 0.1f); // Dark background
		_background.Size = viewportSize;
		AddChild(_background);

		_centerContainer = new CenterContainer();
		_centerContainer.Size = viewportSize;
		AddChild(_centerContainer);
		var vboxContainer = new VBoxContainer();
		_centerContainer.AddChild(vboxContainer);

		_studioLogo = new TextureRect();
		_studioLogo.Texture = GD.Load<Texture2D>("res://Images/KenttletonStudios2.png");
		_studioLogo.Size = viewportSize;
		vboxContainer.AddChild(_studioLogo);

		// Setup UI
		_gameLogo = new TextureRect();
		_gameLogo.Texture = GD.Load<Texture2D>("res://Images/FracturedRealms.png");
		_gameLogo.Size = viewportSize;
		_gameLogo.Visible = false;
		vboxContainer.AddChild(_gameLogo);

		// Start loading process
		StartLoadingAsync();
		await FadeTo(_studioLogo, _gameLogo, duration: 1.0f);
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		var viewportSize = GetViewportRect().Size;
		if (viewportSize != _background.Size)
		{
			_background.Size = viewportSize;
		}
	}

	private async Task StartLoadingAsync()
	{
		await Task.Delay(1000);
		bool worldExists = FileAccess.FileExists(WorldDataIndexPath);
		_studioLogo.Visible = false;
		_gameLogo.Visible = true;
		await Task.Delay(1000);
		GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
	}

	public async Task FadeTo(TextureRect from, TextureRect to, float duration = 1.0f)
	{
		to.Modulate = new Color(1, 1, 1, 0);
		to.Show();

		var tween = CreateTween();
		tween.SetParallel(true);

		tween.TweenProperty(from, "modulate:a", 0.0f, duration);
		tween.TweenProperty(to, "modulate:a", 1.0f, duration);

		await ToSignal(tween, Tween.SignalName.Finished);

		// Swap references
		var temp = from;
		from = to;
		to = temp;

		to.Hide();
	}
}
