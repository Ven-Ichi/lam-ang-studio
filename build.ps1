param([switch]$TestsOnly,[switch]$NativeChecks)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (!(Test-Path -LiteralPath $root)) { throw "Project folder missing" }
if (!(Test-Path -LiteralPath "$root\dist")) { New-Item -ItemType Directory -Path "$root\dist" | Out-Null }
$common = @('/nologo','/optimize+','/platform:x64','/r:System.Web.Extensions.dll','/r:System.Core.dll',"/resource:$root\src\key-indices.json,KeyIndices.json")
& $compiler @common "/out:$root\dist\ProtocolChecks.exe" "$root\src\Models.cs" "$root\src\Protocol.cs" "$root\src\HidDevice.cs" "$root\tests\ProtocolChecks.cs"
if ($LASTEXITCODE -ne 0) { throw "Protocol check compilation failed" }
& "$root\dist\ProtocolChecks.exe"
if ($LASTEXITCODE -ne 0) { throw "Protocol checks failed" }
& $compiler @common "/out:$root\dist\DeviceCheck.exe" "$root\src\Models.cs" "$root\src\Protocol.cs" "$root\src\HidDevice.cs" "$root\tests\DeviceCheck.cs"
if ($LASTEXITCODE -ne 0) { throw "Device check compilation failed" }
if (!$TestsOnly) {
    $wpf="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF"
    $sources= @("$root\src\Models.cs","$root\src\Protocol.cs","$root\src\HidDevice.cs","$root\src\MainWindow.cs","$root\src\HostActions.cs","$root\src\Program.cs")
    & $compiler @common '/target:winexe' "/out:$root\dist\LamAngStudio.exe" "/r:$wpf\PresentationFramework.dll" "/r:$wpf\PresentationCore.dll" "/r:$wpf\WindowsBase.dll" '/r:System.Xaml.dll' '/r:System.Windows.Forms.dll' "/resource:$root\src\Theme.xaml,Theme.xaml" @sources
    if ($LASTEXITCODE -ne 0) { throw "Application compilation failed" }
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { $hash = [BitConverter]::ToString($sha.ComputeHash([System.IO.File]::ReadAllBytes("$root\dist\LamAngStudio.exe"))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
    [System.IO.File]::WriteAllText("$root\dist\SHA256SUMS.txt", "$hash  LamAngStudio.exe`n", [System.Text.UTF8Encoding]::new($false))
    "Built $root\dist\LamAngStudio.exe"
    if ($NativeChecks) {
        & $compiler @common '/target:exe' '/main:NativeUiChecks' "/out:$root\dist\NativeUiChecks.exe" "/r:$wpf\PresentationFramework.dll" "/r:$wpf\PresentationCore.dll" "/r:$wpf\WindowsBase.dll" '/r:System.Xaml.dll' '/r:System.Windows.Forms.dll' "/resource:$root\src\Theme.xaml,Theme.xaml" @sources "$root\tests\NativeUiChecks.cs"
        if ($LASTEXITCODE -ne 0) { throw "Native UI check compilation failed" }
        & "$root\dist\NativeUiChecks.exe"
        if ($LASTEXITCODE -ne 0) { throw "Native UI checks failed" }
    }
}
