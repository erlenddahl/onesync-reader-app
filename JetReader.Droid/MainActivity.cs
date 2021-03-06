﻿using System;
using System.IO;
using System.Net;
using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using JetReader.DependencyService;
using JetReader.Droid.DependencyService;
using Autofac;
using JetReader.Service;
using JetReader.Model.Messages;
using Android.Content;
using Android.Provider;
using Android.Webkit;
using JetReader.Page;
using Plugin.FilePicker;
using Plugin.FilePicker.Abstractions;
using Plugin.HybridWebView.Droid;
using Plugin.Permissions;

namespace JetReader.Droid {
    [Activity(Label = "JetReader", Icon = "@mipmap/ic_launcher", Theme = "@style/MainTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
    [IntentFilter(new[]{Intent.ActionView}, Categories = new []{Intent.CategoryBrowsable, Intent.CategoryDefault}, DataMimeType = "application/epub+zip")]
    [IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryDefault }, DataScheme = "file", DataHost = "*", DataMimeType = "*/*", DataPathPattern = ".*\\.epub")]
    [IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryBrowsable, Intent.CategoryDefault }, DataScheme = "http", DataHost = "*", DataMimeType = "*/*", DataPathPattern = ".*\\.epub")]
    public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity {

        BatteryBroadcastReceiver _batteryBroadcastReceiver;
        private bool _disposed = false;

        protected override void OnCreate(Bundle bundle) {

            if (!string.IsNullOrWhiteSpace(Intent.DataString))
            {
                var uri = Android.Net.Uri.Parse(Intent.DataString);
                var filePath = IOUtil.GetPath(ApplicationContext, uri);
                if (string.IsNullOrEmpty(filePath))
                {
                    filePath = IOUtil.IsMediaStore(Intent.Scheme) ? uri.ToString() : uri.Path;
                }

                var fileName = GetFileName(ApplicationContext, uri);

                var fs = IocManager.Container.Resolve<FileService>();
                UserSettings.OpenBookImmediately = new FileData(filePath, fileName, () => fs.LoadFileStreamAsync(filePath).Result);
            }

            TabLayoutResource = Resource.Layout.Tabbar;
            ToolbarResource = Resource.Layout.Toolbar;

            base.OnCreate(bundle);
            Xamarin.Essentials.Platform.Init(this, bundle);

            SetUpIoc();

            HybridWebViewRenderer.Initialize();

            HybridWebViewRenderer.OnControlChanged += (sender, webView) => {
                webView.SetLayerType(LayerType.Software, null);
                webView.Settings.LoadWithOverviewMode = true;
                webView.Settings.UseWideViewPort = true;
            };

            Plugin.CurrentActivity.CrossCurrentActivity.Current.Init(this, bundle);

            // Rg.popup initialization, see: https://github.com/rotorgames/Rg.Plugins.Popup/wiki/Getting-started
            Rg.Plugins.Popup.Popup.Init(this, bundle);

            global::Xamarin.Forms.Forms.SetFlags("FastRenderers_Experimental");
            global::Xamarin.Forms.Forms.Init(this, bundle);

            //TODO: Make sure to compile for all Android versions: https://forums.xamarin.com/discussion/382/suggestions-on-how-to-support-multiple-api-levels-from-a-single-application-apk
#if __ANDROID_28__
            //Window.Attributes.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;
#endif

            LoadApplication(new App());

            Window.SetSoftInputMode(SoftInput.AdjustResize);

            _batteryBroadcastReceiver = new BatteryBroadcastReceiver();
            Application.Context.RegisterReceiver(_batteryBroadcastReceiver, new IntentFilter(Intent.ActionBatteryChanged));
        }

        /// <summary>
        /// Retrieves file name part from given Uri
        /// </summary>
        /// <param name="context">Android context to access content resolver</param>
        /// <param name="uri">Uri to get filename for</param>
        /// <returns>file name part</returns>
        private string GetFileName(Context context, Android.Net.Uri uri)
        {
            string[] projection = { MediaStore.MediaColumns.DisplayName };

            var resolver = context.ContentResolver;
            var name = string.Empty;
            var metaCursor = resolver.Query(uri, projection, null, null, null);

            if (metaCursor != null)
            {
                try
                {
                    if (metaCursor.MoveToFirst())
                    {
                        name = metaCursor.GetString(0);
                    }
                }
                finally
                {
                    metaCursor.Close();
                }
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                if (!Path.HasExtension(name))
                    name = name.TrimEnd('.') + '.' + GetExtensionFromUri(context, uri);

                return name;
            }
            else
            {
                var extension = GetExtensionFromUri(context, uri);
                if (!string.IsNullOrEmpty(extension))
                    return "filename." + extension;
                else
                    return Path.GetFileName(WebUtility.UrlDecode(uri.ToString()));
            }
        }

        /// <summary>
        /// Returns a file extension for given content Uri
        /// </summary>
        /// <param name="context">context to use</param>
        /// <param name="uri">content Uri to check</param>
        /// <returns>file extension, without leading dot, or empty string</returns>
        public static string GetExtensionFromUri(Context context, Android.Net.Uri uri)
        {
            string mimeType = context.ContentResolver.GetType(uri);
            return mimeType != null ? MimeTypeMap.Singleton.GetExtensionFromMimeType(mimeType) : string.Empty;
        }

        protected override void OnStart() {
            base.OnStart();

            SetUpSubscribers();
        }

        protected override void OnStop() {
            base.OnStop();

            IocManager.Container.Resolve<IMessageBus>().UnSubscribe("MainActivity");
        }

        public override bool OnKeyDown([GeneratedEnum] Keycode keyCode, KeyEvent e) {
            if (UserSettings.Control.VolumeButtons && (keyCode == Keycode.VolumeDown || keyCode == Keycode.VolumeUp) && App.IsCurrentPageType(typeof(ReaderPage))) {
                var messageBus = IocManager.Container.Resolve<IMessageBus>();
                messageBus.Send(new GoToPageMessage { Next = keyCode == Keycode.VolumeDown, Previous = keyCode == Keycode.VolumeUp });

                return true;
            }

            return base.OnKeyDown(keyCode, e);
        }

        public override void OnBackPressed() {
            if (Rg.Plugins.Popup.Popup.SendBackPressed(base.OnBackPressed))
            {
                // There were open popups. They should now be closed.
                return;
            }

            IocManager.Container.Resolve<IMessageBus>().Send(new BackPressedMessage());
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults) {
            PermissionsImplementation.Current.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        private void SetUpIoc() {
            IocManager.ContainerBuilder.RegisterType<AndroidFileService>().As<FileService>();
            IocManager.ContainerBuilder.RegisterType<AndroidAssetsManager>().As<IAssetsManager>();
            IocManager.ContainerBuilder.RegisterType<BrightnessProvider>().As<IBrightnessProvider>();
            IocManager.ContainerBuilder.RegisterInstance(new BrightnessProvider { Brightness = Android.Provider.Settings.System.GetFloat(ContentResolver, Android.Provider.Settings.System.ScreenBrightness) / 255 }).As<IBrightnessProvider>();
            IocManager.ContainerBuilder.RegisterType<BatteryProvider>().As<IBatteryProvider>();
            IocManager.ContainerBuilder.RegisterType<FileHelper>().As<IFileHelper>();
            IocManager.ContainerBuilder.RegisterType<ToastService>().As<IToastService>();
            IocManager.ContainerBuilder.RegisterType<VersionProvider>().As<IVersionProvider>();
            IocManager.Build();
        }

        private void SetUpSubscribers() {
            var messageBus = IocManager.Container.Resolve<IMessageBus>();
            messageBus.Subscribe<ChangeBrightnessMessage>(ChangeBrightness, "MainActivity");
            messageBus.Subscribe<FullscreenRequestMessage>(ToggleFullscreen, "MainActivity");
            messageBus.Subscribe<CloseAppMessage>(CloseAppMessageSubscriber, "MainActivity");
        }

        private void CloseAppMessageSubscriber(CloseAppMessage msg) {
            Finish();
        }

        private void ChangeBrightness(ChangeBrightnessMessage msg) {
            RunOnUiThread(() => {
                var attributesWindow = new WindowManagerLayoutParams();
                attributesWindow.CopyFrom(Window.Attributes);
                attributesWindow.ScreenBrightness = (float)ChangeBrightnessMessage.Validate(msg.Brightness);
                Window.Attributes = attributesWindow;
            });
        }

        private bool isFullscreen = false;
        private void ToggleFullscreen(FullscreenRequestMessage msg)
        {
            if (!UserSettings.Reader.Fullscreen) return;

            //TODO: See https://stackoverflow.com/questions/7692789/toggle-fullscreen-mode for more robust function (for more Android versions)
            RunOnUiThread(() =>
            {
                isFullscreen = msg.Fullscreen ?? !isFullscreen;

                if (isFullscreen)
                {
                    Window.DecorView.SystemUiVisibility = (StatusBarVisibility)(
                        SystemUiFlags.Fullscreen
                        | SystemUiFlags.HideNavigation
                        | SystemUiFlags.Immersive
                        | SystemUiFlags.ImmersiveSticky
                        | SystemUiFlags.LowProfile
                        | SystemUiFlags.LayoutStable
                        | SystemUiFlags.LayoutHideNavigation
                        | SystemUiFlags.LayoutFullscreen
                    );
                }
                else
                {
                    Window.DecorView.SystemUiVisibility = (StatusBarVisibility)(
                        SystemUiFlags.LayoutStable
                    );
                }
            });
        }

        protected override void Dispose(bool disposing) {

            if (!_disposed) {
                if (disposing) {
                    if (_batteryBroadcastReceiver != null) {
                        Application.Context.UnregisterReceiver(_batteryBroadcastReceiver);
                    }
                }

                _disposed = true;
            }

            base.Dispose(disposing);
        }
    }
}

