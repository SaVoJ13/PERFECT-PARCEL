# Генератор тимчасових звуків KEBAB STREET (S1-13). PowerShell, без Python і зовнішніх бібліотек.
# WAV пишеться вручну байтами: RIFF/WAVE, PCM, 16 біт, моно, 22050 Гц. Усі звуки <= 1 с, піки <= 0.45 (захист слуху).
# Запуск: powershell -ExecutionPolicy Bypass -File tools/audio/generate_placeholder_sfx.ps1
# Ліцензія результату: Original (власний код проєкту). Детермінований: однакові байти при кожному запуску.
param([string]$OutDir = (Join-Path $PSScriptRoot '..\..\Assets\_Project\Resources\Audio'))

$ErrorActionPreference = 'Stop'
$Rate = 22050
$Peak = 0.45

function Write-Wav([string]$path, [double[]]$samples) {
    $count = $samples.Length
    $dataBytes = $count * 2
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)
    $bw.Write([System.Text.Encoding]::ASCII.GetBytes('RIFF'))
    $bw.Write([int](36 + $dataBytes))
    $bw.Write([System.Text.Encoding]::ASCII.GetBytes('WAVE'))
    $bw.Write([System.Text.Encoding]::ASCII.GetBytes('fmt '))
    $bw.Write([int]16)               # розмір блоку fmt
    $bw.Write([int16]1)              # PCM
    $bw.Write([int16]1)              # моно
    $bw.Write([int]$Rate)            # частота
    $bw.Write([int]($Rate * 2))      # байт/с
    $bw.Write([int16]2)              # вирівнювання блоку
    $bw.Write([int16]16)             # біт на семпл
    $bw.Write([System.Text.Encoding]::ASCII.GetBytes('data'))
    $bw.Write([int]$dataBytes)
    # Нормалізація до $Peak і короткі fade-in/out 5 мс (без клацань).
    $max = 0.0
    foreach ($s in $samples) { if ([Math]::Abs($s) -gt $max) { $max = [Math]::Abs($s) } }
    if ($max -le 0) { $max = 1.0 }
    $fade = [int]($Rate * 0.005)
    for ($i = 0; $i -lt $count; $i++) {
        $v = $samples[$i] / $max * $Peak
        if ($i -lt $fade) { $v = $v * $i / $fade }
        if ($i -ge $count - $fade) { $v = $v * ($count - 1 - $i) / $fade }
        $bw.Write([int16][Math]::Round($v * 32767))
    }
    $bw.Flush()
    [System.IO.File]::WriteAllBytes($path, $ms.ToArray())
    $bw.Dispose()
}

function New-Buffer([double]$seconds) { return , (New-Object 'double[]' ([int]($Rate * $seconds))) }

# Шипіння: детермінований шум (LCG), посилений високі частоти (різниця сусідніх семплів), швидка атака, повільний спад.
function Make-Sizzle {
    $n = [int]($Rate * 0.8)
    $a = New-Object 'double[]' $n
    $state = [long]12345
    $prev = 0.0
    for ($i = 0; $i -lt $n; $i++) {
        $state = (($state * 1103515245) + 12345) -band 0x7FFFFFFF
        $noise = ($state / 1073741823.5) - 1.0
        $hp = $noise - $prev * 0.6
        $prev = $noise
        $t = $i / $Rate
        $env = [Math]::Min(1.0, $t / 0.02) * [Math]::Exp(-3.2 * $t)
        $crackle = 1.0 + 0.5 * [Math]::Sin(2 * [Math]::PI * 23 * $t)
        $a[$i] = $hp * $env * $crackle
    }
    return , $a
}

