using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace ProjectDesk
{
    [Guid("1B57AF6E-A6FC-4DFC-9A69-34C1E95CDB79")]
    public sealed class ProjectDeskWindow : ToolWindowPane
    {
        public ProjectDeskWindow() : base(null)
        {
            Caption = "Project Desk";
            Content = new ProjectDeskControl();
        }

        internal void Initialize(ProjectDeskPackage package)
        {
            ((ProjectDeskControl)Content).SetPackage(package);
        }
    }
}
