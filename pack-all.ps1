# 一键打包所有库项目
# 用法: .\pack-all.ps1 [-Configuration Debug|Release] [-OutputDir .\nupkg]

param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$OutputDir = (Join-Path $PSScriptRoot 'nupkg')
)

$projects = @(
    'projectFrameCut.Drawing.Base'
    'projectFrameCut.Drawing.Effect'
    'projectFrameCut.Drawing.Processing'
    'projectFrameCut.Drawing.Text'
    'projectFrameCut.Drawing.Vector'
    'projectFrameCut.Drawing'
)

Write-Host "=== 一键打包所有库 ===" -ForegroundColor Cyan
Write-Host "配置: $Configuration"
Write-Host "输出: $OutputDir"
Write-Host

# 先还原
Write-Host ">>> 还原 NuGet 包..." -ForegroundColor Yellow
dotnet restore "$PSScriptRoot\projectFrameCut.Drawing\projectFrameCut.Drawing.csproj"
if ($LASTEXITCODE -ne 0) { Write-Host "还原失败！" -ForegroundColor Red; exit 1 }

Write-Host

$success = 0
$failed = 0

foreach ($proj in $projects) {
    $csproj = Join-Path $PSScriptRoot $proj "$proj.csproj"
    $name = Split-Path $proj -Leaf

    Write-Host ">>> 打包 $name ..." -ForegroundColor Yellow

    dotnet pack $csproj `
        --configuration $Configuration `
        --output $OutputDir `
        --no-restore `
        -p:IncludeSymbols=true `
        -p:SymbolPackageFormat=snupkg `
        -p:ContinuousIntegrationBuild=true `
        -p:Deterministic=true

    if ($LASTEXITCODE -eq 0) {
        Write-Host "    ✓ $name 打包成功" -ForegroundColor Green
        $success++
    } else {
        Write-Host "    ✗ $name 打包失败" -ForegroundColor Red
        $failed++
    }
}

Write-Host
Write-Host "=== 完成 ===" -ForegroundColor Cyan
Write-Host "成功: $success | 失败: $failed"

if ($success -gt 0) {
    $count = (Get-ChildItem $OutputDir -Filter '*.nupkg' | Measure-Object).Count
    Write-Host "生成 $count 个 nupkg 文件到: $OutputDir" -ForegroundColor Green
}
