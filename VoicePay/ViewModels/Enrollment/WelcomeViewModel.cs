﻿using System.Threading.Tasks;
using System.Windows.Input;
using Plugin.Permissions.Abstractions;
using Xamarin.Forms;
using Plugin.Permissions;
using VoicePay.Helpers;
using System.Diagnostics;
using VoicePay.Services;
using VoicePay.Views.Enrollment;
using SpeakerRecognitionAPI.Interfaces;
using System;

namespace VoicePay.ViewModels.Enrollment
{
    public class WelcomeViewModel : BaseViewModel
    {
        private readonly ISpeakerVerification _verificationService;
        private readonly IPermissions _permissionService;

        public PermissionStatus PermissionStatus { get; private set; }
        public ICommand CheckAndGoTrainCommand { get; private set; }
        public ICommand CheckAndGoVerifyCommand { get; private set; }
        public ICommand ClearCommand { get; set; }

        private bool IsProfileCreated => Settings.Instance.Contains(nameof(Settings.UserIdentificationId)); 


        public WelcomeViewModel() : this(CrossPermissions.Current, VerificationService.Instance) { }
        public WelcomeViewModel(IPermissions permissionService, ISpeakerVerification verificationService)
        {
            _permissionService = permissionService;
            _verificationService = verificationService;

            CheckAndGoTrainCommand = new Command(async () => await CheckAndTrain());
            CheckAndGoVerifyCommand = new Command(async () => await CheckAndVerify());
            ClearCommand = new Command(Clear);
        }


        #region Command actions

        private async Task CheckAndTrain()
        {
            var savedPhrase = Settings.EnrolledPhrase;

            Page page;
            if (string.IsNullOrEmpty(savedPhrase))
            {
                page = new SelectPhrasePage();
            }
            else
            {
                EnrollmentProcess.SelectedPhrase = savedPhrase;
                page = new AudioRecordingPage();
            }
            
            await CheckPermissionsAndGoTo(page);
        }

        private async Task CheckAndVerify()
        {
            await CheckPermissionsAndGoTo(new AudioVerifyPage());
        }

        private void Clear()
        {
            Settings.Instance.Clear();
        }

        #endregion


        private async Task CheckPermissionsAndGoTo(Page page)
        {
            IsBusy = true;
            
            await RequestPermissionsIfNotGranted();
            
            if (PermissionStatus == PermissionStatus.Granted)
            {
                if (!IsProfileCreated)
                {
                    await TryCreateProfile();
                }
                
                if (IsProfileCreated)
                {
                    await GoToProcess(page);
                    IsBusy = false;
                }
                else
                {
                    IsBusy = false;
                    DisplayAlert("¡Oops!", "There was an unexpected error, try again later.", "OK");
                }
            }
            else
            {
                IsBusy = false;
                DisplayAlert("¡Oops!", "We can't continue if you don't give us access to your mic.", "OK");
            }
        }


        private async Task RequestPermissionsIfNotGranted()
        {
            PermissionStatus = await _permissionService.CheckPermissionStatusAsync(Permission.Microphone);
            if (PermissionStatus != PermissionStatus.Granted)
            {
                if (await _permissionService.ShouldShowRequestPermissionRationaleAsync(Permission.Microphone))
                {
                    DisplayAlert("Permissions", "You should authorize us to use your microphone.", "OK");
                }

                var results = await _permissionService.RequestPermissionsAsync(Permission.Microphone);
                if (results.ContainsKey(Permission.Microphone))
                    PermissionStatus = results[Permission.Microphone];
            }
        }

        private async Task TryCreateProfile()
        {
            try
            {
                var profile = await _verificationService.CreateProfileAsync();
                if (!string.IsNullOrEmpty(profile.VerificationProfileId))
                {
                    Settings.UserIdentificationId = profile.VerificationProfileId;
                }
            }
            catch
            {
                Debug.WriteLine("Error trying to create profile.");
            }
        }

        private async Task GoToProcess(Page page)
        {
            await MasterNavigateTo(page);
        }

    }
}
