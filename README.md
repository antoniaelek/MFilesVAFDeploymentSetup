# Visual Studio Extension for M-Files VAF Application Deployment Setup

This extension will generate Debug and Release config files in Visual studio project and modify default build behaviour for M-Files VAF Application project. 

Settings from `App.Debug.config` file will be used to deploy VAF project when building with `Debug` configuration, and those from `App.Release.config` when building with `Release` configuration.

# Installation
- Download latest release from [Releases](https://github.com/antoniaelek/MFilesVAFDeploymentSetup/releases) page
- Run the installer :)

# Usage
- Open M-Files VAF application project in Visual Studio
- In **Solution Explorer**, select your VAF project
- Click **Tools** -> **Setup M-Files VAF Application Deployment**
- Click **OK** to confirm the location where setup files will be generated
- Click **Reload Solution** to display generated files in Solution Explorer

![Instructions](https://github.com/antoniaelek/MFilesVAFDeploymentSetup/tree/master/resources/images/readme-1.gif)