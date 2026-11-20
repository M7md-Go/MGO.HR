using System;
using System.Windows.Forms;
using HR.BLL;
using HR.Helpers;
using HR.UI.UI;

namespace HR.UI
{
    /// <summary>Application entry point: schema bootstrap, central error boundary, login loop.</summary>
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

#if DEBUG
            foreach (string arg in args)
            {
                if (string.Equals(arg, "/preview", StringComparison.OrdinalIgnoreCase))
                {
                    RunPreviewHost();
                    return;
                }
            }
#endif

            try
            {
                BootstrapBLL.EnsureDatabase();
                UserBLL.InitializeFirstAdmin();
                SettingsBLL.Refresh();
            }
            catch (Exception initializationError)
            {
                Logger.LogError(initializationError, "Program.Main — bootstrap failed");
                Theme.ShowError("تعذر تهيئة قاعدة البيانات. راجع مجلد السجلات (Logs). التفاصيل: " +
                                StripDetails(initializationError));
                return;
            }

            while (true)
            {
                using (FrmLogin loginForm = new FrmLogin())
                {
                    if (loginForm.ShowDialog() != DialogResult.OK)
                    {
                        break;
                    }
                }
                bool exitRequested = false;
                using (FrmMain mainForm = new FrmMain())
                {
                    mainForm.ShowDialog();
                    exitRequested = mainForm.UserLoggedOut == false;
                }
                if (exitRequested)
                {
                    break;
                }
            }
        }

#if DEBUG
        /// <summary>DEBUG-only preview loop: DB bootstrap then the preview gallery (no login flow).</summary>
        private static void RunPreviewHost()
        {
            try
            {
                BootstrapBLL.EnsureDatabase();
                UserBLL.InitializeFirstAdmin();
                SettingsBLL.Refresh();
            }
            catch (Exception initializationError)
            {
                Logger.LogError(initializationError, "Program.RunPreviewHost — bootstrap failed");
                Theme.ShowError("تعذر تهيئة قاعدة البيانات لوضع المعاينة. راجع مجلد السجلات (Logs).");
                return;
            }
            Application.Run(new FrmPreviewHost());
        }
#endif

        private static bool _crashHandling = false;

        /// <summary>Handles UI-thread exceptions: Arabic message to the user, details to the log (BR-23).</summary>
        private static void OnThreadException(object sender, System.Threading.ThreadExceptionEventArgs args)
        {
            ShowCrashDialog(args.Exception);
        }

        /// <summary>Handles non-UI thread exceptions (ZKTeco polling, background calculations).</summary>
        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            Exception fatal = args.ExceptionObject as Exception;
            if (fatal != null)
            {
                ShowCrashDialog(fatal);
            }
        }

        private static void ShowCrashDialog(Exception crash)
        {
            if (_crashHandling)
            {
                return;
            }
            _crashHandling = true;
            try
            {
                Logger.LogError(crash, "Central error boundary");
                Theme.ShowError("حدث خطأ غير متوقع. تم حفظ تفاصيل الخطأ في ملف السجلات.\n" + StripDetails(crash));
            }
            finally
            {
                _crashHandling = false;
            }
        }

        private static string StripDetails(Exception crash)
        {
            BusinessException intended = crash as BusinessException;
            if (intended != null)
            {
                return intended.Message;
            }
            return string.Empty;
        }
    }
}
