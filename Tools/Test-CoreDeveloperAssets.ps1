[CmdletBinding()]
param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$JsonOutput,
    [switch]$Strict
)

$ErrorActionPreference = 'Stop'
$characters = @('Hunter', 'Runner', 'Ambusher', 'Elite')
$requirements = [System.Collections.Generic.List[object]]::new()

function Add-Requirement {
    param(
        [string]$Character,
        [string]$Group,
        [string]$RelativePath
    )

    $fullPath = Join-Path $ProjectRoot $RelativePath
    $requirements.Add([pscustomobject]@{
        Character = $Character
        Group = $Group
        Path = $RelativePath.Replace('\', '/')
        Present = Test-Path -LiteralPath $fullPath -PathType Leaf
    })
}

foreach ($character in $characters) {
    $sourceRoot = 'ArtSource\CustomEnemies\SculptTextureRevision4'
    $unityRoot = 'Assets\Art\CustomEnemies'

    Add-Requirement $character 'Blender source' "$sourceRoot\Blender\${character}_Sculpt4.blend"
    Add-Requirement $character 'Blender source' "$sourceRoot\Blender\${character}_Sculpt4_EditableMaterials.blend"

    foreach ($texture in @('BaseColor_4K', 'Normal_2K', 'Roughness_2K', 'Metallic_2K')) {
        Add-Requirement $character 'Source texture' "$sourceRoot\Textures\${character}_${texture}.png"
    }

    Add-Requirement $character 'FBX export' "$sourceRoot\UnityExports\${character}_Animated.fbx"
    Add-Requirement $character 'Unity model' "$unityRoot\Models\${character}_Animated.fbx"
    Add-Requirement $character 'Unity model metadata' "$unityRoot\Models\${character}_Animated.fbx.meta"
    Add-Requirement $character 'Unity material' "$unityRoot\Materials\${character}_Sculpt4_PBR_Atlas.mat"
    Add-Requirement $character 'Unity material metadata' "$unityRoot\Materials\${character}_Sculpt4_PBR_Atlas.mat.meta"
    Add-Requirement $character 'Enemy prefab' "Assets\Prefabs\Enemies\${character}.prefab"
    Add-Requirement $character 'Enemy prefab metadata' "Assets\Prefabs\Enemies\${character}.prefab.meta"
    Add-Requirement $character 'Blender preview' "$sourceRoot\Previews\${character}_Actual_Blender.png"
    Add-Requirement $character 'Blender face preview' "$sourceRoot\Previews\${character}_Face_Actual_Blender.png"
    Add-Requirement $character 'Technical evidence' "$sourceRoot\Evidence\${character}.json"
}

$sharedRequirements = @(
    @('Review scene', 'ArtSource\CustomEnemies\SculptTextureRevision4\Blender\Four_Enemies_Sculpt4_Review.blend'),
    @('Lineup preview', 'ArtSource\CustomEnemies\SculptTextureRevision4\Previews\Four_Enemies_Actual_Blender.png'),
    @('Motion preview', 'ArtSource\CustomEnemies\SculptTextureRevision4\Previews\Walk_Actual_Blender.png'),
    @('Motion preview', 'ArtSource\CustomEnemies\SculptTextureRevision4\Previews\Attack_Actual_Blender.png'),
    @('Technical evidence', 'ArtSource\CustomEnemies\SculptTextureRevision4\Evidence\review.json')
)

foreach ($item in $sharedRequirements) {
    Add-Requirement 'Shared' $item[0] $item[1]
}

$presentCount = @($requirements | Where-Object Present).Count
$missing = @($requirements | Where-Object { -not $_.Present })
$summary = [pscustomobject]@{
    SchemaVersion = 1
    CheckedAt = (Get-Date).ToString('o')
    Repository = Split-Path -Leaf (Resolve-Path -LiteralPath $ProjectRoot).Path
    ProjectRoot = '.'
    Required = $requirements.Count
    Present = $presentCount
    Missing = $missing.Count
    Complete = $missing.Count -eq 0
    Assets = @($requirements)
}

$requirements |
    Sort-Object Character, Group, Path |
    Format-Table Character, Group, Present, Path -AutoSize

Write-Host "`nCore Developer assets: $presentCount/$($requirements.Count) present."

if ($JsonOutput) {
    $outputFile = if ([System.IO.Path]::IsPathRooted($JsonOutput)) {
        $JsonOutput
    } else {
        Join-Path $ProjectRoot $JsonOutput
    }

    $outputDirectory = Split-Path -Parent $outputFile
    if ($outputDirectory -and -not (Test-Path -LiteralPath $outputDirectory)) {
        New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    }

    $summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $outputFile -Encoding utf8
    Write-Host "Validation JSON written to $outputFile"
}

if ($Strict -and $missing.Count -gt 0) {
    exit 1
}
