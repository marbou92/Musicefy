# scripts/fix-msbuild-platforms.ps1
# Run from repository root in Codespaces: pwsh scripts/fix-msbuild-platforms.ps1

$projects = @("Musicefy.Core\Musicefy.Core.csproj","Musicefy\Musicefy.csproj")
foreach ($proj in $projects) {
  if (-not (Test-Path $proj)) { Write-Host "Skipping missing $proj"; continue }
  $text = Get-Content -Raw -Path $proj

  # Ensure PlatformTarget exists
  if ($text -notmatch "<PlatformTarget>") {
    $text = $text -replace "(<PropertyGroup>[\s\S]*?</PropertyGroup>)", '$1' # no-op to keep structure
    $text = $text -replace "(<PropertyGroup>)", "<PropertyGroup>`r`n    <PlatformTarget>AnyCPU</PlatformTarget>"
  } else {
    $text = $text -replace "<PlatformTarget>.*?</PlatformTarget>","<PlatformTarget>AnyCPU</PlatformTarget>"
  }

  # Ensure OutputPath groups for both spellings and both configs
  $needed = @(
    "Condition=`'$(Configuration)|$(Platform)`'=='Debug|Any CPU'",
    "Condition=`'$(Configuration)|$(Platform)`'=='Release|Any CPU'",
    "Condition=`'$(Configuration)|$(Platform)`'=='Debug|AnyCPU'",
    "Condition=`'$(Configuration)|$(Platform)`'=='Release|AnyCPU'"
  )

  foreach ($cond in $needed) {
    if ($text -notmatch [regex]::Escape($cond)) {
      $group = "`r`n  <PropertyGroup $cond>`r`n    <OutputPath>bin\$((($cond -split '\|')[0] -replace "Condition=`'|\''","") -replace 'Debug|Release','')\</OutputPath>`r`n  </PropertyGroup>`r`n"
      # simpler: append Release/Debug groups near top
      $text = $text -replace "(</PropertyGroup>)", "$&`r`n$group"
    }
  }

  Set-Content -Path $proj -Value $text -Encoding UTF8
  Write-Host "Patched $proj"
}

# Fix the .sln: add Release|AnyCPU entries if missing
$sln = "Musicefy.sln"
if (Test-Path $sln) {
  $s = Get-Content -Raw -Path $sln
  if ($s -notmatch "Release\|AnyCPU") {
    # Add Release|AnyCPU to SolutionConfigurationPlatforms if missing
    $s = $s -replace "(GlobalSection\(SolutionConfigurationPlatforms\) = preSolution\s*)([\s\S]*?)(\s*EndGlobalSection)",
      '$1$2' + "`r`n        Release|AnyCPU = Release|AnyCPU`r`n$3"

    # For each project mapping, duplicate Debug|Any CPU -> Release|AnyCPU if needed
    $s = $s -replace "(\{[0-9A-Fa-f\-]+\}\.[^\r\n]*Debug\|Any CPU[^\r\n]*)",
      { param($m) $m.Value + "`r`n" + ($m.Value -replace "Debug\|Any CPU","Release|AnyCPU") }

    Set-Content -Path $sln -Value $s -Encoding UTF8
    Write-Host "Patched $sln with Release|AnyCPU entries"
  } else {
    Write-Host "$sln already contains Release|AnyCPU"
  }
} else {
  Write-Host "Solution file not found: $sln"
}
