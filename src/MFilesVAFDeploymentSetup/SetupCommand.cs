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
            string error = null;
            var path = GetCsprojFile(out error);
            if (string.IsNullOrWhiteSpace(path))
            {
                VsShellUtilities.ShowMessageBox(
                    package,
                    error,
                    "Error loading VAF application project!",
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                return;
            }

            // Display confirmation dialog
            if (VsShellUtilities.ShowMessageBox(
                    package,
                    path,
                    "Setup will be generated for the following project:",
                    OLEMSGICON.OLEMSGICON_INFO,
                    OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_SECOND) == 2)
            {
                return;
            }

            // Try to generate setup files and update project file
            try
            {
                var root = Path.GetDirectoryName(path);
                var resources = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources");
                AddSetupFilesToProjectFolder(root, resources);
                EditProjectFile(path);
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

        /// <summary>
        /// Edits project file to include generated setup files and AfterBuild event for deployment.
        /// </summary>
        private static void EditProjectFile(string path)
        {
            var str = new StringReader(File.ReadAllText(path));
            var xmlReader = XmlReader.Create(str);
            var project = ProjectRootElement.Create(xmlReader);

            // Project Items
            AddProjectItem(project, "App.Debug.config");
            AddProjectItem(project, "App.Release.config");

            // After Build Target
            var afterBuildTarget = AddProjectTarget(project, "AfterBuild");
            afterBuildTarget.RemoveAllChildren();
            AddVAFAppInstallTasks(afterBuildTarget);

            project.Save(path);
        }

        /// <summary>
        /// Adds setup files to project folder.
        /// </summary>
        private static void AddSetupFilesToProjectFolder(string root, string resources)
        {
            // Add config files
            var config = File.ReadAllText(Path.Combine(resources, "Template.App.config"));

            File.WriteAllText(Path.Combine(root, "App.Debug.config"), config);
            File.WriteAllText(Path.Combine(root, "App.Release.config"), config);

            // Add script
            var script = File.ReadAllText(Path.Combine(resources, "install-application.ps1"));
            File.WriteAllText(Path.Combine(root, "install-application.ps1"), script);
        }

        /// <summary>
        /// Adds VAF Application Install tasks to target.
        /// </summary>
        private static void AddVAFAppInstallTasks(ProjectTargetElement target)
        {
            AddTask(target, "Delete",
                new KeyValuePair<string, string>("Files", @"$(TargetDir)$(TargetFileName).config"));

            AddTask(target, "Copy",
                new KeyValuePair<string, string>("SourceFiles", @"$(ProjectDir)\App.$(Configuration).config"),
                new KeyValuePair<string, string>("DestinationFiles", @"$(TargetDir)App.config"));

            AddTask(target, "Delete",
                new KeyValuePair<string, string>("Files", @"bin\$(Configuration)\$(ProjectName).mfappx"));

            var ciTask = AddTask(target, "CreateItem",
                new KeyValuePair<string, string>("Include", @"bin\$(Configuration)\**\*.*"));

            ciTask.AddOutputItem("Include", "ZipFiles");

            AddTask(target, "Zip",
                new KeyValuePair<string, string>("ZipFileName", @"bin\$(Configuration)\$(ProjectName).mfappx"),
                new KeyValuePair<string, string>("WorkingDirectory", @"$(TargetDir)"),
                new KeyValuePair<string, string>("Files", @"@(ZipFiles)"));

            AddTask(target, "Exec",
                new KeyValuePair<string, string>("Command", @"PowerShell -ExecutionPolicy Bypass -File install-application.ps1 -TargetDir ""$(TargetDir)"""));
        }

        /// <summary>
        /// Adds a single task to target.
        /// </summary>
        private static ProjectTaskElement AddTask(ProjectTargetElement target, string name, params KeyValuePair<string, string>[] parameters)
        {
            var task = target.AddTask(name);
            foreach (var param in parameters)
            {
                task.SetParameter(param.Key, param.Value);
            }
            return task;
        }

        /// <summary>
        /// Adds target to project.
        /// </summary>
        private static ProjectTargetElement AddProjectTarget(ProjectRootElement project, string target)
        {
            return project.Targets.FirstOrDefault(t => t.Name == target) ?? project.AddTarget(target);
        }

        /// <summary>
        /// Adds project item to project.
        /// </summary>
        private static ProjectItemElement AddProjectItem(ProjectRootElement project, string include)
        {
            return project.Items.FirstOrDefault(t => t.Include == include) ?? project.AddItem("None", include);
        }

        /// <summary>
        /// Tries to get csproj file that contains currently selected item in Solution Explorer.
        /// </summary>
        private string GetCsprojFile(out string error)
        {
            // Try to get file selected in Solution Explorer
            error = "No valid project file selected in Solution Explorer.";
            string path = null;
            try
            {
                path = GetPathOfSelectedItem();
            }
            catch (Exception)
            {
                return null;
            }

            // Csproj file selected in Solution Explorer
            if (IsSelectedItemProjectFile(path))
            {
                error = null;
                return path;
            }

            // Csproj file not selected, try to find it
            try
            {
                path = GetContainingProject(path);
            }
            catch (Exception ex)
            {
                error += ex.Message;
                path = null;
            }

            // Found csproj file
            if (IsSelectedItemProjectFile(path))
            {
                error = null;
                return path;
            }

            // Couldn't find csproj file
            return null;
        }

        /// <summary>
        /// Checks if selected item is project file (has .csproj extension).
        /// </summary>
        private bool IsSelectedItemProjectFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            return path.EndsWith(".csproj");
        }

        /// <summary>
        /// Gets project (.csproj file) that contains the specified path.
        /// </summary>
        private static string GetContainingProject(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            path = Path.GetFullPath(path);
            var dir = Path.GetDirectoryName(path);
            var files = Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly);
            try
            {
                var proj = files.SingleOrDefault(f => f.EndsWith(".csproj"));
                if (proj != null)
                {
                    return proj;
                }
            }
            catch (Exception)
            {
                throw new Exception($"More than one .csproj file exist in {dir}.");
            }
            return GetContainingProject(dir);
        }

        /// <summary>
        /// Gets the path of currently selected item in Solution Explorer.
        /// </summary>
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
            string filePath = null;
            ((IVsProject)hierarchy).GetMkDocument(itemid, out filePath);
            return filePath;
        }

    }
}
