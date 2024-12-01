//Librerías fundamentales para poder realizar la aplicación
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Devices.Power;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System.Power;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;


namespace BatteryAlarmApp
{

    public sealed partial class MainPage : Page
    {
        MediaPlayer minAlarmPlayer = new MediaPlayer();
        MediaPlayer maxAlarmPlayer = new MediaPlayer();
        StorageFile MinAlarmSoundFile;
        StorageFile MaxAlarmSoundFile;


        public string MinAlarmSoundName { get; set; }
        public string MaxAlarmSoundName { get; set; }

        //Battery
        private DispatcherTimer _progressTimer;
        private double _progressValue = 0;



        public MainPage()
        {
            InitializeComponent();
            Battery.AggregateBattery.ReportUpdated += AggregateBattery_ReportUpdated;

            _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _progressTimer.Tick += ProgressTimer_Tick;
            _progressTimer.Start();

            UpdateBatteryInfo();
        }

        //Background Task

        ///Inicio Battery Progress Battery
        public void ProgressTimer_Tick(object sender, object e)
        {
            BatteryReport batteryReport = Battery.AggregateBattery.GetReport();
            double batteryPercentage = 0;

            if (batteryReport.FullChargeCapacityInMilliwattHours.HasValue && batteryReport.RemainingCapacityInMilliwattHours.HasValue)
            {
                batteryPercentage = (double)(batteryReport.RemainingCapacityInMilliwattHours.Value / (double)batteryReport.FullChargeCapacityInMilliwattHours.Value * 100);
            }

            // Actualizar el texto con el porcentaje de batería real
            PercentageTextBlock.Text = $"{batteryPercentage:F0}%";

            // Establecer el valor de _progressValue en el rango de 0 a 17
            _progressValue = batteryPercentage * 17 / 100;

            UpdateProgressValue(_progressValue);
        }

        public void UpdateProgressBar()
        {
            // Calculate the progress angle
            double progressAngle = (_progressValue / 100) * 360;

            // Update the ellipse stroke dash array
            ProgressBarEllipse.StrokeDashArray = new DoubleCollection() { progressAngle, 360 - progressAngle };

            // Update the percentage text block
            //PercentageTextBlock.Text = $"{_progressValue:F0}%";
        }

        ///Fin Battery Progress Battery
        // Update the progress value and call UpdateProgressBar()
        private void UpdateProgressValue(double value)
        {
            _progressValue = value;
            UpdateBatteryInfo();
            UpdateProgressBar();
        }

