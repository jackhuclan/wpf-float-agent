# Floaticon 构建脚本
# 用法：
#   .\build.ps1                 # win-x64 自包含单文件发布（Floaticon-<版本>.exe）-> 根目录 release\
#   .\build.ps1 -Clean          # 先清理 release / artifacts 再发布
#   .\build.ps1 -Pack           # 发布 + 将 FloatAgent / FloatAgent.Abstractions 打成 NuGet 包 -> 根目录 artifacts\
#   .\build.ps1 -Pack -VersionSuffix preview   # 生成 1.1.0-preview 版本号的包
[CmdletBinding()]
param(
    # 打 NuGet 包（FloatAgent、FloatAgent.Abstractions）
    [switch]$Pack,

    # 先删除旧的 release / artifacts 目录
    [switch]$Clean,

    # 编译配置，默认 Release
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    # 可选的包版本后缀（如 preview、rc.1），生成 1.1.0-xxx 形式的版本号
    [string]$VersionSuffix = ''
)

$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot
$ReleaseDir = Join-Path $Root 'release'
$ArtifactsDir = Join-Path $Root 'artifacts'
$Solution = Join-Path $Root 'Floaticon.slnx'

function Write-Step([string]$Message) {
    Write-Host "`n=== $Message ===" -ForegroundColor Cyan
}

Write-Step "Floaticon 构建（配置：$Configuration）"

if ($Clean) {
    Write-Step "清理旧的输出目录"
    foreach ($dir in @($ReleaseDir, $ArtifactsDir)) {
        if (Test-Path $dir) {
            Remove-Item $dir -Recurse -Force
            Write-Host "已删除 $dir"
        }
    }
    dotnet clean $Solution -c $Configuration -v q --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet clean 失败（退出码 $LASTEXITCODE）" }
}

# 1) Release 单文件发布：win-x64 自包含 .NET 运行时，用户机器无需安装 .NET；
#    产物为单个 exe，文件名带版本号（Floaticon-<版本>.exe）
Write-Step "单文件发布（win-x64 self-contained）-> $ReleaseDir"
dotnet publish (Join-Path $Root 'src\Floaticon\Floaticon.csproj') `
    -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none -p:DebugSymbols=false `
    -o $ReleaseDir --nologo
if ($LASTEXITCODE -ne 0) { throw "发布失败（退出码 $LASTEXITCODE）" }

# 版本号从 Directory.Build.props 读取（唯一事实来源），重命名 exe
[xml]$buildProps = Get-Content (Join-Path $Root 'Directory.Build.props')
$version = @($buildProps.Project.PropertyGroup)[0].Version
$exeName = "Floaticon-$version.exe"
$plainExe = Join-Path $ReleaseDir 'Floaticon.exe'
$versionedExe = Join-Path $ReleaseDir $exeName
if (Test-Path $plainExe) {
    Move-Item $plainExe $versionedExe -Force
}

if (Test-Path $versionedExe) {
    $sizeMb = [math]::Round((Get-Item $versionedExe).Length / 1MB, 1)
    Write-Host "发布完成：$versionedExe（$sizeMb MB）" -ForegroundColor Green
} else {
    Write-Warning "未在 $ReleaseDir 找到 $exeName，请检查发布输出。"
}

# 2) 可选：把类库打成 NuGet 包，放到根目录 artifacts\
if ($Pack) {
    Write-Step "打包 NuGet 包 -> $ArtifactsDir"
    New-Item $ArtifactsDir -ItemType Directory -Force | Out-Null

    $packTargets = @(
        (Join-Path $Root 'src\FloatAgent.Abstractions\FloatAgent.Abstractions.csproj'),
        (Join-Path $Root 'src\FloatAgent\FloatAgent.csproj')
    )

    foreach ($project in $packTargets) {
        # dotnet pack 会先编译再打包，无需单独 build
        $packArgs = @('pack', $project, '-c', $Configuration, '-o', $ArtifactsDir, '--nologo')
        if ($VersionSuffix) { $packArgs += @('--version-suffix', $VersionSuffix) }
        dotnet @packArgs
        if ($LASTEXITCODE -ne 0) { throw "打包 $project 失败（退出码 $LASTEXITCODE）" }
    }

    Write-Host "NuGet 包已生成：" -ForegroundColor Green
    Get-ChildItem $ArtifactsDir -Filter *.nupkg | ForEach-Object { Write-Host "  $($_.Name)" }
}

Write-Step "构建结束"
