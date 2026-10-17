[CmdletBinding()]
param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$JsonOutput
)

$ErrorActionPreference = 'Stop'
$characters = @('Hunter', 'Runner', 'Ambusher', 'Elite')
$enemyRoot = Join-Path $ProjectRoot 'Assets\Prefabs\Enemies'
$modelRoot = Join-Path $ProjectRoot 'Assets\Art\CustomEnemies\Models'
$failures = [System.Collections.Generic.List[string]]::new()

function Read-Text([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Missing required file: $Path"
    }
    return Get-Content -LiteralPath $Path -Raw
}

function Read-UnityGuid([string]$MetaPath) {
    $text = Read-Text $MetaPath
    $match = [regex]::Match($text, '(?m)^guid:\s*([0-9a-f]{32})\s*$')
    if (-not $match.Success) {
        throw "Missing Unity GUID in $MetaPath"
    }
    return $match.Groups[1].Value
}

function Require([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        $failures.Add($Message)
    }
}

$basePath = Join-Path $enemyRoot 'EnemyBase.prefab'
$baseText = Read-Text $basePath
$baseGuid = Read-UnityGuid "$basePath.meta"

Require ($baseText -match '(?m)^\s*m_LocalScale:\s*\{x:\s*1,\s*y:\s*1,\s*z:\s*1\}\s*$') `
    'EnemyBase root scale must remain (1, 1, 1).'
Require ($baseText -match '(?m)^CapsuleCollider:\s*$') `
    'EnemyBase must contain its shared CapsuleCollider.'
Require ($baseText -match '(?m)^\s*m_Radius:\s*0\.35\s*$') `
    'EnemyBase capsule radius must remain 0.35 m.'
Require ($baseText -match '(?m)^\s*m_Height:\s*1\.9\s*$') `
    'EnemyBase capsule height must remain 1.9 m.'
Require ($baseText -match '(?m)^\s*m_Center:\s*\{x:\s*0,\s*y:\s*0\.95,\s*z:\s*0\}\s*$') `
    'EnemyBase capsule center must remain at (0, 0.95, 0).'

$results = foreach ($character in $characters) {
    $prefabPath = Join-Path $enemyRoot "$character.prefab"
    $modelMetaPath = Join-Path $modelRoot "${character}_Animated.fbx.meta"
    $prefabText = Read-Text $prefabPath
    $modelGuid = Read-UnityGuid $modelMetaPath

    $inheritsBase = $prefabText.Contains("guid: $baseGuid")
    $referencesModel = $prefabText.Contains("guid: $modelGuid")
    Require $inheritsBase "$character prefab does not inherit EnemyBase."
    Require $referencesModel "$character prefab does not reference its imported FBX."

    $positionMatches = [regex]::Matches(
        $prefabText,
        'propertyPath:\s*m_LocalPosition\.(x|y|z)\s*\r?\n\s*value:\s*([^\r\n]+)'
    )
    $positionValues = @($positionMatches | ForEach-Object {
        [double]::Parse($_.Groups[2].Value.Trim(), [Globalization.CultureInfo]::InvariantCulture)
    })
    $zeroPlacement = $positionValues.Count -ge 3 -and
        @($positionValues | Where-Object { [math]::Abs($_) -gt 0.00001 }).Count -eq 0
    Require $zeroPlacement "$character visual position overrides must remain at the authored origin."

    $scaleMatches = [regex]::Matches(
        $prefabText,
        'propertyPath:\s*m_LocalScale\.(x|y|z)\s*\r?\n\s*value:\s*([^\r\n]+)'
    )
    $scaleValues = @($scaleMatches | ForEach-Object {
        [double]::Parse($_.Groups[2].Value.Trim(), [Globalization.CultureInfo]::InvariantCulture)
    })
    $boundedScale = @($scaleValues | Where-Object { $_ -lt 0.5 -or $_ -gt 1.5 }).Count -eq 0
    Require $boundedScale "$character contains an unsafe visual scale outside 0.5-1.5."

    [pscustomobject]@{
        Character = $character
        InheritsEnemyBase = $inheritsBase
        ReferencesModel = $referencesModel
        PositionOverrides = $positionValues
        ScaleOverrides = $scaleValues
        PlacementAtOrigin = $zeroPlacement
        ScaleWithinBounds = $boundedScale
    }
}

$report = [pscustomobject]@{
    SchemaVersion = 1
    CheckedAt = (Get-Date).ToString('o')
    EnemyBaseGuid = $baseGuid
    Collider = [pscustomobject]@{
        Type = 'CapsuleCollider'
        Radius = 0.35
        Height = 1.9
        Center = @(0.0, 0.95, 0.0)
    }
    Characters = @($results)
    Failures = @($failures)
    Passed = $failures.Count -eq 0
}

$results | Format-Table Character, InheritsEnemyBase, ReferencesModel,
    PlacementAtOrigin, ScaleWithinBounds -AutoSize

if ($JsonOutput) {
    $outputPath = if ([IO.Path]::IsPathRooted($JsonOutput)) {
        $JsonOutput
    } else {
        Join-Path $ProjectRoot $JsonOutput
    }
    $directory = Split-Path -Parent $outputPath
    if ($directory -and -not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $outputPath -Encoding utf8
}

if ($failures.Count) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'Enemy prefab scale, collider inheritance, and visual placement checks passed.'
