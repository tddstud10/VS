param (
    [Parameter(Mandatory=$true)]
    [string]
    $Version
)

$ErrorActionPreference = "Stop"

$RootDir = Split-Path (Split-Path $MyInvocation.MyCommand.Path)

Write-Output "Parameter Version = $Version"

Write-Output "Updating vsix manifest... "
$manifestPath = Join-path $RootDir "TddStudioPackage\source.extension.vsixmanifest"
$xml = [xml](Get-Content $manifestPath)
if (-not $xml.PackageManifest.Metadata.Identity.Version)
{
    throw "PackageManifest/Metadata/Identity[Version] not found!"
}
else
{
    $xml.PackageManifest.Metadata.Identity.Version = $Version
}
$xml.Save($manifestPath)
Write-Output "Done"

Write-Output "Updating Constants.?s... "
Get-ChildItem "$RootDir\Constants.?s" | ForEach-Object {
    $verPattern = ' ProductVersion = \".*\";$'
    $contents = Get-Content $_
    if (-not ($contents -match $verPattern))
    {
        throw "ProductVersion literal not found in $_"
    }
    else
    {
        $contents = $contents -replace $verPattern, " ProductVersion = `"$Version`";"
    }

    Out-File -FilePath $_ -InputObject $contents -Encoding utf8
}
Write-Output "Done"
