<h1>
	<img src="https://raw.githubusercontent.com/antoniaelek/MFilesVAFDeploymentSetup/master/resources/images/logo.png?token=AMTjdYWVnmE0CTJ3Bp3oa83h0SFo9mnpks5bisuLwA%3D%3D" alt="logo" width="125"/> M-Files VAF Application Deployment Setup
</h1>

This extension automates M-Files VAF application deployment from Visual Studio to multiple environments (eq. dev and production). The extension will generate `Debug` and `Release` config files in Visual Studio project and modify default build behaviour of the project to use the appropriate build configuration's settings and deploy application to target server. For each configuration, deploy can be disabled by setting `deploy` property in configuration files to `false`, in which case, `mfappx` archive will be created, but not deployed to destination server.

Supported Visual studio versions: VS2015, VS2017.

## Installation
- Download `vsix` installer from the latest release on [Releases](https://github.com/antoniaelek/MFilesVAFDeploymentSetup/releases) page
- Run the installer :slightly_smiling_face: 

## Usage
- Open M-Files VAF application project in Visual Studio
- In **Solution Explorer**, select your VAF project
- Click **Tools** -> **Setup M-Files VAF Application Deployment**
- Click **OK** to confirm the location where setup files will be generated
- Click **Reload Solution** to display generated files in Solution Explorer

![Instructions-1](https://raw.githubusercontent.com/antoniaelek/MFilesVAFDeploymentSetup/master/resources/images/readme-1.gif?token=AMTjdYj42LFq5YJkiSRuO1fDGGoy16J8ks5bhA_cwA%3D%3D)

- Edit generated `App.Debug.config` and `App.Release.config` files
- When you want to only build the `mfappx` package, but not to deply it on server, set the value of `<deploy>` element to `false`

![Instructions-2](https://raw.githubusercontent.com/antoniaelek/MFilesVAFDeploymentSetup/master/resources/images/readme-2.gif?token=AMTjdShkBFKjemGAVrbQTp653EUwdjozks5biqVHwA%3D%3D)

- When deploying the package, set the value of `<deploy>` element to `true`

![Instructions-3](https://raw.githubusercontent.com/antoniaelek/MFilesVAFDeploymentSetup/master/resources/images/readme-3.gif?token=AMTjdbD7h83hZ31eGOuevFjW3gCvvIb8ks5biqYKwA%3D%3D)
