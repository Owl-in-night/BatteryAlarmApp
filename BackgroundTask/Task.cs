using System;
using Windows.ApplicationModel.Background;
using Windows.Devices.Power;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.UI.Notifications;

namespace BackgroundTask
{
    public sealed class Task : IBackgroundTask
    {
        private BackgroundTaskDeferral _deferral;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();
            taskInstance.Canceled += OnCanceled;

            // Lógica de la tarea en segundo plano
            var batteryReport = Battery.AggregateBattery.GetReport();
            double batteryPercentage = 0;

            if (batteryReport.FullChargeCapacityInMilliwattHours.HasValue && batteryReport.RemainingCapacityInMilliwattHours.HasValue)
            {
                batteryPercentage = (double)(batteryReport.RemainingCapacityInMilliwattHours.Value / (double)batteryReport.FullChargeCapacityInMilliwattHours.Value * 100);
            }

            // Lógica para comprobar el nivel de batería y reproducir alarmas
            var localSettings = ApplicationData.Current.LocalSettings;
            int minBatteryPercentage = (int)(localSettings.Values["MinBatteryPercentage"] ?? 20);
            int maxBatteryPercentage = (int)(localSettings.Values["MaxBatteryPercentage"] ?? 80);
            bool minAlarmActive = (bool)(localSettings.Values["MinAlarmActive"] ?? false);
            bool maxAlarmActive = (bool)(localSettings.Values["MaxAlarmActive"] ?? false);

            if (batteryPercentage <= minBatteryPercentage && minAlarmActive)
            {
                PlayAlarm(localSettings.Values["MinAlarmSoundFilePath"] as string);
            }

            if (batteryPercentage >= maxBatteryPercentage && maxAlarmActive)
            {
                PlayAlarm(localSettings.Values["MaxAlarmSoundFilePath"] as string);
            }

            _deferral.Complete();
        }

        private void OnCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            _deferral.Complete();
        }

        private void PlayAlarm(string soundFilePath)
        {
            if (!string.IsNullOrEmpty(soundFilePath))
            {
                var mediaPlayer = new MediaPlayer();
                var file = StorageFile.GetFileFromPathAsync(soundFilePath).AsTask().Result;
                mediaPlayer.Source = MediaSource.CreateFromStorageFile(file);
                mediaPlayer.Play();
            }
        }
    }
}
