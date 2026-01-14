[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$OutputDir = '',

    [Parameter(Mandatory = $false)]
    [ValidateRange(0.1, 10240)]
    [double]$TargetSizeMB = 10,

    [Parameter(Mandatory = $false)]
    [ValidateSet(
        'all',
        'txt-utf8',
        'txt-utf16',
        'txt-two-tags',
        'txt-malformed',
        'txt-single-record',
        'txt-nested-envelope-basic',
        'txt-nested-envelope-with-noise-outside-records',
        'txt-multi-depth-wrappers-depth3',
        'txt-interleaved-nonrecord-tags-between-records',
        'txt-nested-records-inner-repeats',
        'txt-nested-records-same-tag-name',
        'txt-records-at-mixed-depths',
        'html'
    )]
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

function New-NestedEnvelopeWriter {
    return {
        param(
            [System.IO.StreamWriter]$sw,
            [System.IO.FileStream]$fs,
            [long]$targetBytes,
            [int]$suffixBytes,
            [string]$recordTag,
            [int]$payloadChars,
            [int]$noiseMode
        )

        $payload = ('X' * $payloadChars)
        $i = 0

        while (($fs.Position + $suffixBytes) -lt $targetBytes) {
            $i++

            if ($noiseMode -ge 1) {
                if (($i % 137) -eq 0) {
                    $sw.Write('    <Diagnostics level="info">marker</Diagnostics>' + "`n")
                }
                if (($i % 251) -eq 0) {
                    $sw.Write('    <!-- non-record comment -->' + "`n")
                }
            }

            $sw.Write(('    <{0} id="{1}">{2}</{0}>' -f $recordTag, $i, $payload) + "`n")

            if (($i % 250) -eq 0) {
                $sw.Flush()
            }
        }
    }
}

function New-InterleavedNoiseWriter {
    return {
        param(
            [System.IO.StreamWriter]$sw,
            [System.IO.FileStream]$fs,
            [long]$targetBytes,
            [int]$suffixBytes,
            [string]$recordTag,
            [int]$payloadChars
        )

        $payload = ('N' * $payloadChars)
        $i = 0
        while (($fs.Position + $suffixBytes) -lt $targetBytes) {
            $i++

            if (($i % 3) -eq 0) {
                $sw.Write('    <Spacer/>' + "`n")
            }
            if (($i % 10) -eq 0) {
                $sw.Write(('    <Meta seq="{0}">m</Meta>' -f $i) + "`n")
            }

            $sw.Write(('    <{0} id="{1}">{2}</{0}>' -f $recordTag, $i, $payload) + "`n")

            if (($i % 200) -eq 0) {
                $sw.Flush()
            }
        }
    }
}

function New-NestedInnerRepeatsWriter {
    return {
        param(
            [System.IO.StreamWriter]$sw,
            [System.IO.FileStream]$fs,
            [long]$targetBytes,
            [int]$suffixBytes,
            [string]$recordTag,
            [int]$lineCount,
            [int]$linePayloadChars
        )

        $linePayload = ('L' * $linePayloadChars)

        $i = 0
        while (($fs.Position + $suffixBytes) -lt $targetBytes) {
            $i++

            $sw.Write(('    <{0} id="{1}">' -f $recordTag, $i) + "`n")
            for ($j = 1; $j -le $lineCount; $j++) {
                $sw.Write(('      <Line index="{0}">{1}</Line>' -f $j, $linePayload) + "`n")
            }
            $sw.Write(('    </{0}>' -f $recordTag) + "`n")

            if (($i % 100) -eq 0) {
                $sw.Flush()
            }
        }
    }
}

function New-NestedSameTagNameWriter {
    return {
        param(
            [System.IO.StreamWriter]$sw,
            [System.IO.FileStream]$fs,
            [long]$targetBytes,
            [int]$suffixBytes,
            [string]$tagName,
            [int]$payloadChars
        )

        $payload = ('S' * $payloadChars)

        $i = 0
        while (($fs.Position + $suffixBytes) -lt $targetBytes) {
            $i++
            $sw.Write(('    <{0} id="{1}">' -f $tagName, $i) + "`n")
            $sw.Write(('      <{0} class="inner">{1}</{0}>' -f $tagName, $payload) + "`n")
            if (($i % 4) -eq 0) {
                $sw.Write(('      <{0} class="inner">{1}</{0}>' -f $tagName, $payload) + "`n")
            }
            $sw.Write(('    </{0}>' -f $tagName) + "`n")

            if (($i % 150) -eq 0) {
                $sw.Flush()
            }
        }
    }
}

function New-MixedDepthRecordsWriter {
    return {
        param(
            [System.IO.StreamWriter]$sw,
            [System.IO.FileStream]$fs,
            [long]$targetBytes,
            [int]$suffixBytes,
            [string]$recordTag,
            [int]$payloadChars
        )

        $payload = ('M' * $payloadChars)
        $i = 0
        while (($fs.Position + $suffixBytes) -lt $targetBytes) {
            $i++

            if (($i % 7) -eq 0) {
                $sw.Write('    <Group>' + "`n")
                $sw.Write(('      <{0} id="{1}">{2}</{0}>' -f $recordTag, $i, $payload) + "`n")
                $sw.Write('    </Group>' + "`n")
            }
            elseif (($i % 11) -eq 0) {
                $sw.Write('    <Group><Inner>' + "`n")
                $sw.Write(('      <{0} id="{1}">{2}</{0}>' -f $recordTag, $i, $payload) + "`n")
                $sw.Write('    </Inner></Group>' + "`n")
            }
            else {
                $sw.Write(('    <{0} id="{1}">{2}</{0}>' -f $recordTag, $i, $payload) + "`n")
            }

            if (($i % 200) -eq 0) {
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
    'all' {
        @(
            'txt-utf8',
            'txt-utf16',
            'txt-two-tags',
            'txt-malformed',
            'txt-single-record',
            'txt-nested-envelope-basic',
            'txt-nested-envelope-with-noise-outside-records',
            'txt-multi-depth-wrappers-depth3',
            'txt-interleaved-nonrecord-tags-between-records',
            'txt-nested-records-inner-repeats',
            'txt-nested-records-same-tag-name',
            'txt-records-at-mixed-depths',
            'html'
        )
    }
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

        'txt-nested-envelope-basic' {
            $path = Join-Path $OutputDir ("example-nested-envelope-utf8-{0}mb.txt" -f ([int][Math]::Round($TargetSizeMB)))
            $enc = Get-Encoding 'utf8'
            $record = 'Ficher'
            $prefix = "<Envelope>\n  <Header><Version>1</Version></Header>\n  <Payload>\n"
            $suffix = "  </Payload>\n  <Footer><Checksum>0</Checksum></Footer>\n</Envelope>\n"
            Write-TargetFile -Path $path -Encoding $enc -Prefix $prefix -Suffix $suffix -WriteRecords (New-NestedEnvelopeWriter) -WriteRecordsArgs @($record, 850, 0) -TargetBytes $targetBytes
        }

        'txt-nested-envelope-with-noise-outside-records' {
            $path = Join-Path $OutputDir ("example-nested-envelope-noise-utf8-{0}mb.txt" -f ([int][Math]::Round($TargetSizeMB)))
            $enc = Get-Encoding 'utf8'
            $record = 'Ficher'
            $prefix = "<Envelope>\n  <Header><Version>1</Version></Header>\n  <Payload>\n    <Intro>This is not a record</Intro>\n"
            $suffix = "    <Outro>End</Outro>\n  </Payload>\n  <Footer><Checksum>0</Checksum></Footer>\n</Envelope>\n"
            Write-TargetFile -Path $path -Encoding $enc -Prefix $prefix -Suffix $suffix -WriteRecords (New-NestedEnvelopeWriter) -WriteRecordsArgs @($record, 700, 1) -TargetBytes $targetBytes
        }

        'txt-multi-depth-wrappers-depth3' {
            $path = Join-Path $OutputDir ("example-nested-depth3-utf8-{0}mb.txt" -f ([int][Math]::Round($TargetSizeMB)))
            $enc = Get-Encoding 'utf8'
            $record = 'Ficher'
            $prefix = "<L1>\n  <L2>\n    <L3>\n"
            $suffix = "    </L3>\n  </L2>\n</L1>\n"
            Write-TargetFile -Path $path -Encoding $enc -Prefix $prefix -Suffix $suffix -WriteRecords (New-MarkupRecordWriter) -WriteRecordsArgs @($record, 650, $false) -TargetBytes $targetBytes
        }

        'txt-interleaved-nonrecord-tags-between-records' {
            $path = Join-Path $OutputDir ("example-interleaved-noise-utf8-{0}mb.txt" -f ([int][Math]::Round($TargetSizeMB)))
            $enc = Get-Encoding 'utf8'
            $wrapper = 'Root'
            $record = 'Ficher'
            Write-TargetFile -Path $path -Encoding $enc -Prefix "<$wrapper>`n" -Suffix "</$wrapper>`n" -WriteRecords (New-InterleavedNoiseWriter) -WriteRecordsArgs @($record, 600) -TargetBytes $targetBytes
        }

        'txt-nested-records-inner-repeats' {
            $path = Join-Path $OutputDir ("example-nested-records-lines-utf8-{0}mb.txt" -f ([int][Math]::Round($TargetSizeMB)))
            $enc = Get-Encoding 'utf8'
            $wrapper = 'Root'
            $record = 'Record'
            Write-TargetFile -Path $path -Encoding $enc -Prefix "<$wrapper>`n" -Suffix "</$wrapper>`n" -WriteRecords (New-NestedInnerRepeatsWriter) -WriteRecordsArgs @($record, 5, 180) -TargetBytes $targetBytes
        }

        'txt-nested-records-same-tag-name' {
            $path = Join-Path $OutputDir ("example-nested-same-tag-utf8-{0}mb.txt" -f ([int][Math]::Round($TargetSizeMB)))
            $enc = Get-Encoding 'utf8'
            $wrapper = 'Root'
            $tag = 'Item'
            Write-TargetFile -Path $path -Encoding $enc -Prefix "<$wrapper>`n" -Suffix "</$wrapper>`n" -WriteRecords (New-NestedSameTagNameWriter) -WriteRecordsArgs @($tag, 280) -TargetBytes $targetBytes
        }

        'txt-records-at-mixed-depths' {
            $path = Join-Path $OutputDir ("example-mixed-depth-records-utf8-{0}mb.txt" -f ([int][Math]::Round($TargetSizeMB)))
            $enc = Get-Encoding 'utf8'
            $wrapper = 'Root'
            $record = 'Ficher'
            Write-TargetFile -Path $path -Encoding $enc -Prefix "<$wrapper>`n" -Suffix "</$wrapper>`n" -WriteRecords (New-MixedDepthRecordsWriter) -WriteRecordsArgs @($record, 520) -TargetBytes $targetBytes
        }

        default {
            throw "Unknown variant: $v"
        }
    }
}
