Param(
	[Parameter(Mandatory=$True)][string]$TargetDir
)

$TargetDir =$TargetDir.TrimEnd("""")
Write-Host ""
Write-Host $TargetDir

# Get application details
try{
    $appDefPath = Join-Path -Path $TargetDir -ChildPath "appdef.xml"
    $appDef = [xml](Get-Content $appDefPath)
    $appGuid = $appDef.application.guid
	$mfappx = $appDef.application.'extension-objects'.'extension-object'.assembly.TrimEnd(".dll") + ".mfappx"
	$AppFilePath = Join-Path -Path $TargetDir -ChildPath $mfappx
}
catch
{
}

if (-not $appGuid)
{
	Write-Host "Unable to read App GUID from appdef.xml"
	exit
}

if (-not $AppFilePath)
{
	Write-Host "Unable to read Assembly name from appdef.xml"
	exit
}

# Get config
$ConfigFile =  Join-Path -Path $TargetDir -ChildPath "App.config"
$xml = [xml](Get-Content -Path $ConfigFile)

# Deploy enabled
[bool]$deploy = [System.Convert]::ToBoolean($xml.configuration.deploy)

# Target vault
$vaultName = $xml.configuration.vaultName

# Connection details 
$authType = $xml.configuration.authType
$userName = $xml.configuration.userName
$password = $xml.configuration.password
$domain = $xml.configuration.domain
$spn = $xml.configuration.spn
$protocolSequence = $xml.configuration.protocolSequence
$networkAddress = $xml.configuration.networkAddress
$endpoint = [int]$xml.configuration.endpoint
$encryptedConnection = [bool]$xml.configuration.encryptedConnection
$localComputerName = $xml.configuration.localComputerName

Write-Host "Using the following configuration from config file:" $ConfigFile
Write-Host "  deploy:" $deploy
Write-Host "  vaultName:" $vaultName
Write-Host "  authType:" $authType
Write-Host "  userName:" $userName
Write-Host "  password:" $password
Write-Host "  domain:" $domain
Write-Host "  spn:" $spn
Write-Host "  protocolSequence:" $protocolSequence
Write-Host "  networkAddress:" $networkAddress
Write-Host "  endpoint:" $endpoint
Write-Host "  encryptedConnection:" $encryptedConnection
Write-Host "  localComputerName:" $localComputerName

# If disabled, exit
if (-not $deploy) {
	exit
}

# If any of the andatory properties is not set, exit
if ((-not $vaultName) -or (-not $authType) -or (-not $networkAddress) -or (-not $endpoint) -or (-not $protocolSequence))
{
	$missingProperties = ""
	if(-not $vaultName) { $missingProperties = $missingProperties + "vaultName, " }
	if(-not $authType) { $missingProperties = $missingProperties + "authType, " }
	if(-not $networkAddress) { $missingProperties = $missingProperties + "networkAddress, " }
	if(-not $endpoint) { $missingProperties = $missingProperties + "endpoint, " }
	if(-not $protocolSequence) { $missingProperties = $missingProperties + "protocolSequence, " }
    $missingProperties = $missingProperties.TrimEnd(", ")
	Write-Host "Mandatory properties not set:" $missingProperties
	exit
}

Write-Host "Connecting to Vault" $vaultName "on server" $networkAddress 

# Load M-Files API
$null = [System.Reflection.Assembly]::LoadWithPartialName("Interop.MFilesAPI")

# Connect to M-Files Server
$server = new-object MFilesAPI.MFilesServerApplicationClass
$tzi = new-object MFilesAPI.TimeZoneInformationClass
$tzi.LoadWithCurrentTimeZone()
$null = $server.ConnectAdministrativeEx( $tzi, $authType, $userName, $password, $domain, $spn, $protocolSequence, $networkAddress, $endpoint, $encryptedConnection, $localComputerName )

# Get the target vault
$vaultOnServer = $server.GetOnlineVaults().GetVaultByName( $vaultName )

# Login to vault
$vault = $vaultOnServer.LogIn()

# Try to uninstall existing application
try
{
	Write-Host "Checking for previous installation of the application..."

	# Uninstall
	$vault.CustomApplicationManagementOperations.UninstallCustomApplication( $appGuid );
	
	Write-Host "Restarting after uninstall..."
	
	# Restart vault. The installation seems to fail, if the vault is not restarted after uninstall.
	$server.VaultManagementOperations.TakeVaultOffline( $vaultOnServer.GUID, $true )
	$server.VaultManagementOperations.BringVaultOnline( $vaultOnServer.GUID )
		
	# Login to vault again.
	$vault = $vaultOnServer.LogIn()
}
catch
{
    Write-Host $PSItem.ToString()
}

Write-Host "Installing application..."

# Install application. The vault should not have the application installed at this point.
$vault.CustomApplicationManagementOperations.InstallCustomApplication( $AppFilePath )

Write-Host "Restarting after install..."

# Restart vault
$server.VaultManagementOperations.TakeVaultOffline( $vaultOnServer.GUID, $true )
$server.VaultManagementOperations.BringVaultOnline( $vaultOnServer.GUID )
#>