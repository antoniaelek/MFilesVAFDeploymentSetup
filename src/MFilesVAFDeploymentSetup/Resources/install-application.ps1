Param(
	[Parameter(Mandatory=$True)][string]$ConfigFile,
	[Parameter(Mandatory=$True)][string]$AppFilePath
)

# Application details
$appGuid = "5ccd6c17-50f9-4135-aa92-d8ac9a7333dd"

# Get config
$xml = [xml](Get-Content -Path $ConfigFile)

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
catch {}

Write-Host "Installing application..."

# Install application. The vault should not have the application installed at this point.
$vault.CustomApplicationManagementOperations.InstallCustomApplication( $AppFilePath )

Write-Host "Restarting after install..."

# Restart vault
$server.VaultManagementOperations.TakeVaultOffline( $vaultOnServer.GUID, $true )
$server.VaultManagementOperations.BringVaultOnline( $vaultOnServer.GUID )
#>