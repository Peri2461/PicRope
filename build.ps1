param([switch]$DesktopTests, [switch]$FolderTests, [switch]$MenuTests, [switch]$EditorTests, [switch]$ThemeTests, [switch]$WindTests, [switch]$Package)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$frameworkPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compilerPath = Join-Path $frameworkPath 'csc.exe'
$buildPath = Join-Path $projectRoot 'build'
New-Item -ItemType Directory -Path $buildPath -Force | Out-Null
$references = @(
    "/r:$frameworkPath\WPF\PresentationCore.dll",
    "/r:$frameworkPath\WPF\PresentationFramework.dll",
    "/r:$frameworkPath\WPF\WindowsBase.dll",
    "/r:$frameworkPath\System.Xaml.dll",
    "/r:$frameworkPath\System.Web.Extensions.dll",
    "/r:$frameworkPath\Microsoft.VisualBasic.dll",
    "/r:$projectRoot\vendor\WebView2\Microsoft.Web.WebView2.Core.dll",
    "/r:$projectRoot\vendor\WebView2\Microsoft.Web.WebView2.Wpf.dll",
    '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll'
)
$sources = (Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs').FullName
$manifestPath = Join-Path $projectRoot 'src\app.manifest'
$iconPath = Join-Path $projectRoot 'src\assets\PicRope.ico'
& $compilerPath /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 "/out:$buildPath\PicRope.exe" "/win32manifest:$manifestPath" "/win32icon:$iconPath" "/resource:$iconPath,PicRope.Icon" $references $sources
if ($LASTEXITCODE -ne 0) { throw '应用构建失败' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\App.config') -Destination (Join-Path $buildPath 'PicRope.exe.config')
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE'),(Join-Path $projectRoot 'NOTICE') -Destination $buildPath
$editorPath = Join-Path $buildPath 'editor'
$noticePath = Join-Path $buildPath 'third-party-notices'
New-Item -ItemType Directory -Path $editorPath,(Join-Path $noticePath 'Filerobot'),(Join-Path $noticePath 'WebView2') -Force | Out-Null
Copy-Item -Path (Join-Path $projectRoot 'src\editor\*') -Destination $editorPath
Copy-Item -LiteralPath (Join-Path $projectRoot 'vendor\Filerobot\filerobot-image-editor.min.js') -Destination $editorPath
Copy-Item -LiteralPath (Join-Path $projectRoot 'vendor\Filerobot\LICENSE'),(Join-Path $projectRoot 'vendor\Filerobot\THIRD-PARTY-NOTICES.txt') -Destination (Join-Path $noticePath 'Filerobot')
Copy-Item -LiteralPath (Join-Path $projectRoot 'vendor\WebView2\LICENSE.txt'),(Join-Path $projectRoot 'vendor\WebView2\NOTICE.txt') -Destination (Join-Path $noticePath 'WebView2')
Copy-Item -LiteralPath (Join-Path $projectRoot 'vendor\WebView2\Microsoft.Web.WebView2.Core.dll'),(Join-Path $projectRoot 'vendor\WebView2\Microsoft.Web.WebView2.Wpf.dll'),(Join-Path $projectRoot 'vendor\WebView2\WebView2Loader.dll') -Destination $buildPath
Write-Output ('构建完成：' + (Join-Path $buildPath 'PicRope.exe'))

if (-not ($FolderTests -or $MenuTests -or $EditorTests -or $ThemeTests -or $WindTests) -and (Test-Path -LiteralPath (Join-Path $projectRoot 'tests\CoreTests.cs'))) {
    & $compilerPath /nologo /target:exe /codepage:65001 "/out:$buildPath\CoreTests.exe" /main:ScreenLine.Tests.CoreTests $references (Join-Path $projectRoot 'src\Core.cs') (Join-Path $projectRoot 'src\SpringMotion.cs') (Join-Path $projectRoot 'tests\CoreTests.cs')
    if ($LASTEXITCODE -ne 0) { throw '核心测试构建失败' }
    & (Join-Path $buildPath 'CoreTests.exe')
    if ($LASTEXITCODE -ne 0) { throw '核心测试未通过' }
}

if ($DesktopTests -or $MenuTests -or $EditorTests -or $ThemeTests -or $WindTests) {
    $desktopReferences = $references + @("/r:$frameworkPath\WPF\UIAutomationClient.dll", "/r:$frameworkPath\WPF\UIAutomationTypes.dll")
    & $compilerPath /nologo /target:exe /platform:x64 /codepage:65001 "/out:$buildPath\DesktopTests.exe" /main:ScreenLine.Tests.DesktopTests "/win32manifest:$manifestPath" "/win32icon:$iconPath" "/resource:$iconPath,PicRope.Icon" $desktopReferences $sources (Join-Path $projectRoot 'tests\DesktopTests.cs')
    if ($LASTEXITCODE -ne 0) { throw '桌面测试构建失败' }
    Copy-Item -LiteralPath (Join-Path $projectRoot 'src\App.config') -Destination (Join-Path $buildPath 'DesktopTests.exe.config')
    if ($WindTests) { & (Join-Path $buildPath 'DesktopTests.exe') --wind }
    elseif ($ThemeTests) { & (Join-Path $buildPath 'DesktopTests.exe') --themes }
    elseif ($MenuTests) { & (Join-Path $buildPath 'DesktopTests.exe') --menus }
    elseif ($EditorTests) { & (Join-Path $buildPath 'DesktopTests.exe') --editor }
    else { & (Join-Path $buildPath 'DesktopTests.exe') }
    if ($LASTEXITCODE -ne 0) { throw '桌面测试未通过' }
}

if ($EditorTests) {
    & $compilerPath /nologo /target:exe /codepage:65001 "/out:$buildPath\EditorFileTests.exe" /main:ScreenLine.Tests.EditorFileTests $references (Join-Path $projectRoot 'src\Core.cs') (Join-Path $projectRoot 'src\ImageEditFiles.cs') (Join-Path $projectRoot 'tests\EditorFileTests.cs')
    if ($LASTEXITCODE -ne 0) { throw '编辑图片文件测试构建失败' }
    & (Join-Path $buildPath 'EditorFileTests.exe')
    if ($LASTEXITCODE -ne 0) { throw '编辑图片文件测试未通过' }
}

if ($FolderTests) {
    & $compilerPath /nologo /target:exe /codepage:65001 "/out:$buildPath\FolderOpenTests.exe" $references (Join-Path $projectRoot 'src\Core.cs') (Join-Path $projectRoot 'src\WindowsShell.cs') (Join-Path $projectRoot 'tests\FolderOpenTests.cs')
    if ($LASTEXITCODE -ne 0) { throw '目录打开测试构建失败' }
    & (Join-Path $buildPath 'FolderOpenTests.exe')
    if ($LASTEXITCODE -ne 0) { throw '目录打开测试未通过' }
}

if ($Package) {
    & (Join-Path $projectRoot 'package.ps1')
}
