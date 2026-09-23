<#
.SYNOPSIS
Renders art/superpowers-icon.xaml to the PNG sizes the VSIX ships. Run with Windows PowerShell (STA).
#>
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase

$root = Split-Path -Parent $PSScriptRoot
$xaml = Get-Content -Raw (Join-Path $root 'art\superpowers-icon.xaml')
$vsix = Join-Path $root 'TheKameleon.Superpowers.Vsix'

$targets = @(
    @{ Size = 16;  Path = 'Images\Superpowers.16.16.png' },
    @{ Size = 20;  Path = 'Images\Superpowers.20.20.png' },
    @{ Size = 32;  Path = 'Images\Superpowers.32.32.png' },
    @{ Size = 128; Path = 'Resources\icon.png' },
    @{ Size = 200; Path = 'Resources\preview.png' }
)

foreach ($target in $targets) {
    $size = $target.Size
    $viewbox = New-Object System.Windows.Controls.Viewbox
    $viewbox.Child = [System.Windows.Markup.XamlReader]::Parse($xaml)
    $viewbox.Width = $size
    $viewbox.Height = $size
    $viewbox.Measure((New-Object System.Windows.Size($size, $size)))
    $viewbox.Arrange((New-Object System.Windows.Rect(0, 0, $size, $size)))
    $viewbox.UpdateLayout()

    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($size, $size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($viewbox)
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))

    $output = Join-Path $vsix $target.Path
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $output) | Out-Null
    $stream = [System.IO.File]::Create($output)
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
    Write-Host "Rendered $($target.Path) ($size x $size)"
}
