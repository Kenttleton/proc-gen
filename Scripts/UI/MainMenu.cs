using Godot;
using System.Threading.Tasks;

/// <summary>
/// LEARNING: The main menu handles world generation in the background.
/// When "New Game" is clicked, we show a generation overlay and stream
/// progress updates while the world generates asynchronously.
/// </summary>
public partial class MainMenu : Control
{
	private ColorRect _background;
	// UI Elements
	private Button _newGameButton;
	private Button _continueButton;
	private Button _settingsButton;
	private Button _quitButton;

	// Generation overlay
	private Panel _generationOverlay;
	private ProgressBar _generationProgress;
	private Label _generationStatus;

	// World generation
	private WorldLoader _worldLoader;
	private bool _isGenerating = false;

	public override void _Ready()
	{
		var viewportSize = GetViewportRect().Size;
		// Get background and set to screen size
		_background = new ColorRect();
		_background.Color = new Color(0.1f, 0.1f, 0.1f); // Dark background
		_background.Size = viewportSize;
		AddChild(_background);

		// Get menu buttons
		_newGameButton = GetNode<Button>("VBoxContainer/NewGameButton");
		_continueButton = GetNode<Button>("VBoxContainer/ContinueButton");
		_settingsButton = GetNode<Button>("VBoxContainer/SettingsButton");
		_quitButton = GetNode<Button>("VBoxContainer/QuitButton");

		// Center menu
		var menuBox = GetNode<VBoxContainer>("VBoxContainer");
		menuBox.AnchorLeft = 0.5f;
		menuBox.AnchorRight = 0.5f;
		menuBox.AnchorTop = 0.5f;
		menuBox.AnchorBottom = 0.5f;
		menuBox.OffsetLeft = -menuBox.Size.X / 2;
		menuBox.OffsetTop = -menuBox.Size.Y / 2;

		// Get generation overlay
		_generationOverlay = GetNode<Panel>("GenerationOverlay");
		_generationProgress = GetNode<ProgressBar>("GenerationOverlay/VBoxContainer/ProgressBar");
		_generationStatus = GetNode<Label>("GenerationOverlay/VBoxContainer/ProgressLabel");

		// Initially hide generation overlay
		_generationOverlay.Visible = false;
		_generationOverlay.Size = viewportSize;

		var overlayStyle = new StyleBoxFlat();
		overlayStyle.BgColor = new Color(0, 0, 0, 0.8f);
		_generationOverlay.AddThemeStyleboxOverride("panel", overlayStyle);

		// Connect button signals
		_newGameButton.Pressed += OnNewGamePressed;
		_continueButton.Pressed += OnContinuePressed;
		_settingsButton.Pressed += OnSettingsPressed;
		_quitButton.Pressed += OnQuitPressed;

		// Check if world exists to enable/disable Continue button
		bool worldExists = FileAccess.FileExists("user://world_data/index.dat");
		_continueButton.Disabled = !worldExists;

		// Create world loader
		_worldLoader = new WorldLoader();
		AddChild(_worldLoader);

		// Listen to generation progress
		_worldLoader.GenerationProgress += OnGenerationProgress;
		_worldLoader.GenerationComplete += OnGenerationComplete;
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

	private async void OnNewGamePressed()
	{
		if (_isGenerating) return;

		// Show generation overlay
		_generationOverlay.Visible = true;
		_isGenerating = true;

		// Disable menu buttons
		SetMenuButtonsEnabled(false);

		// Configure world generation settings
		var settings = new WorldGenerationSettings
		{
			Seed = (int)GD.Randi(),
			WorldSizeChunks = new Vector2I(45, 35),
			ChunkSize = new Vector2I(64, 64),
			HeightScale = 10.0f,
			NoiseScale = 0.02f
		};

		// Start generation asynchronously
		await _worldLoader.GenerateWorldAsync(settings);

		// WorldLoader will emit GenerationComplete when done
	}

	private void OnContinuePressed()
	{
		// Load existing world
		LoadWorld();
	}

	private void OnSettingsPressed()
	{
		// TODO: Open settings menu
		GD.Print("Settings not implemented yet");
	}

	private void OnQuitPressed()
	{
		GetTree().Quit();
	}

	private void OnGenerationProgress(float progress, string status)
	{
		// LEARNING: This is called from WorldLoader as generation progresses
		// We update the UI on the main thread
		_generationProgress.Value = progress;
		_generationStatus.Text = status;
	}

	private void OnGenerationComplete(bool success)
	{
		_isGenerating = false;

		if (success)
		{
			_generationStatus.Text = "World generated! Starting game...";
			_generationProgress.Value = 100;

			// Wait a moment then load the world
			GetTree().CreateTimer(1.0).Timeout += LoadWorld;
		}
		else
		{
			_generationStatus.Text = "Generation failed. Please try again.";
			_generationOverlay.Visible = false;
			SetMenuButtonsEnabled(true);
		}
	}

	private void LoadWorld()
	{
		// Transition to the game world
		GetTree().ChangeSceneToFile("res://Scenes/World.tscn");
	}

	private void SetMenuButtonsEnabled(bool enabled)
	{
		_newGameButton.Disabled = !enabled;
		_continueButton.Disabled = !enabled;
		_settingsButton.Disabled = !enabled;
		_quitButton.Disabled = !enabled;
	}
}