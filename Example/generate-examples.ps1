[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$OutputDir = '',

    [Parameter(Mandatory = $false)]
    [ValidateRange(0.1, 10240)]
    [double]$TargetSizeMB = 10,

    [Parameter(Mandatory = $false)]
    [ValidateSet('all','txt-utf8','txt-utf16','txt-two-tags','txt-malformed','txt-single-record','html')]
    [string]$Variant = 'all'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $OutputDir = Join-Path $scriptDir 'generated'
}

function Get-Encoding([string]$name) {
    switch ($name.ToLowerInvariant()) {
        'utf8' { return [System.Text.UTF8Encoding]::new($false) }
        'utf8-bom' { return [System.Text.UTF8Encoding]::new($true) }
        'utf16' { return [System.Text.UnicodeEncoding]::new($false, $true) }     # LE + BOM
        'utf16le' { return [System.Text.UnicodeEncoding]::new($false, $true) }
        'utf16be' { return [System.Text.UnicodeEncoding]::new($true, $true) }    # BE + BOM
        default { throw "Unknown encoding: $name" }
    }
}

function Ensure-Dir([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) {
        New-Item -ItemType Directory -Path $path | Out-Null
    }
}

function Write-TargetFile {
    param(
        [Parameter(Mandatory=$true)][string]$Path,
        [Parameter(Mandatory=$true)][System.Text.Encoding]$Encoding,
        [Parameter(Mandatory=$true)][string]$Prefix,
        [Parameter(Mandatory=$true)][string]$Suffix,
        [Parameter(Mandatory=$true)][scriptblock]$WriteRecords,
        [Parameter(Mandatory=$false)][object[]]$WriteRecordsArgs = @(),
        [Parameter(Mandatory=$true)][long]$TargetBytes
    )

    Ensure-Dir (Split-Path -Parent $Path)

    $fs = [System.IO.FileStream]::new($Path, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
    try {
        $sw = [System.IO.StreamWriter]::new($fs, $Encoding)
        try {
            $sw.NewLine = "`n"

            $suffixBytes = $Encoding.GetByteCount($Suffix)

            $sw.Write($Prefix)
            $sw.Flush()

            & $WriteRecords $sw $fs $TargetBytes $suffixBytes @WriteRecordsArgs

            $sw.Write($Suffix)
            $sw.Flush()
        }
        finally {
            $sw.Dispose()
        }
    }
    finally {
        $fs.Dispose()
    }

    $finalSize = (Get-Item -LiteralPath $Path).Length
    Write-Host ("Generated: {0} ({1:N0} bytes)" -f $Path, $finalSize)
}

function New-MarkupRecordWriter {
    return {
        param(
            [System.IO.StreamWriter]$sw,
            [System.IO.FileStream]$fs,
            [long]$targetBytes,
            [int]$suffixBytes,
            [string]$recordTag,
            [int]$payloadChars,
            [bool]$emitMalformed
        )

        $payload = ('X' * $payloadChars)

        $i = 0
        while (($fs.Position + $suffixBytes) -lt $targetBytes) {
            $i++

            # Simple pseudo-XML record: <Tag id="N">payload</Tag>
            if ($emitMalformed -and ($i % 50 -eq 0)) {
                # Intentionally omit close tag occasionally.
                $sw.Write(('  <{0} id="{1}">{2}' -f $recordTag, $i, $payload) + "`n")
            }
            else {
                $sw.Write(('  <{0} id="{1}">{2}</{0}>' -f $recordTag, $i, $payload) + "`n")
            }

            if (($i % 250) -eq 0) {
                $sw.Flush()
            }
        }
    }
}

function New-TwoTagsRecordWriter {
    return {
        param(
            [System.IO.StreamWriter]$sw,
            [System.IO.FileStream]$fs,
            [long]$targetBytes,
            [int]$suffixBytes,
            [string]$tagA,
            [string]$tagB,
            [int]$payloadCharsA,
            [int]$payloadCharsB
        )

        $payloadA = ('A' * $payloadCharsA)
        $payloadB = ('B' * $payloadCharsB)

        $i = 0
        while (($fs.Position + $suffixBytes) -lt $targetBytes) {
            $i++

            # Heavily favor A so auto-detect likely selects A,
            # but allow users to override to B for manual mode testing.
            $sw.Write(("  <{0}>{1}</{0}>" -f $tagA, $payloadA) + "`n")
            $sw.Write(("  <{0}>{1}</{0}>" -f $tagA, $payloadA) + "`n")
            $sw.Write(("  <{0}>{1}</{0}>" -f $tagA, $payloadA) + "`n")

            if (($i % 3) -eq 0) {
                $sw.Write(("  <{0}>{1}</{0}>" -f $tagB, $payloadB) + "`n")
            }

            if (($i % 100) -eq 0) {
                $sw.Flush()
            }
        }
    }
}

function New-SingleHugeRecordWriter {
    return {
        param(
            [System.IO.StreamWriter]$sw,
            [System.IO.FileStream]$fs,
            [long]$targetBytes,
            [int]$suffixBytes,
            [string]$recordTag
        )

        # One record that (likely) exceeds common chunk sizes to trigger the
        # "single record larger than target" behavior.
        $remaining = [Math]::Max(0, $targetBytes - $fs.Position - $suffixBytes)

        # StreamWriter uses chars; to keep it simple, just overshoot with a large char payload.
        # For UTF-8, this is ~1 byte/char; for UTF-16, ~2 bytes/char.
        $payloadChars = [Math]::Max(1024, [int]($remaining / 2))
        $payload = ('Z' * $payloadChars)

        $sw.Write(('  <{0} id="1">{1}</{0}>' -f $recordTag, $payload) + "`n")
        $sw.Flush()
    }
}

function New-HtmlWriter {
    return {
        param(
            [System.IO.StreamWriter]$sw,
            [System.IO.FileStream]$fs,
            [long]$targetBytes,
            [int]$suffixBytes,
            [int]$payloadChars
        )

        $payload = ('lorem ipsum ' * [Math]::Ceiling($payloadChars / 12))

        $i = 0
        while (($fs.Position + $suffixBytes) -lt $targetBytes) {
            $i++
            $sw.Write('<div class="page">' + "`n")
            $sw.Write(("  <h2>Page {0}</h2>" -f $i) + "`n")
            $sw.Write(("  <p>{0}</p>" -f $payload) + "`n")
            $sw.Write('</div>' + "`n")

            if (($i % 100) -eq 0) {
                $sw.Flush()
            }
        }
    }
}

Ensure-Dir $OutputDir
$targetBytes = [long]([Math]::Round($TargetSizeMB * 1024 * 1024))

Write-Host "OutputDir: $OutputDir"
Write-Host ("TargetSize: {0} MB ({1:N0} bytes)" -f $TargetSizeMB, $targetBytes)
Write-Host "Variant: $Variant"

$variantsToRun = switch ($Variant) {
    'all' { @('txt-utf8','txt-utf16','txt-two-tags','txt-malformed','txt-single-record','html') }
    default { @($Variant) }
}

foreach ($v in $variantsToRun) {
    switch ($v) {
        'txt-utf8' {
            $path = Join-Path $OutputDir ("example-markup-utf8-{0}mb.txt" -f ([int][Math]::Round($TargetSizeMB)))
            $enc = Get-Encoding 'utf8'
            $wrapper = 'Example'
            $record = 'Ficher'
            Write-TargetFile -Path $path -Encoding $enc -Prefix "<$wrapper>`n" -Suffix "</$wrapper>`n" -WriteRecords (New-MarkupRecordWriter) -WriteRecordsArgs @($record, 900, $false) -TargetBytes $targetBytes
        }

        'txt-utf16' {
            $path = Join-Path $OutputDir ("example-markup-utf16le-bom-{0}mb.txt" -f ([int][Math]::Round($TargetSizeMB)))
            $enc = Get-Encoding 'utf16'
            $wrapper = 'Root'
            $record = 'Item'
            Write-TargetFile -Path $path -Encoding $enc -Prefix "<$wrapper>`n" -Suffix "</$wrapper>`n" -WriteRecords (New-MarkupRecordWriter) -WriteRecordsArgs @($record, 700, $false) -TargetBytes $targetBytes
        }

        'txt-two-tags' {
            $path = Join-Path $OutputDir ("example-two-tags-utf8-{0}mb.txt" -f ([int][Math]::Round($TargetSizeMB)))
            $enc = Get-Encoding 'utf8'
            $wrapper = 'Root'
            Write-TargetFile -Path $path -Encoding $enc -Prefix "<$wrapper>`n" -Suffix "</$wrapper>`n" -WriteRecords (New-TwoTagsRecordWriter) -WriteRecordsArgs @('A', 'B', 600, 250) -TargetBytes $targetBytes
        }

        'txt-malformed' {
            $path = Join-Path $OutputDir ("example-malformed-utf8-{0}mb.txt" -f ([int][Math]::Round($TargetSizeMB)))
            $enc = Get-Encoding 'utf8'
            $wrapper = 'Example'
            $record = 'Ficher'
            Write-TargetFile -Path $path -Encoding $enc -Prefix "<$wrapper>`n" -Suffix "</$wrapper>`n" -WriteRecords (New-MarkupRecordWriter) -WriteRecordsArgs @($record, 600, $true) -TargetBytes $targetBytes
        }

        'txt-single-record' {
            $path = Join-Path $OutputDir ("example-single-record-utf8-{0}mb.txt" -f ([int][Math]::Round($TargetSizeMB)))
            $enc = Get-Encoding 'utf8'
            $wrapper = 'Example'
            $record = 'BigRecord'
            Write-TargetFile -Path $path -Encoding $enc -Prefix "<$wrapper>`n" -Suffix "</$wrapper>`n" -WriteRecords (New-SingleHugeRecordWriter) -WriteRecordsArgs @($record) -TargetBytes $targetBytes
        }

        'html' {
            $path = Join-Path $OutputDir ("example-html-utf8-{0}mb.html" -f ([int][Math]::Round($TargetSizeMB)))
            $enc = Get-Encoding 'utf8'
            $prefix = '<!doctype html>' + "`n" + '<html><head><meta charset="utf-8" /></head><body>' + "`n"
            $suffix = '</body></html>' + "`n"
            Write-TargetFile -Path $path -Encoding $enc -Prefix $prefix -Suffix $suffix -WriteRecords (New-HtmlWriter) -WriteRecordsArgs @(1200) -TargetBytes $targetBytes
        }

        default {
            throw "Unknown variant: $v"
        }
    }
}
