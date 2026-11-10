using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace ProjectDesk
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("Project Desk", "Visual project and file manager", "1.0")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(ProjectDeskWindow))]
    [Guid(PackageGuidString)]
    public sealed class ProjectDeskPackage : AsyncPackage
    {
        public const string PackageGuidString = "71BFB3E7-57D5-4A40-A1E8-4AF99A4E738A";
        private static readonly Guid CommandSetGuid = new Guid("9F635C60-631C-47D9-9D8F-E441E1308C12");
        private const int OpenProjectDeskCommandId = 0x0100;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var commandService = await GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService == null)
            {
                throw new InvalidOperationException("Visual Studio's menu command service is unavailable.");
            }

            var commandId = new CommandID(CommandSetGuid, OpenProjectDeskCommandId);
            commandService.AddCommand(new MenuCommand(ShowProjectDesk, commandId));
        }

        internal async Task<DTE> GetDteAsync()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();
            var dte = await GetServiceAsync(typeof(SDTE)) as DTE;
            if (dte == null)
            {
                throw new InvalidOperationException("Visual Studio's automation service is unavailable.");
            }

            return dte;
        }

        private void ShowProjectDesk(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var window = FindToolWindow(typeof(ProjectDeskWindow), 0, true) as ProjectDeskWindow;
            if (window?.Frame == null)
            {
                throw new InvalidOperationException("The Project Desk window could not be created.");
            }

            window.Initialize(this);
            ErrorHandler.ThrowOnFailure(((IVsWindowFrame)window.Frame).Show());
        }
    }
}
