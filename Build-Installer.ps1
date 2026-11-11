param(
    [string]$Payload = (Join-Path $PSScriptRoot 'publish'),
    [string]$Output = (Join-Path $PSScriptRoot 'EriMixer-0.3.0-x64.msi')
)
$ErrorActionPreference = 'Stop'
$Payload = (Resolve-Path -LiteralPath $Payload).Path
$Output = [IO.Path]::GetFullPath($Output)
if (Test-Path -LiteralPath $Output) { throw "Output already exists: $Output. Choose a new output filename." }
$buildDirectory = Join-Path ([IO.Path]::GetDirectoryName($Output)) ('msi-build-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $buildDirectory | Out-Null

function Invoke-Com($target, [string]$method, [object[]]$arguments = @()) {
    $target.GetType().InvokeMember($method, [Reflection.BindingFlags]::InvokeMethod, $null, $target, $arguments)
}
function Set-Com($target, [string]$property, [object[]]$arguments) {
    $target.GetType().InvokeMember($property, [Reflection.BindingFlags]::SetProperty, $null, $target, $arguments)
}
$installer = New-Object -ComObject WindowsInstaller.Installer
$database = Invoke-Com $installer 'OpenDatabase' @($Output, 3)
$tableColumns = @{}
function Sql([string]$statement) {
    try { $view = Invoke-Com $database 'OpenView' @($statement) }
    catch { throw "Invalid MSI query: $statement`n$_" }
    try { Invoke-Com $view 'Execute' } finally { Invoke-Com $view 'Close' }
}
function Row([string]$table, [object[]]$values) {
    $encoded = foreach ($v in $values) {
        if ($null -eq $v) { 'NULL' }
        elseif ($v -is [int] -or $v -is [long]) { [string]$v }
        else { "'" + ([string]$v).Replace("'", "''") + "'" }
    }
    Sql ('INSERT INTO `' + $table + '` (' + ($tableColumns[$table] -join ',') + ') VALUES (' + ($encoded -join ',') + ')')
}
function Add-Stream([string]$table, [string]$name, [string]$file) {
    $record = Invoke-Com $installer 'CreateRecord' @(2)
    Set-Com $record 'StringData' @(1, $name)
    Invoke-Com $record 'SetStream' @(2, $file)
    $columns = if ($table -eq '_Streams') { '`Name`,`Data`' } else { $tableColumns[$table] -join ',' }
    $view = Invoke-Com $database 'OpenView' @('INSERT INTO `' + $table + '` (' + $columns + ') VALUES (?, ?)')
    try { Invoke-Com $view 'Execute' @($record) } finally { Invoke-Com $view 'Close' }
}

$schemas = @(
    'CREATE TABLE `Property` (`Property` CHAR(72) NOT NULL, `Value` CHAR(0) LOCALIZABLE PRIMARY KEY `Property`)',
    'CREATE TABLE `Directory` (`Directory` CHAR(72) NOT NULL, `Directory_Parent` CHAR(72), `DefaultDir` CHAR(255) NOT NULL LOCALIZABLE PRIMARY KEY `Directory`)',
    'CREATE TABLE `Component` (`Component` CHAR(72) NOT NULL, `ComponentId` CHAR(38), `Directory_` CHAR(72) NOT NULL, `Attributes` SHORT NOT NULL, `Condition` CHAR(255), `KeyPath` CHAR(72) PRIMARY KEY `Component`)',
    'CREATE TABLE `Feature` (`Feature` CHAR(38) NOT NULL, `Feature_Parent` CHAR(38), `Title` CHAR(64) LOCALIZABLE, `Description` CHAR(255) LOCALIZABLE, `Display` SHORT, `Level` SHORT NOT NULL, `Directory_` CHAR(72), `Attributes` SHORT NOT NULL PRIMARY KEY `Feature`)',
    'CREATE TABLE `FeatureComponents` (`Feature_` CHAR(38) NOT NULL, `Component_` CHAR(72) NOT NULL PRIMARY KEY `Feature_`, `Component_`)',
    'CREATE TABLE `File` (`File` CHAR(72) NOT NULL, `Component_` CHAR(72) NOT NULL, `FileName` CHAR(255) NOT NULL LOCALIZABLE, `FileSize` LONG NOT NULL, `Version` CHAR(72), `Language` CHAR(20), `Attributes` SHORT, `Sequence` LONG NOT NULL PRIMARY KEY `File`)',
    'CREATE TABLE `Media` (`DiskId` SHORT NOT NULL, `LastSequence` LONG NOT NULL, `DiskPrompt` CHAR(64) LOCALIZABLE, `Cabinet` CHAR(255), `VolumeLabel` CHAR(32), `Source` CHAR(72) PRIMARY KEY `DiskId`)',
    'CREATE TABLE `Registry` (`Registry` CHAR(72) NOT NULL, `Root` SHORT NOT NULL, `Key` CHAR(255) NOT NULL LOCALIZABLE, `Name` CHAR(255) LOCALIZABLE, `Value` CHAR(0) LOCALIZABLE, `Component_` CHAR(72) NOT NULL PRIMARY KEY `Registry`)',
    'CREATE TABLE `Shortcut` (`Shortcut` CHAR(72) NOT NULL, `Directory_` CHAR(72) NOT NULL, `Name` CHAR(128) NOT NULL LOCALIZABLE, `Component_` CHAR(72) NOT NULL, `Target` CHAR(255), `Arguments` CHAR(255), `Description` CHAR(255) LOCALIZABLE, `Hotkey` SHORT, `Icon_` CHAR(72), `IconIndex` SHORT, `ShowCmd` SHORT, `WkDir` CHAR(72) PRIMARY KEY `Shortcut`)',
    'CREATE TABLE `Icon` (`Name` CHAR(72) NOT NULL, `Data` OBJECT NOT NULL PRIMARY KEY `Name`)',
    'CREATE TABLE `RemoveFile` (`FileKey` CHAR(72) NOT NULL, `Component_` CHAR(72) NOT NULL, `FileName` CHAR(255) LOCALIZABLE, `DirProperty` CHAR(72) NOT NULL, `InstallMode` SHORT NOT NULL PRIMARY KEY `FileKey`)',
    'CREATE TABLE `InstallExecuteSequence` (`Action` CHAR(72) NOT NULL, `Condition` CHAR(255), `Sequence` SHORT PRIMARY KEY `Action`)',
    'CREATE TABLE `InstallUISequence` (`Action` CHAR(72) NOT NULL, `Condition` CHAR(255), `Sequence` SHORT PRIMARY KEY `Action`)',
    'CREATE TABLE `LaunchCondition` (`Condition` CHAR(255) NOT NULL, `Description` CHAR(255) NOT NULL LOCALIZABLE PRIMARY KEY `Condition`)',
    'CREATE TABLE `Upgrade` (`UpgradeCode` CHAR(38) NOT NULL, `VersionMin` CHAR(20), `VersionMax` CHAR(20), `Language` CHAR(255), `Attributes` SHORT NOT NULL, `Remove` CHAR(255), `ActionProperty` CHAR(72) NOT NULL PRIMARY KEY `UpgradeCode`,`VersionMin`,`VersionMax`,`Language`,`Attributes`)'
)
foreach ($schema in $schemas) {
    $tableName = [regex]::Match($schema, 'CREATE TABLE `([^`]+)`').Groups[1].Value
    $tableColumns[$tableName] = @([regex]::Matches($schema, '`([^`]+)` (?:CHAR|SHORT|LONG|OBJECT)') | ForEach-Object { '`' + $_.Groups[1].Value + '`' })
    Sql $schema
}
$properties = [ordered]@{
    ProductCode = '{B86D4148-CFD1-4B44-9B9B-1C511A344CA6}'
    UpgradeCode = '{A7A6A038-F5C0-4F04-B477-774D5C72C76D}'
    ProductName = 'EriMixer'
    ProductVersion = '0.3.0'
    Manufacturer = 'EriVyra'
    ProductLanguage = '1033'
    INSTALLLEVEL = '1'
    ARPNOMODIFY = '1'
    ARPPRODUCTICON = 'EriMixerIcon'
    ARPCOMMENTS = 'EriMixer 0.3.0 freeware. Requires .NET 8 Desktop Runtime (x64). Optional sign-in startup.'
}
foreach ($entry in $properties.GetEnumerator()) { Row 'Property' @($entry.Key, $entry.Value) }
Row 'Directory' @('TARGETDIR', $null, 'SourceDir')
Row 'Directory' @('LocalAppDataFolder', 'TARGETDIR', '.')
Row 'Directory' @('ProgramsFolder', 'LocalAppDataFolder', 'Programs')
Row 'Directory' @('INSTALLFOLDER', 'ProgramsFolder', 'EriMixer')
Row 'Directory' @('ProgramMenuFolder', 'TARGETDIR', '.')
Row 'Directory' @('MenuFolder', 'ProgramMenuFolder', 'EriMixer')
Row 'Feature' @('Main', $null, 'EriMixer', 'Mixer, Start menu shortcut, and Windows startup', 1, 1, 'INSTALLFOLDER', 0)
Row 'LaunchCondition' @('VersionNT64', 'EriMixer requires 64-bit Windows and the .NET 8 Desktop Runtime (x64).')
Row 'Upgrade' @($properties.UpgradeCode, $null, '0.3.0', $null, 1, 'ALL', 'PREVIOUSVERSIONS')
Row 'Property' @('PREVIOUSVERSIONS', '')

$files = @(Get-ChildItem -LiteralPath $Payload -File | Where-Object { $_.Extension -ne '.pdb' } | Sort-Object Name)
if ($files.Name -notcontains 'EriMixer.exe') { throw 'Published EriMixer.exe is missing.' }
$cabinetPath = Join-Path $buildDirectory 'payload.cab'
$ddf = [Collections.Generic.List[string]]::new()
$ddf.Add('.OPTION EXPLICIT')
$ddf.Add('.Set CabinetNameTemplate=payload.cab')
$ddf.Add('.Set DiskDirectoryTemplate="' + $buildDirectory + '"')
$ddf.Add('.Set CompressionType=MSZIP')
$ddf.Add('.Set Cabinet=ON')
$ddf.Add('.Set Compress=ON')
$ddf.Add('.Set MaxDiskSize=0')
$manifest = @()
$number = 0
foreach ($file in $files) {
    $number++
    $id = 'F' + $number.ToString('0000')
    $component = 'C' + $number.ToString('0000')
    Row 'Component' @($component, ('{' + [guid]::NewGuid().ToString().ToUpperInvariant() + '}'), 'INSTALLFOLDER', 256, $null, $id)
    Row 'FeatureComponents' @('Main', $component)
    $fileVersion = $file.VersionInfo.FileVersion
    if ($fileVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') { $fileVersion = $null }
    Row 'File' @($id, $component, ($id + '|' + $file.Name), [int]$file.Length, $fileVersion, $null, 512, $number)
    $ddf.Add('"' + $file.FullName + '" ' + $id)
    $manifest += [pscustomobject]@{ FileId = $id; Name = $file.Name; SHA256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash }
    if ($file.Name -eq 'EriMixer.exe') { $exeComponent = $component; $exeId = $id }
}
$ddfPath = Join-Path $buildDirectory 'payload.ddf'
$ddf | Set-Content -LiteralPath $ddfPath -Encoding ascii
Push-Location $buildDirectory
try { & "$env:WINDIR\System32\makecab.exe" /F $ddfPath | Out-Null; if ($LASTEXITCODE -ne 0) { throw 'Cabinet creation failed.' } }
finally { Pop-Location }
Row 'Media' @(1, $files.Count, $null, '#payload.cab', $null, $null)
Add-Stream '_Streams' 'payload.cab' $cabinetPath
Add-Stream 'Icon' 'EriMixerIcon' (Join-Path $PSScriptRoot 'Assets\EriMixer.ico')

Row 'Component' @('Startup', '{2D09C47B-B579-4162-9B24-D3DD174A1A77}', 'INSTALLFOLDER', 260, $null, 'StartupRun')
Row 'FeatureComponents' @('Main', 'Startup')
Row 'Registry' @('StartupRun', 1, 'Software\Microsoft\Windows\CurrentVersion\Run', 'EriMixer', ('"[#' + $exeId + ']" --startup'), 'Startup')
Row 'Shortcut' @('StartMenu', 'MenuFolder', 'EriMixer', $exeComponent, ('[#' + $exeId + ']'), $null, 'EriMixer volume mixer', $null, 'EriMixerIcon', 0, 1, 'INSTALLFOLDER')
Row 'RemoveFile' @('RemoveMenuFolder', $exeComponent, $null, 'MenuFolder', 2)
Row 'RemoveFile' @('RemoveInstallFolder', $exeComponent, $null, 'INSTALLFOLDER', 2)

$actions = [ordered]@{
    LaunchConditions = 100; CostInitialize = 800; FileCost = 900; CostFinalize = 1000
    FindRelatedProducts = 25; InstallValidate = 1400; InstallInitialize = 1500
    RemoveExistingProducts = 1550; ProcessComponents = 1600
    MigrateFeatureStates = 1200
    UnpublishFeatures = 1800; RemoveRegistryValues = 2600; RemoveShortcuts = 3200
    RemoveFiles = 3500; RemoveFolders = 3600; CreateFolders = 3700; InstallFiles = 4000
    CreateShortcuts = 4500; WriteRegistryValues = 5000; RegisterUser = 6000
    RegisterProduct = 6100; PublishFeatures = 6300; PublishProduct = 6400; InstallFinalize = 6600
}
foreach ($entry in $actions.GetEnumerator()) { Row 'InstallExecuteSequence' @($entry.Key, $null, [int]$entry.Value) }
foreach ($action in @('FindRelatedProducts', 'LaunchConditions', 'CostInitialize', 'FileCost', 'CostFinalize')) { Row 'InstallUISequence' @($action, $null, [int]$actions[$action]) }
Row 'InstallUISequence' @('ExecuteAction', $null, 1300)
Invoke-Com $database 'Commit'
$summary = $database.GetType().InvokeMember('SummaryInformation', [Reflection.BindingFlags]::GetProperty, $null, $database, @(20))
foreach ($pair in @(@(1,1252), @(2,'Installation Database'), @(3,'EriMixer 0.3.0'), @(4,'EriVyra'), @(7,'x64;1033'), @(9,('{' + [guid]::NewGuid().ToString().ToUpperInvariant() + '}')), @(14,200), @(15,10), @(18,'EriMixer package builder'))) {
    Set-Com $summary 'Property' @([int]$pair[0], $pair[1])
}
Invoke-Com $summary 'Persist'
Invoke-Com $database 'Commit'

$manifestPath = "$Output.manifest.json"
$manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding utf8
Write-Output "Created $Output"
Write-Output "Manifest: $manifestPath"
Write-Output "Validation inputs: $buildDirectory"
