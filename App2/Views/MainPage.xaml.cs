using System;
using Windows.UI.Xaml.Controls;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Popups;
using Windows.Storage.Pickers;
using Windows.Media.Audio;
using Windows.Media.Render;
using Windows.UI.Xaml.Controls.Primitives;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Media.MediaProperties;

namespace App2.Views
{
    public sealed partial class MainPage : Page
    {
        private DispatcherTimer positionTimer;

        private DispatcherTimer recordTimer;
        private DateTimeOffset lastTime;
        private TimeSpan totalRunningTime;

        private AudioGraph audioGraph;
        private AudioFileInputNode fileInput;
        private AudioDeviceOutputNode deviceOutput;
        private EchoEffectDefinition echoEffect;

        bool isPlaying = false;
        bool firstLoad = true;

        public MainPage()
        {
            InitializeComponent();
            InitializeAudioGraph();

            positionTimer = new DispatcherTimer();
            positionTimer.Interval = TimeSpan.FromSeconds(1);
            positionTimer.Tick += PositionTimer_Tick;
            positionTimer.Start();


            recordTimer = new DispatcherTimer();
            recordTimer.Tick += Timer_Tick;
            recordTimer.Interval = TimeSpan.FromSeconds(1);

        }
        private void Timer_Tick(object sender, object e)
        {
            DateTimeOffset time = DateTimeOffset.Now;
            TimeSpan span = time - lastTime;
            lastTime = time;
            totalRunningTime += span;
            RecordTimeTextBlock.Text = totalRunningTime.ToString(@"m\:ss");
        }

        private void PositionTimer_Tick(object sender, object e)
        {
            try
            {
                if (isPlaying)
                {
                    // Aktualizuj czas odtwarzania na suwaku
                    Parameter3Slider.Maximum = fileInput.Duration.TotalSeconds;
                    Parameter3Slider.Value = fileInput.Position.TotalSeconds;
                    var timeValue = TimeSpan.FromSeconds(Parameter3Slider.Value);
                    Parameter3Text.Text =timeValue.ToString(@"m\:ss");
                }

            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error while updating position: " + ex.Message);
            }
        }

        private async void InitializeAudioGraph()
        {
            try
            {
                AudioGraphSettings settings = new AudioGraphSettings(AudioRenderCategory.Media);
                CreateAudioGraphResult result = await AudioGraph.CreateAsync(settings);
                if (result.Status != AudioGraphCreationStatus.Success)
                {
                    // Cannot create graph
                    return;
                }

                audioGraph = result.Graph;

                // Create a device output node
                CreateAudioDeviceOutputNodeResult deviceOutputNodeResult = await audioGraph.CreateDeviceOutputNodeAsync();
                if (deviceOutputNodeResult.Status != AudioDeviceNodeCreationStatus.Success)
                {
                    // Cannot create device output node
                    return;
                }

                deviceOutput = deviceOutputNodeResult.DeviceOutputNode;

                // Create an echo effect
                echoEffect = new EchoEffectDefinition(audioGraph);
                echoEffect.WetDryMix = 0.5f; // Adjust to taste
                echoEffect.Feedback = 0.5f; // Adjust to taste
                echoEffect.Delay = 50; // Adjust to taste
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error while initializing audio graph: " + ex.Message);
            }
        }

        private TaskCompletionSource<bool> stopRecordingTaskCompletionSource;
        private async void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var savePicker = new Windows.Storage.Pickers.FileSavePicker();
                savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.MusicLibrary;
                savePicker.FileTypeChoices.Add("WAV files", new List<string> { ".wav" });
                savePicker.SuggestedFileName = "MyRecording.wav";

                StorageFile file = await savePicker.PickSaveFileAsync();
                if (file != null)
                {
                    StopRecordingButton.Visibility = Visibility.Visible;
                    RecordButton.Visibility = Visibility.Collapsed;

                    totalRunningTime = TimeSpan.Zero;
                    lastTime = DateTimeOffset.Now;
                    recordTimer.Start();

                    var mediaCapture = new Windows.Media.Capture.MediaCapture();

                    await mediaCapture.InitializeAsync();

                    var profile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.Auto);
                    await mediaCapture.StartRecordToStorageFileAsync(profile, file);

