﻿using System;

using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using EbookReader.DependencyService;
using EbookReader.Droid.DependencyService;
using Autofac;
using Xam.Plugin.WebView.Droid;
using EbookReader.Service;
using EbookReader.Model.Messages;
using Android.Content;

namespace EbookReader.Droid {
    [Activity(Label = "EbookReader", Icon = "@drawable/icon", Theme = "@style/MainTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
    public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity {

        BatteryBroadcastReceiver _batteryBroadcastReceiver;
        private bool disposed = false;
        private bool doubleBackToExitPressedOnce = false;

        protected override void OnCreate(Bundle bundle) {
            TabLayoutResource = Resource.Layout.Tabbar;
            ToolbarResource = Resource.Layout.Toolbar;

            base.OnCreate(bundle);

            this.SetUpIoc();
            this.SetUpSubscribers();

            FormsWebViewRenderer.Initialize();

            FormsWebViewRenderer.OnControlChanged += (sender, webView) => {
                webView.SetLayerType(LayerType.Software, null);
                webView.Settings.LoadWithOverviewMode = true;
                webView.Settings.UseWideViewPort = true;
            };

            global::Xamarin.Forms.Forms.Init(this, bundle);
            LoadApplication(new App());

            _batteryBroadcastReceiver = new BatteryBroadcastReceiver();
            Application.Context.RegisterReceiver(_batteryBroadcastReceiver, new IntentFilter(Intent.ActionBatteryChanged));
        }

        public override bool OnKeyDown([GeneratedEnum] Keycode keyCode, KeyEvent e) {
            if (UserSettings.Control.VolumeButtons && (keyCode == Keycode.VolumeDown || keyCode == Keycode.VolumeUp)) {
                var messageBus = IocManager.Container.Resolve<IMessageBus>();
                messageBus.Send(new GoToPageMessage { Next = keyCode == Keycode.VolumeDown, Previous = keyCode == Keycode.VolumeUp });

                return true;
            }

            return base.OnKeyDown(keyCode, e);
        }
        
        public override void OnBackPressed() {
            if (doubleBackToExitPressedOnce) {
                base.OnBackPressed();
                Java.Lang.JavaSystem.Exit(0);
                return;
            }

            this.doubleBackToExitPressedOnce = true;
            Toast.MakeText(this, "Press once again to exit!", ToastLength.Short).Show();
            IocManager.Container.Resolve<IMessageBus>().Send(new BackPressedMessage());

            new Handler().PostDelayed(() => {
                doubleBackToExitPressedOnce = false;
            }, 2000);
        }

        private void SetUpIoc() {
            IocManager.ContainerBuilder.RegisterType<AndroidAssetsManager>().As<IAssetsManager>();
            IocManager.ContainerBuilder.RegisterType<BrightnessProvider>().As<IBrightnessProvider>();
            IocManager.ContainerBuilder.RegisterInstance(new BrightnessProvider { Brightness = Android.Provider.Settings.System.GetFloat(ContentResolver, Android.Provider.Settings.System.ScreenBrightness) / 255 }).As<IBrightnessProvider>();
            IocManager.ContainerBuilder.RegisterType<CryptoService>().As<ICryptoService>();
            IocManager.ContainerBuilder.RegisterType<BatteryProvider>().As<IBatteryProvider>();
            IocManager.ContainerBuilder.RegisterType<FileHelper>().As<IFileHelper>();
            IocManager.Build();
        }

        private void SetUpSubscribers() {
            var messageBus = IocManager.Container.Resolve<IMessageBus>();
            messageBus.Subscribe<ChangesBrightnessMessage>(ChangeBrightness);
            messageBus.Subscribe<FullscreenRequestMessage>(ToggleFullscreen);
        }

        private void ChangeBrightness(ChangesBrightnessMessage msg) {
            RunOnUiThread(() => {
                var brightness = Math.Min(msg.Brightness, 1);
                brightness = Math.Max(brightness, 0);

                var attributesWindow = new WindowManagerLayoutParams();
                attributesWindow.CopyFrom(Window.Attributes);
                attributesWindow.ScreenBrightness = brightness;
                Window.Attributes = attributesWindow;
            });
        }

        private void ToggleFullscreen(FullscreenRequestMessage msg) {
            if (msg.Fullscreen) {
                RunOnUiThread(() => {
                    Window.AddFlags(WindowManagerFlags.Fullscreen);
                });
            } else {
                RunOnUiThread(() => {
                    Window.ClearFlags(WindowManagerFlags.Fullscreen);
                });
            }
        }

        protected override void Dispose(bool disposing) {

            if (!disposed) {
                if (disposing) {
                    if (_batteryBroadcastReceiver != null) {
                        Application.Context.UnregisterReceiver(_batteryBroadcastReceiver);
                    }
                }

                disposed = true;
            }

            base.Dispose(disposing);
        }
    }
}

