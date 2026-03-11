param(
    [Parameter(Mandatory = $false)]
    [string]$VdprojPath = "MSI.Installer\MSI.Installer.vdproj",

    [Parameter(Mandatory = $false)]
    [string]$PublishSourceForFileList = "_publish\KGV.Wpf\0.1.0",

    [Parameter(Mandatory = $false)]
    [string]$PublishBasePathInVdproj = "D:\Programmieren\KGV-Publish\AppFiles\0.1.0"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $VdprojPath)) {
    throw "vdproj not found: $VdprojPath"
}

function Get-NamedBlockBounds([string]$inputText, [string]$blockName) {
    $needle = '"' + $blockName + '"'
    $start = $inputText.IndexOf($needle, [System.StringComparison]::Ordinal)
    if ($start -lt 0) {
        throw "Block not found: $blockName"
    }

    $open = $inputText.IndexOf('{', $start)
    if ($open -lt 0) {
        throw "Opening '{' not found for block: $blockName"
    }

    $depth = 0
    $close = -1
    for ($i = $open; $i -lt $inputText.Length; $i++) {
        $ch = $inputText[$i]
        if ($ch -eq '{') { $depth++ }
        elseif ($ch -eq '}') {
            $depth--
            if ($depth -eq 0) {
                $close = $i
                break
            }
        }
    }

    if ($close -lt 0) {
        throw "Closing '}' not found for block: $blockName"
    }

    return [pscustomobject]@{
        Start = $start
        OpenBrace = $open
        CloseBrace = $close
    }
}

if (-not (Test-Path -LiteralPath $PublishSourceForFileList)) {
    throw "Publish folder (for file list) not found: $PublishSourceForFileList`nRun: dotnet publish KGV.Wpf\\KGV.Wpf.csproj -c Release -f net8.0-windows -o $PublishSourceForFileList --self-contained false"
}

$md5 = [System.Security.Cryptography.MD5]::Create()
function Get-KeyFromName([string]$name) {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($name)
    $hash = $md5.ComputeHash($bytes)
    return ($hash | ForEach-Object { $_.ToString('x2') }) -join ''
}

$files = Get-ChildItem -LiteralPath $PublishSourceForFileList -File |
    Where-Object { $_.Extension -notin @('.pdb', '.xml') } |
    Sort-Object Name

if ($files.Count -eq 0) {
    throw "No files found in publish folder: $PublishSourceForFileList"
}

$exe = $files | Where-Object { $_.Name -ieq 'KGV.Wpf.exe' } | Select-Object -First 1
if (-not $exe) {
    throw "KGV.Wpf.exe not found in publish folder. Ensure OutputType is WinExe and publish was successful. Folder: $PublishSourceForFileList"
}

$exeKey = '_' + (Get-KeyFromName $exe.Name)