                    stopRecordingTaskCompletionSource = new TaskCompletionSource<bool>();
                    await stopRecordingTaskCompletionSource.Task;
                    await mediaCapture.StopRecordAsync();
                    recordTimer.Stop();
                }
            }
            catch (Exception ex)
            {
                MessageDialog dialog = new MessageDialog($"Wystąpił błąd podczas nagrywania: {ex.Message}", "Błąd");
                await dialog.ShowAsync();
            }
        }
        private void StopRecordingButton_Click(object sender, RoutedEventArgs e)
        {
            if (stopRecordingTaskCompletionSource != null)
            {
                stopRecordingTaskCompletionSource.SetResult(true);
                StopRecordingButton.Visibility = Visibility.Collapsed;
                RecordButton.Visibility = Visibility.Visible;
            }
        }

        private async void LoadWavButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as ButtonBase;
                button.Click -= LoadWavButton_Click;

                FileOpenPicker openPicker = new FileOpenPicker();
                openPicker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
                openPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.MusicLibrary;
                openPicker.FileTypeFilter.Add(".wav");
                StorageFile file = await openPicker.PickSingleFileAsync();

                if (file != null)
                {
                    isPlaying = true;
                    StartPlayingButton.Visibility = Visibility.Collapsed;
                    PausePlayingButton.Visibility = Visibility.Visible;
                    CreateAudioFileInputNodeResult fileInputResult = await audioGraph.CreateFileInputNodeAsync(file);
                    if (fileInputResult.Status != AudioFileNodeCreationStatus.Success)
                    {
                        // Cannot create file input node
                        return;
                    }
                    if (!firstLoad)
                    {
                        audioGraph.ResetAllNodes();
                        fileInput.EffectDefinitions.Remove(echoEffect);
                        fileInput.RemoveOutgoingConnection(deviceOutput);
                    }
                    firstLoad = false;
                    fileInput = fileInputResult.FileInputNode;
                    fileInput.EffectDefinitions.Add(echoEffect);
                    fileInput.AddOutgoingConnection(deviceOutput);

                    // Start AudioGraph
                    fileInput.Seek(TimeSpan.Zero);
                    Parameter3Slider.Value = 0;
                    audioGraph.Start();
                }
                button.Click += LoadWavButton_Click;
            }
            catch (NullReferenceException ex)
            {
                ShowErrorMessage("Error while loading WAV file: " + ex.Message);
            }
        }

        private async void ShowErrorMessage(string message)
        {
            var dialog = new MessageDialog(message, "Error");
            await dialog.ShowAsync();
        }

        // Empty methods to match XAML definitions
        private void Parameter1Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            try
            {
                if (deviceOutput != null)
                {
                    deviceOutput.OutgoingGain = (float)Parameter1Slider.Value/100;
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error while adjusting volume: " + ex.Message);
            }
        }
        private void Parameter2Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            try
            {
                if(isPlaying)
                {
                    // Aktualizuj prędkość odtwarzania
                    fileInput.PlaybackSpeedFactor = (float)Parameter2Slider.Value;
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error while seeking: " + ex.Message);
            }
        }
        private void Parameter3Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            try
            {
                if (isPlaying)
                {
                    // Aktualizuj czas odtwarzania
                    fileInput.Seek(TimeSpan.FromSeconds(Parameter3Slider.Value));
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error while seeking: " + ex.Message);
            }
        }
        private void Start_Playing(object sender, RoutedEventArgs e)
        {
            try
            {
                // Rozpocznij odtwarzanie
                audioGraph.Start();
                StartPlayingButton.Visibility = Visibility.Collapsed;
                PausePlayingButton.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error while starting playback: " + ex.Message);
            }
        }
        private void Pause_Playing(object sender, RoutedEventArgs e)
        {
            try
            {
                // Rozpocznij odtwarzanie
                audioGraph.Stop();
                StartPlayingButton.Visibility = Visibility.Visible;
                PausePlayingButton.Visibility = Visibility.Collapsed;

            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error while starting playback: " + ex.Message);
            }
        }

        private void RecordTimeTextBlock_SelectionChanged(object sender, RoutedEventArgs e)
        {

        }
    }
}