# Каса: два дзвінкі тони (E6, B6) з гармонікою і експоненційним спадом.
function Make-Cash {
    $n = [int]($Rate * 0.6)
    $a = New-Object 'double[]' $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $Rate
        $v = 0.0
        $f1 = 1318.5
        $v += [Math]::Sin(2 * [Math]::PI * $f1 * $t) * [Math]::Exp(-9 * $t)
        $v += 0.3 * [Math]::Sin(2 * [Math]::PI * $f1 * 2.76 * $t) * [Math]::Exp(-14 * $t)
        if ($t -ge 0.09) {
            $u = $t - 0.09
            $f2 = 1975.5
            $v += [Math]::Sin(2 * [Math]::PI * $f2 * $u) * [Math]::Exp(-8 * $u)
            $v += 0.3 * [Math]::Sin(2 * [Math]::PI * $f2 * 2.76 * $u) * [Math]::Exp(-13 * $u)
        }
        $a[$i] = $v
    }
    return , $a
}

# Радісний: висхідне арпеджіо C5-E5-G5-C6.
function Make-Happy {
    $notes = @(523.25, 659.25, 783.99, 1046.5)
    $step = 0.1
    $n = [int]($Rate * 0.55)
    $a = New-Object 'double[]' $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $Rate
        $k = [Math]::Min([int][Math]::Floor($t / $step), $notes.Length - 1)
        $u = $t - $k * $step
        $f = $notes[$k]
        $env = [Math]::Min(1.0, $u / 0.008) * [Math]::Exp(-5 * $u)
        $a[$i] = ([Math]::Sin(2 * [Math]::PI * $f * $t) + 0.25 * [Math]::Sin(2 * [Math]::PI * 2 * $f * $t)) * $env
    }
    return , $a
}

# Сумний: спадний глісандо 440 -> 200 Гц із легким вібрато (інтегрована фаза).
function Make-Sad {
    $n = [int]($Rate * 0.7)
    $a = New-Object 'double[]' $n
    $phase = 0.0
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $Rate
        $f = 440 - (240 * $t / 0.7) + 6 * [Math]::Sin(2 * [Math]::PI * 6 * $t)
        $phase += 2 * [Math]::PI * $f / $Rate
        $env = [Math]::Min(1.0, $t / 0.02) * [Math]::Max(0.0, 1.0 - $t / 0.7)
        $a[$i] = ([Math]::Sin($phase) + 0.3 * [Math]::Sin(2 * $phase)) * $env
    }
    return , $a
}

# Пригоріле: два різких прямокутних сигнали 880 Гц (тривога), тихіше за пік.
function Make-Burnt {
    $n = [int]($Rate * 0.5)
    $a = New-Object 'double[]' $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $Rate
        $cycle = $t % 0.22
        if ($t -lt 0.44 -and $cycle -lt 0.14) {
            $sq = if ([Math]::Sin(2 * [Math]::PI * 880 * $t) -ge 0) { 1.0 } else { -1.0 }
            $a[$i] = $sq * 0.6
        }
    }
    return , $a
}

# Тап кнопки: короткий «клік» 700 Гц, 60 мс.
function Make-Tap {
    $n = [int]($Rate * 0.06)
    $a = New-Object 'double[]' $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $Rate
        $a[$i] = [Math]::Sin(2 * [Math]::PI * 700 * $t) * [Math]::Exp(-60 * $t)
    }
    return , $a
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path
Write-Wav (Join-Path $OutDir 'sfx_sizzle.wav') (Make-Sizzle)
Write-Wav (Join-Path $OutDir 'sfx_cash.wav') (Make-Cash)
Write-Wav (Join-Path $OutDir 'sfx_happy.wav') (Make-Happy)
Write-Wav (Join-Path $OutDir 'sfx_sad.wav') (Make-Sad)
Write-Wav (Join-Path $OutDir 'sfx_burnt.wav') (Make-Burnt)
Write-Wav (Join-Path $OutDir 'sfx_tap.wav') (Make-Tap)
Get-ChildItem $OutDir -Filter *.wav | ForEach-Object { '{0} {1} bytes' -f $_.Name, $_.Length }