        // Evento para manejar la actualización del nivel de batería
        private async void AggregateBattery_ReportUpdated(Battery sender, object args)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                // Llama a la función para actualizar el nivel de batería
                UpdateBatteryInfo();

            });
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await LoadSettingsAsync();
            //BackgroundTask
            //var task = await BackgroundTaskHelper.RegisterTask("BatteryAlarmApp", "BackgroundTask.Task", new TimeTrigger(15, false));
            await RegisterBackgroundTask();
        }


        private async Task RegisterBackgroundTask()
        {
            string taskName = "BatteryAlarmBackgroundTask";
            string taskEntryPoint = "BackgroundTask.Task";

            // Desregistrar la tarea existente si ya está registrada
            foreach (System.Collections.Generic.KeyValuePair<Guid, IBackgroundTaskRegistration> task in BackgroundTaskRegistration.AllTasks)
            {
                if (task.Value.Name == taskName)
                {
                    task.Value.Unregister(true);
                }
            }

            // Solicitar acceso a tareas en segundo plano
            BackgroundAccessStatus access = await BackgroundExecutionManager.RequestAccessAsync();
            if (access == BackgroundAccessStatus.AlwaysAllowed || access == BackgroundAccessStatus.AllowedSubjectToSystemPolicy)
            {
                // Registrar la nueva tarea en segundo plano
                BackgroundTaskBuilder taskBuilder = new BackgroundTaskBuilder
                {
                    Name = taskName,
                    TaskEntryPoint = taskEntryPoint
                };

                taskBuilder.SetTrigger(new TimeTrigger(15, false)); // Trigger cada 15 minutos
                _ = taskBuilder.Register();
            }
        }


        private void UpdateBatteryInfo()
        {
            try
            {
                Battery battery = Battery.AggregateBattery;
                ApplicationDataContainer localSettingsMin = ApplicationData.Current.LocalSettings;
                ApplicationDataContainer localSettingsMax = ApplicationData.Current.LocalSettings;

                if (battery == null)
                {
                    // Battery not available, update UI accordingly
                    BatteryCapacityTextBlock.Text = "Capacidad de la batería: Desconocido";
                    BatteryRemainingCapacityTextBlock.Text = "Capacidad restante de la batería: Desconocido";
                    BatteryFullChargeTimeTextBlock.Text = "Tiempo para carga completa de la batería: Desconocido";
                    BatteryTimeRemainingTextBlock.Text = "Tiempo restante de la batería: Desconocido";
                    BatteryChargeTypeTextBlock.Text = "Estado de la batería: No disponible";
                    BatteryPowerSavingTextBlock.Text = "Modo de ahorro de energía: Desconocido";
                }
                else
                {
                    BatteryReport batteryReport = battery.GetReport();
                    int? chargeRate = batteryReport.ChargeRateInMilliwatts;
                    int? fullChargeCapacity = batteryReport.FullChargeCapacityInMilliwattHours;
                    int? remainingCapacity = batteryReport.RemainingCapacityInMilliwattHours;
                    BatteryStatus chargeStatus = batteryReport.Status;
                    double? remainingChargePercent = null;
                    TimeSpan? timeRemaining = null;
                    TimeSpan? fullChargeTime = null;

                    if (batteryReport.RemainingCapacityInMilliwattHours.HasValue && batteryReport.FullChargeCapacityInMilliwattHours.HasValue)
                    {
                        remainingChargePercent = (double)(batteryReport.RemainingCapacityInMilliwattHours.Value / (double)batteryReport.FullChargeCapacityInMilliwattHours.Value * 100);
                    }

                    if (remainingChargePercent.HasValue && chargeRate < 0)
                    {
                        timeRemaining = TimeSpan.FromHours((double)((double)remainingCapacity / -chargeRate));
                    }

                    /////Tiempo para cargar
                    if (chargeRate > 0)
                    {
                        // Muestra "Calculando" mientras se calcula el tiempo para la carga completa
                        fullChargeTime = TimeSpan.FromHours((double)((double)(fullChargeCapacity - remainingCapacity) / chargeRate));
                        BatteryFullChargeTimeTextBlock.Text = $"Tiempo para carga completa de la batería: {FormatTimeSpan(fullChargeTime.Value)}";

                    }
                    else
                    {
                        // El dispositivo se está descargando, mostrar "Desconocido"
                        BatteryFullChargeTimeTextBlock.Text = "Tiempo para carga completa de la batería: Desconocido";
                    }

                    // Verificar si el nivel de carga es 95%
                    BatteryFullChargeTimeTextBlock.Text = remainingChargePercent >= 95
                        ? "Tiempo para carga completa de la batería: Cargado"
                        : "Tiempo para carga completa de la batería: Desconocido";

                    if (remainingChargePercent <= 95)
                    {
                        BatteryFullChargeTimeTextBlock.Text = $"Tiempo para carga completa de la batería: {(fullChargeTime.HasValue ? FormatTimeSpan(fullChargeTime.Value) : "Desconocido")}";
                    }


                    ///Sonido
                    // Comprobar si el nivel de batería coincide con el porcentaje mínimo y reproducir la alarma si es necesario
                    bool isChargerConnected = batteryReport.Status == BatteryStatus.Charging || batteryReport.Status == BatteryStatus.Idle;

                    // Comprobar si el nivel de batería coincide con el porcentaje mínimo y reproducir la alarma si es necesario
                    // Verificar si la clave existe y no es nula en localSettingsMin
                    if (localSettingsMin.Values.ContainsKey("MinBatteryPercentage") &&
                        localSettingsMin.Values["MinBatteryPercentage"] != null)
                    {
                        // Convertir el valor a entero para compararlo
                        int minBatteryPercentage = (int)localSettingsMin.Values["MinBatteryPercentage"];

                        // Comprobar si el nivel de batería coincide con el porcentaje mínimo
                        if (remainingChargePercent == minBatteryPercentage)
                        {
                            // Verificar que el checkbox esté inicializado y marcado
                            if (MinAlarmActiveCheckbox != null && MinAlarmActiveCheckbox.IsChecked == true)
                            {
                                // Verificar si el reproductor está inicializado
                                if (MinAlarmSoundPlayer != null)
                                {
                                    // Solo reproducir la alarma si el cargador no está conectado
                                    if (!isChargerConnected)
                                    {
                                        MinAlarmSoundPlayer.Play();
                                    }
                                    else
                                    {
                                        MinAlarmSoundPlayer.Stop();
                                    }
                                }
                                else
                                {
                                    Debug.WriteLine("MinAlarmSoundPlayer no está inicializado.");
                                }
                            }
                            else
                            {
                                Debug.WriteLine("MinAlarmActiveCheckbox no está inicializado o no está marcado.");
                            }
                        }
                    }
                    else
                    {
                        Debug.WriteLine("MinBatteryPercentage no está configurado en localSettingsMin.");
                    }


                    // Comprobar si el nivel de batería coincide con el porcentaje mínimo y reproducir la alarma si es necesario
                    if (localSettingsMin.Values.ContainsKey("MinBatteryPercentage") &&
                        localSettingsMin.Values["MinBatteryPercentage"] != null)
                    {
                        if (remainingChargePercent == (int)localSettingsMin.Values["MinBatteryPercentage"])
                        {
                            if (MinAlarmActiveCheckbox != null && MinAlarmActiveCheckbox.IsChecked == true)
                            {
                                if (MinAlarmSoundPlayer != null)
                                {
                                    // Solo reproducir la alarma si el cargador no está conectado
                                    if (!isChargerConnected)
                                    {
                                        MinAlarmSoundPlayer.Play();
                                    }
                                    else
                                    {
                                        MinAlarmSoundPlayer.Stop();
                                    }
                                }
                                else
                                {
                                    Debug.WriteLine("MinAlarmSoundPlayer no está inicializado.");
                                }
                            }
                            else
                            {
                                Debug.WriteLine("MinAlarmActiveCheckbox no está inicializado o no está marcado.");
                            }
                        }
                    }
                    else
                    {
                        Debug.WriteLine("MinBatteryPercentage no está configurado en localSettingsMin.");
                    }

                    ///
                    // Verificar si la clave existe y no es nula en localSettingsMax
                    if (localSettingsMax.Values.ContainsKey("MaxBatteryPercentage") &&
                        localSettingsMax.Values["MaxBatteryPercentage"] != null)
                    {
                        // Convertir el valor a entero para compararlo
                        int maxBatteryPercentage = (int)localSettingsMax.Values["MaxBatteryPercentage"];

                        // Comprobar si el nivel de batería coincide con el porcentaje máximo
                        if (remainingChargePercent == maxBatteryPercentage)
                        {
                            // Verificar que el checkbox esté inicializado y marcado
                            if (MaxAlarmActiveCheckbox != null && MaxAlarmActiveCheckbox.IsChecked == true)
                            {
                                // Verificar si el reproductor está inicializado
                                if (MaxAlarmSoundPlayer != null)
                                {
                                    // Solo reproducir la alarma si el cargador está conectado
                                    if (isChargerConnected)
                                    {
                                        MaxAlarmSoundPlayer.Play();
                                    }
                                    else
                                    {
                                        MaxAlarmSoundPlayer.Stop();
                                    }
                                }
                                else
                                {
                                    Debug.WriteLine("MaxAlarmSoundPlayer no está inicializado.");
                                }
                            }
                            else
                            {
                                Debug.WriteLine("MaxAlarmActiveCheckbox no está inicializado o no está marcado.");
                            }
                        }
                    }
                    else
                    {
                        Debug.WriteLine("MaxBatteryPercentage no está configurado en localSettingsMax.");
                    }

                    //Max
                    // Verificar si la clave "MaxBatteryPercentage" existe y no es nula en localSettingsMax
                    if (localSettingsMax.Values.ContainsKey("MaxBatteryPercentage") &&
                        localSettingsMax.Values["MaxBatteryPercentage"] != null)
                    {
                        // Convertir el valor a entero para la comparación
                        int maxBatteryPercentage = (int)localSettingsMax.Values["MaxBatteryPercentage"];

                        // Comprobar si el nivel de batería excede o es igual al porcentaje máximo permitido
                        if (remainingChargePercent >= maxBatteryPercentage)
                        {
                            // Verificar que MaxAlarmActiveCheckbox esté inicializado y marcado
                            if (MaxAlarmActiveCheckbox != null && MaxAlarmActiveCheckbox.IsChecked == true)
                            {
                                // Verificar que el reproductor de sonido esté inicializado
                                if (MaxAlarmSoundPlayer != null)
                                {
                                    // Solo reproducir la alarma si el cargador está conectado
                                    if (isChargerConnected)
                                    {
                                        MaxAlarmSoundPlayer.Play();
                                    }
                                    else
                                    {
                                        MaxAlarmSoundPlayer.Stop();
                                    }
                                }
                                else
                                {
                                    Debug.WriteLine("MaxAlarmSoundPlayer no está inicializado.");
                                }
                            }
                            else
                            {
                                Debug.WriteLine("MaxAlarmActiveCheckbox no está inicializado o no está marcado.");
                            }
                        }
                    }
                    else
                    {
                        Debug.WriteLine("MaxBatteryPercentage no está configurado en localSettingsMax.");
                    }

                    /////Tiempo para cargar

                    BatteryChargeTypeTextBlock.Text = $"Estado de la batería: {(chargeRate > 0 ? "Cargando" : "Descargando")}";
                    BatteryCapacityTextBlock.Text = $"Capacidad de la batería: {fullChargeCapacity} mWh";
                    BatteryRemainingCapacityTextBlock.Text = $"Capacidad restante de la batería: {remainingCapacity} mWh";
                    BatteryTimeRemainingTextBlock.Text = $"Tiempo restante de la batería: {(timeRemaining.HasValue ? FormatTimeSpan(timeRemaining.Value) : "Desconocido")}";
                    BatteryPowerSavingTextBlock.Text = $"Modo de ahorro de energía: {PowerManager.EnergySaverStatus}";
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                // Handle unauthorized access exception here
                // For example:
                Debug.WriteLine($"Error de acceso denegado: {ex.Message}");
            }
        }



        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            return $"{(int)timeSpan.TotalHours} hora/s {timeSpan.Minutes} minuto/s";
        }

        //CONFIGURACIÓN
        private async void SelectMinAlarmSound_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".wav");
            picker.FileTypeFilter.Add(".mp3");
            StorageFile file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                MinAlarmSoundName = file.DisplayName;
                MinAlarmSoundTextBlock.Text = $"{MinAlarmSoundName}{file.FileType.ToLower()}";
                MinAlarmSoundFile = file;
                MinAlarmSoundPlayer.SetSource(await file.OpenAsync(FileAccessMode.Read), file.ContentType);

                // Guardar la ruta del archivo de sonido mínimo en la configuración local
                ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values["MinBatteryPercentage"] = (int)MinBatteryPercentageSlider.Value;
                localSettings.Values["MinAlarmSoundFilePath"] = file.Path;
            }
        }

        private async void SelectMaxAlarmSound_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".wav");
            picker.FileTypeFilter.Add(".mp3");
            StorageFile file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                MaxAlarmSoundName = file.DisplayName;
                MaxAlarmSoundTextBlock.Text = $"{MaxAlarmSoundName}{file.FileType.ToLower()}";
                MaxAlarmSoundFile = file;
                MaxAlarmSoundPlayer.SetSource(await file.OpenAsync(FileAccessMode.Read), file.ContentType);

                // Guardar la ruta del archivo de sonido máximo en la configuración local
                ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values["MaxBatteryPercentage"] = (int)MaxBatteryPercentageSlider.Value;
                localSettings.Values["MaxAlarmSoundFilePath"] = file.Path;
            }
        }


        private void PlayButtonMin_Click(object sender, RoutedEventArgs e)
        {
            // Reproducir la canción si no está reproduciéndose actualmente
            if (MinAlarmSoundPlayer.CurrentState != MediaElementState.Playing)
            {
                MinAlarmSoundPlayer.Play();
            }
        }

        private void PauseButtonMin_Click(object sender, RoutedEventArgs e)
        {
            // Pausar la canción si está reproduciéndose actualmente
            if (MinAlarmSoundPlayer.CurrentState == MediaElementState.Playing)
            {
                MinAlarmSoundPlayer.Pause();
            }
        }

        private void StopButtonMin_Click(object sender, RoutedEventArgs e)
        {
            // Detener la canción y volver al principio
            MinAlarmSoundPlayer.Stop();
            MinAlarmSoundPlayer.Position = TimeSpan.Zero;
        }

        private void PlayButtonMax_Click(object sender, RoutedEventArgs e)
        {
            // Reproducir la canción si no está reproduciéndose actualmente
            if (MaxAlarmSoundPlayer.CurrentState != MediaElementState.Playing)
            {
                MaxAlarmSoundPlayer.Play();
            }
        }

        private void PauseButtonMax_Click(object sender, RoutedEventArgs e)
        {
            // Pausar la canción si está reproduciéndose actualmente
            if (MaxAlarmSoundPlayer.CurrentState == MediaElementState.Playing)
            {
                MaxAlarmSoundPlayer.Pause();
            }
        }

        private void StopButtonMax_Click(object sender, RoutedEventArgs e)
        {
            // Detener la canción y volver al principio
            MaxAlarmSoundPlayer.Stop();
            MaxAlarmSoundPlayer.Position = TimeSpan.Zero;
        }

        private async void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Guardar configuración
                int minBatteryPercentage = (int)MinBatteryPercentageSlider.Value;
                int maxBatteryPercentage = (int)MaxBatteryPercentageSlider.Value;
                bool minAlarmActive = MinAlarmActiveCheckbox.IsChecked == true;
                bool maxAlarmActive = MaxAlarmActiveCheckbox.IsChecked == true;

                ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values["MinBatteryPercentage"] = minBatteryPercentage;
                localSettings.Values["MaxBatteryPercentage"] = maxBatteryPercentage;
                localSettings.Values["MinAlarmActive"] = minAlarmActive;
                localSettings.Values["MaxAlarmActive"] = maxAlarmActive;
                // Guardar las rutas de los archivos de audio si están seleccionados
                if (MinAlarmSoundFile != null)
                {
                    localSettings.Values["MinAlarmSoundFilePath"] = MinAlarmSoundFile.Path;
                }
                if (MaxAlarmSoundFile != null)
                {
                    localSettings.Values["MaxAlarmSoundFilePath"] = MaxAlarmSoundFile.Path;
                }

                if (MinAlarmSoundPlayer.CurrentState == MediaElementState.Playing)
                {
                    MinAlarmSoundPlayer.Stop();
                }
                if (MaxAlarmSoundPlayer.CurrentState == MediaElementState.Playing)
                {
                    MaxAlarmSoundPlayer.Stop();
                }

                //Mensaje
                MessageDialog dialog = new MessageDialog("Configuración guardada correctamente.");
                _ = await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                MessageDialog dialog = new MessageDialog($"Error al guardar la configuración: {ex.Message}");
                _ = await dialog.ShowAsync();
            }
        }

        private async Task LoadSettingsAsync()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            if (localSettings.Values.ContainsKey("MinBatteryPercentage"))
            {
                MinBatteryPercentageSlider.Value = (int)localSettings.Values["MinBatteryPercentage"];
            }
            if (localSettings.Values.ContainsKey("MaxBatteryPercentage"))
            {
                MaxBatteryPercentageSlider.Value = (int)localSettings.Values["MaxBatteryPercentage"];
            }
            if (localSettings.Values.ContainsKey("MinAlarmActive"))
            {
                MinAlarmActiveCheckbox.IsChecked = (bool)localSettings.Values["MinAlarmActive"];
            }
            if (localSettings.Values.ContainsKey("MaxAlarmActive"))
            {
                MaxAlarmActiveCheckbox.IsChecked = (bool)localSettings.Values["MaxAlarmActive"];
            }
            // Cargar las rutas de los archivos de audio si existen
            if (localSettings.Values.ContainsKey("MinAlarmSoundFilePath"))
            {
                string minAlarmSoundFilePath = (string)localSettings.Values["MinAlarmSoundFilePath"];
                MinAlarmSoundFile = await StorageFile.GetFileFromPathAsync(minAlarmSoundFilePath);
                MinAlarmSoundName = MinAlarmSoundFile.DisplayName;
                MinAlarmSoundTextBlock.Text = $"{MinAlarmSoundName}{MinAlarmSoundFile.FileType.ToLower()}";
                MinAlarmSoundPlayer.SetSource(await MinAlarmSoundFile.OpenAsync(FileAccessMode.Read), MinAlarmSoundFile.ContentType);
            }
            if (localSettings.Values.ContainsKey("MaxAlarmSoundFilePath"))
            {
                string maxAlarmSoundFilePath = (string)localSettings.Values["MaxAlarmSoundFilePath"];
                MaxAlarmSoundFile = await StorageFile.GetFileFromPathAsync(maxAlarmSoundFilePath);
                MaxAlarmSoundName = MaxAlarmSoundFile.DisplayName;
                MaxAlarmSoundTextBlock.Text = $"{MaxAlarmSoundName}{MaxAlarmSoundFile.FileType.ToLower()}";
                MaxAlarmSoundPlayer.SetSource(await MaxAlarmSoundFile.OpenAsync(FileAccessMode.Read), MaxAlarmSoundFile.ContentType);
            }
            //
        }
    }
}
