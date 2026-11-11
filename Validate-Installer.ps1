param([string]$Package, [string]$Manifest, [string]$Destination)
$ErrorActionPreference = 'Stop'
function Call($target, [string]$name, [object[]]$arguments = @()) {
    $target.GetType().InvokeMember($name, [Reflection.BindingFlags]::InvokeMethod, $null, $target, $arguments)
}
function Get($target, [string]$name, [object[]]$arguments = @()) {
    $target.GetType().InvokeMember($name, [Reflection.BindingFlags]::GetProperty, $null, $target, $arguments)
}
function ReadRows($db, [string]$query) {
    try { $view = Call $db OpenView @($query) }
    catch { throw "MSI SQL query failed: $query`n$_" }
    $null = Call $view Execute
    $result = @()
    try {
        while ($record = Call $view Fetch) {
            $values = @()
            for ($index=1; $index -le (Get $record FieldCount); $index++) { $values += Get $record StringData @($index) }
            $result += ,$values
        }
    }
    finally { $null = Call $view Close }
    return ,$result
}
$Package = (Resolve-Path -LiteralPath $Package).Path
$msi = New-Object -ComObject WindowsInstaller.Installer
$db = Call $msi OpenDatabase @($Package, 0)
$files = ReadRows $db 'SELECT `File`,`FileName`,`Component_` FROM `File`'
$manifestRows = @(Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json)
if ($files.Count -ne $manifestRows.Count) { throw 'File count mismatch' }
$components = ReadRows $db 'SELECT `Component`,`Directory_`,`KeyPath` FROM `Component`'
$directories = ReadRows $db 'SELECT `Directory`,`Directory_Parent` FROM `Directory`'
foreach ($c in $components) { if ($c[1] -notin @($directories | ForEach-Object { $_[0] })) { throw 'Missing component directory' } }
foreach ($f in $files) { if ($f[2] -notin @($components | ForEach-Object { $_[0] })) { throw 'Missing file component' } }
$startup = ReadRows $db 'SELECT `Root`,`Key`,`Name`,`Value` FROM `Registry`'
if ($startup.Count -ne 1 -or $startup[0][0] -ne '1' -or $startup[0][1] -ne 'Software\Microsoft\Windows\CurrentVersion\Run' -or $startup[0][2] -ne 'EriMixer' -or $startup[0][3] -notmatch '^"\[#F\d+\]" --startup$') { throw 'Incorrect sign-in registration' }
$allUsers = ReadRows $db "SELECT ``Value`` FROM ``Property`` WHERE ``Property``='ALLUSERS'"
if ($allUsers.Count -ne 0) { throw 'Installer must be per-user' }
$properties = ReadRows $db "SELECT ``Value`` FROM ``Property`` WHERE ``Property``='ProductVersion'"
if ($properties.Count -ne 1 -or $properties[0][0] -ne '0.3.0') { throw 'Incorrect MSI product version' }
$upgrade = ReadRows $db 'SELECT `UpgradeCode`,`VersionMax`,`Attributes`,`Remove`,`ActionProperty` FROM `Upgrade`'
if ($upgrade.Count -ne 1 -or $upgrade[0][0] -ne '{A7A6A038-F5C0-4F04-B477-774D5C72C76D}' -or $upgrade[0][1] -ne '0.3.0' -or $upgrade[0][2] -ne '1' -or $upgrade[0][3] -ne 'ALL' -or $upgrade[0][4] -ne 'PREVIOUSVERSIONS') { throw 'Incorrect v0.2.0 major-upgrade detection' }
$sequence = ReadRows $db 'SELECT `Action`,`Sequence` FROM `InstallExecuteSequence`'
$sequence = @($sequence | Where-Object { $_[0] -in @('InstallInitialize', 'RemoveExistingProducts', 'ProcessComponents') })
$sequenceMap = @{}
foreach ($row in $sequence) { $sequenceMap[$row[0]] = [int]$row[1] }
if ($sequenceMap.InstallInitialize -ge $sequenceMap.RemoveExistingProducts -or $sequenceMap.RemoveExistingProducts -ge $sequenceMap.ProcessComponents) { throw 'Old product must be removed before installing the new components' }
$shortcuts = ReadRows $db 'SELECT `Target`,`Arguments` FROM `Shortcut`'
if ($shortcuts.Count -ne 1 -or $shortcuts[0][0] -notmatch '^\[#F\d+\]$' -or $shortcuts[0][1]) { throw 'Incorrect Start menu target' }
New-Item -ItemType Directory -Force -Path $Destination | Out-Null
$Destination = (Resolve-Path -LiteralPath $Destination).Path
$view = Call $db OpenView @("SELECT ``Data`` FROM ``_Streams`` WHERE ``Name``='payload.cab'")
$null = Call $view Execute
$stream = Call $view Fetch
$binary = Call $stream ReadStream @(1, (Get $stream DataSize @(1)), 1)
$cabinet = Join-Path $Destination 'embedded.cab'
[IO.File]::WriteAllBytes($cabinet, [Text.Encoding]::GetEncoding(28591).GetBytes($binary))
$null = Call $view Close
& "$env:WINDIR\System32\expand.exe" '-F:*' $cabinet $Destination | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Embedded cabinet extraction failed' }
foreach ($entry in $manifestRows) {
    $extracted = Join-Path $Destination $entry.FileId
    if ((Get-FileHash -LiteralPath $extracted -Algorithm SHA256).Hash -ne $entry.SHA256) { throw "Payload mismatch: $($entry.Name)" }
}
$summary = Get $db SummaryInformation @(0)
if ((Get $summary Property @(7)) -ne 'x64;1033' -or (Get $summary Property @(15)) -ne 10) { throw 'Wrong installer platform or elevation flags' }
Write-Output "Verified: $($files.Count) embedded files match published SHA256 hashes; v0.3.0 major-upgrade authoring, per-user startup, Start menu, component references, x64/no-elevation metadata."

# Read-only installer-engine session: no install sequence or registration is performed.
$msi.GetType().InvokeMember('UILevel', [Reflection.BindingFlags]::SetProperty, $null, $msi, @(2)) | Out-Null
$session = Call $msi OpenPackage @($Package, 1)
foreach ($action in @('LaunchConditions', 'CostInitialize', 'FileCost', 'CostFinalize')) {
    $result = Call $session DoAction @($action)
    if ($result -ne 1) { throw "Installer engine $action failed: $result" }
}
$folder = Get $session Property @('INSTALLFOLDER')
if ($folder -notlike '*\Programs\EriMixer\') { throw "Unexpected install path: $folder" }
Write-Output "Installer engine resolved: $folder"