$escapedBase = $PublishBasePathInVdproj.Replace('\', '\\')

$entryTypeGuid = '{5259A561-127C-4D43-A0A1-72F10C7B3BF8}'

$entries = New-Object System.Collections.Generic.List[string]
$dq = '"'
$indent = "`t`t`t"
foreach ($f in $files) {
    $key = '_' + (Get-KeyFromName $f.Name)

    $entries.Add($indent + $dq + $entryTypeGuid + ':' + $key + $dq)
    $entries.Add($indent + '{')
    $entries.Add($indent + $dq + 'SourcePath' + $dq + ' = ' + $dq + '8:' + $escapedBase + '\\' + $f.Name + $dq)
    $entries.Add($indent + $dq + 'TargetName' + $dq + ' = ' + $dq + '8:' + $dq)
    $entries.Add($indent + $dq + 'Tag' + $dq + ' = ' + $dq + '8:' + $dq)
    $entries.Add($indent + $dq + 'Folder' + $dq + ' = ' + $dq + '8:_8DDE0BA901704C4DA02C0A766095372D' + $dq)
    $entries.Add($indent + $dq + 'Condition' + $dq + ' = ' + $dq + '8:' + $dq)
    $entries.Add($indent + $dq + 'Transitive' + $dq + ' = ' + $dq + '11:FALSE' + $dq)
    $entries.Add($indent + $dq + 'Vital' + $dq + ' = ' + $dq + '11:TRUE' + $dq)
    $entries.Add($indent + $dq + 'ReadOnly' + $dq + ' = ' + $dq + '11:FALSE' + $dq)
    $entries.Add($indent + $dq + 'Hidden' + $dq + ' = ' + $dq + '11:FALSE' + $dq)
    $entries.Add($indent + $dq + 'System' + $dq + ' = ' + $dq + '11:FALSE' + $dq)
    $entries.Add($indent + $dq + 'Permanent' + $dq + ' = ' + $dq + '11:FALSE' + $dq)
    $entries.Add($indent + $dq + 'SharedLegacy' + $dq + ' = ' + $dq + '11:FALSE' + $dq)
    $entries.Add($indent + $dq + 'PackageAs' + $dq + ' = ' + $dq + '3:1' + $dq)
    $entries.Add($indent + $dq + 'Register' + $dq + ' = ' + $dq + '3:1' + $dq)
    $entries.Add($indent + $dq + 'Exclude' + $dq + ' = ' + $dq + '11:FALSE' + $dq)
    $entries.Add($indent + $dq + 'IsDependency' + $dq + ' = ' + $dq + '11:FALSE' + $dq)
    $entries.Add($indent + $dq + 'IsolateTo' + $dq + ' = ' + $dq + '8:' + $dq)
    $entries.Add($indent + '}')
}

$newFileBlock = "`t`t" + $dq + 'File' + $dq + "`r`n`t`t{`r`n" + ($entries -join "`r`n") + "`r`n`t`t}"

$publishCmd = "dotnet publish ..\\KGV.Wpf\\KGV.Wpf.csproj -c Release -f net8.0-windows -o $PublishBasePathInVdproj --self-contained false"
$publishCmdEscaped = $publishCmd.Replace('\', '\\')

$text = Get-Content -LiteralPath $VdprojPath -Raw

function Replace-NamedBlock([string]$inputText, [string]$blockName, [string]$replacementBlock) {
    $needle = '"' + $blockName + '"'
    $start = $inputText.IndexOf($needle, [System.StringComparison]::Ordinal)
    if ($start -lt 0) {
        throw "Block not found: $blockName"
    }

    $open = $inputText.IndexOf('{', $start)
    if ($open -lt 0) {
        throw "Opening '{' not found for block: $blockName"
    }

    $depth = 0
    $close = -1
    for ($i = $open; $i -lt $inputText.Length; $i++) {
        $ch = $inputText[$i]
        if ($ch -eq '{') { $depth++ }
        elseif ($ch -eq '}') {
            $depth--
            if ($depth -eq 0) {
                $close = $i
                break
            }
        }
    }

    if ($close -lt 0) {
        throw "Closing '}' not found for block: $blockName"
    }

    $end = $inputText.IndexOf("`n", $close)
    if ($end -lt 0) {
        $end = $inputText.Length
    }
    else {
        $end++
    }

    return $inputText.Substring(0, $start) + $replacementBlock + $inputText.Substring($end)
}

# Backup once
$backup = "$VdprojPath.bak"
if (-not (Test-Path -LiteralPath $backup)) {
    Copy-Item -LiteralPath $VdprojPath -Destination $backup
}

# 1) Replace Deployable/File block
$text = Replace-NamedBlock -inputText $text -blockName 'File' -replacementBlock ($newFileBlock + "`r`n")

# 2) Remove ProjectOutput usage (Primary Output)
$emptyProjectOutputBlock = "`t`t" + $dq + 'ProjectOutput' + $dq + "`r`n`t`t{`r`n`t`t}`r`n"
$text = Replace-NamedBlock -inputText $text -blockName 'ProjectOutput' -replacementBlock $emptyProjectOutputBlock

# 3) Ensure shortcut points to EXE
$text = [regex]::Replace(
    $text,
    '(?m)^(\s*\"Target\"\s*=\s*\"8:)_.*?(\"\r?)$',
    "`$1$exeKey`$2",
    1
)

# 4) Update Hierarchy MsmKey to the EXE key
$text = [regex]::Replace(
    $text,
    '(?m)^(\s*\"MsmKey\"\s*=\s*\"8:)_.*?(\"\r?)$',
    "`$1$exeKey`$2",
    1
)

# 5) Add pre-build publish step
$text = [regex]::Replace(
    $text,
    '(?m)^(\s*\"PreBuildEvent\"\s*=\s*\"8:).*?(\"\r?)$',
    "`$1$publishCmdEscaped`$2",
    1
)

# 6) Desktop shortcut (additional to Start Menu)
$desktopFolderKey = '_8A2C0342D3344710AA3A0A8B37832EDF'
if ($text -notmatch [regex]::Escape('"Folder" = "8:' + $desktopFolderKey + '"')) {
    $bounds = Get-NamedBlockBounds -inputText $text -blockName 'Shortcut'

    $desktopShortcutKey = '_' + (Get-KeyFromName 'DesktopShortcut:KGV.Wpf.exe')
    $shortcutTypeGuid = '{970C0BB2-C7D0-45D7-ABFA-7EC378858BC0}'

    $desktopShortcutLines = @(
        ('            "' + $shortcutTypeGuid + ':' + $desktopShortcutKey + '"'),
        '            {',
        '            "Name" = "8:KGV-Oberrothenbach"',
        '            "Arguments" = "8:"',
        '            "Description" = "8:"',
        '            "ShowCmd" = "3:1"',
        '            "IconIndex" = "3:0"',
        '            "Transitive" = "11:FALSE"',
        ('            "Target" = "8:' + $exeKey + '"'),
        ('            "Folder" = "8:' + $desktopFolderKey + '"'),
        '            "WorkingFolder" = "8:_8DDE0BA901704C4DA02C0A766095372D"',
        '            "Icon" = "8:"',
        '            "Feature" = "8:"',
        '            }'
    )

    $desktopShortcutBlock = ($desktopShortcutLines -join "`r`n")
    $insertAt = $bounds.CloseBrace
    $text = $text.Substring(0, $insertAt) + "`r`n" + $desktopShortcutBlock + "`r`n" + $text.Substring($insertAt)
}

Set-Content -LiteralPath $VdprojPath -Value $text -Encoding UTF8

Write-Host "Updated $VdprojPath"
Write-Host "- Packaged files now come from publish folder: $PublishBasePathInVdproj"
Write-Host "- Start Menu shortcut target set to: KGV.Wpf.exe ($exeKey)"