//------------------------------------------------------------------------------
// <copyright file="SetupCommand.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel.Design;
using System.Globalization;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;
using System.Runtime.InteropServices;
using Microsoft.Build.Construction;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;

namespace MFilesVAFDeploymentSetup
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class SetupCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("c8e53d5f-8088-416b-a72c-c9302807eae6");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly Package package;

        /// <summary>
        /// Initializes a new instance of the <see cref="SetupCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private SetupCommand(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            this.package = package;

            OleMenuCommandService commandService = this.ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                var menuCommandID = new CommandID(CommandSet, CommandId);
                var menuItem = new MenuCommand(this.MenuItemCallback, menuCommandID);
                commandService.AddCommand(menuItem);
            }
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static SetupCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(Package package)
        {
            Instance = new SetupCommand(package);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void MenuItemCallback(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Try to get csproj file
            var csprojFilePath = GetCsprojFile();
            if (string.IsNullOrWhiteSpace(csprojFilePath))
            {
                VsShellUtilities.ShowMessageBox(
                    package,
                    "No valid project file selected in Solution Explorer.",
                    "Error loading VAF application project!",
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                return;
            }

            try
            {
                var root = Path.GetDirectoryName(csprojFilePath);
                var resources = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources");

                // Add config files
                var config = File.ReadAllText(Path.Combine(resources, "Template.App.config"));

                File.WriteAllText(Path.Combine(root, "App.Debug.config"), config);
                File.WriteAllText(Path.Combine(root, "App.Release.config"), config);

                // Add script
                var script = File.ReadAllText(Path.Combine(resources, "install-application.ps1"));
                File.WriteAllText(Path.Combine(root, "install-application.ps1"), script);

                // Edit csproj file
                var str = new StringReader(File.ReadAllText(csprojFilePath));
                var xmlReader = XmlReader.Create(str);
                var project = ProjectRootElement.Create(xmlReader);

                // Project Items
                AddProjectItem(project, "App.Debug.config");
                AddProjectItem(project, "App.Release.config");

                // After Build Target
                var afterBuildTarget = AddProjectTarget(project, "AfterBuild");
                afterBuildTarget.RemoveAllChildren();
                AddVAFAppInstallTasks(afterBuildTarget);

                project.Save(csprojFilePath);
            }
            catch (Exception ex)
            {
                VsShellUtilities.ShowMessageBox(
                   package,
                   ex.Message,
                   "Error generating setup files!",
                   OLEMSGICON.OLEMSGICON_CRITICAL,
                   OLEMSGBUTTON.OLEMSGBUTTON_OK,
                   OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                return;
            }
        }

        private static void AddVAFAppInstallTasks(ProjectTargetElement afterBuildTarget)
        {
            AddTaskToTarget(afterBuildTarget, "Delete",
                new KeyValuePair<string, string>("Files", @"$(TargetDir)$(TargetFileName).config"));

            AddTaskToTarget(afterBuildTarget, "Copy",
                new KeyValuePair<string, string>("SourceFiles", @"$(ProjectDir)\App.$(Configuration).config"),
                new KeyValuePair<string, string>("DestinationFiles", @"$(TargetDir)App.config"));

            AddTaskToTarget(afterBuildTarget, "Delete",
                new KeyValuePair<string, string>("Files", @"bin\$(Configuration)\$(ProjectName).mfappx"));

            var ciTask = AddTaskToTarget(afterBuildTarget, "CreateItem",
                new KeyValuePair<string, string>("Include", @"bin\$(Configuration)\**\*.*"));

            ciTask.AddOutputItem("Include", "ZipFiles");

            AddTaskToTarget(afterBuildTarget, "Zip",
                new KeyValuePair<string, string>("ZipFileName", @"bin\$(Configuration)\$(ProjectName).mfappx"),
                new KeyValuePair<string, string>("WorkingDirectory", @"$(TargetDir)"),
                new KeyValuePair<string, string>("Files", @"@(ZipFiles)"));

            AddTaskToTarget(afterBuildTarget, "Exec",
                new KeyValuePair<string, string>("Command", @"PowerShell -ExecutionPolicy Bypass -File install-application.ps1 -ConfigFile ""$(TargetDir)App.config"" -AppFilePath ""bin\$(Configuration)\$(ProjectName).mfappx"""));
        }

        private static ProjectTaskElement AddTaskToTarget(ProjectTargetElement target, string name, params KeyValuePair<string, string>[] parameters)
        {
            var task = target.AddTask(name);
            foreach (var param in parameters)
            {
                task.SetParameter(param.Key, param.Value);
            }
            return task;
        }

        private static ProjectTargetElement AddProjectTarget(ProjectRootElement project, string target)
        {
            return project.Targets.FirstOrDefault(t => t.Name == target) ?? project.AddTarget(target);
        }

        private static ProjectItemElement AddProjectItem(ProjectRootElement project, string include)
        {
            return project.Items.FirstOrDefault(t => t.Include == include) ?? project.AddItem("None", include);
        }

        private string GetCsprojFile()
        {
            string csprojFilePath = null;
            try
            {
                csprojFilePath = GetPathOfSelectedItem();
            }
            catch (Exception)
            {

            }

            return IsSelectedItemProjectFile(csprojFilePath) == true ? csprojFilePath : null;
        }

        private bool IsSelectedItemProjectFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            return path.EndsWith(".csproj");
        }

        private string GetPathOfSelectedItem()
        {
            IVsUIShellOpenDocument openDocument = Package.GetGlobalService(typeof(SVsUIShellOpenDocument)) as IVsUIShellOpenDocument;

            IVsMonitorSelection monitorSelection = Package.GetGlobalService(typeof(SVsShellMonitorSelection)) as IVsMonitorSelection;

            IVsMultiItemSelect multiItemSelect = null;
            IntPtr hierarchyPtr = IntPtr.Zero;
            IntPtr selectionContainerPtr = IntPtr.Zero;
            int hr = VSConstants.S_OK;
            uint itemid = VSConstants.VSITEMID_NIL;

            hr = monitorSelection.GetCurrentSelection(out hierarchyPtr, out itemid, out multiItemSelect, out selectionContainerPtr);

            IVsHierarchy hierarchy = Marshal.GetObjectForIUnknown(hierarchyPtr) as IVsHierarchy;
            IVsUIHierarchy uiHierarchy = hierarchy as IVsUIHierarchy;

            // Get the file path
            string projFilePath = null;
            ((IVsProject)hierarchy).GetMkDocument(itemid, out projFilePath);
            return projFilePath;
        }

    }
}
