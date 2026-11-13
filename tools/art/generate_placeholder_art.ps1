# S1-12: generates KEBAB STREET placeholder art with our own code (System.Drawing). Run (Windows PowerShell):
#   powershell -ExecutionPolicy Bypass -File tools/art/generate_placeholder_art.ps1
# Every file is original (made by this script), no third-party imagery. Registry: docs/art/ASSET_REGISTRY.md.
param([string]$OutDir = (Join-Path $PSScriptRoot "..\..\Assets\_Project\Resources\Art"))
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path

function C($hex, $a = 255) { $h = $hex.TrimStart('#'); [System.Drawing.Color]::FromArgb($a, [Convert]::ToInt32($h.Substring(0,2),16), [Convert]::ToInt32($h.Substring(2,2),16), [Convert]::ToInt32($h.Substring(4,2),16)) }
$Espresso = C "3B2314"; $Cream = C "FFF3D6"; $Sand = C "F7E2B0"; $Chili = C "C2410C"; $Saffron = C "FFB703"
$Mint = C "2E9E4F"; $Amber = C "F59E0B"; $Ember = C "D62828"; $White = C "FFFFFF"

function New-Img($w, $h) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    return @{ Bmp = $bmp; G = $g }
}
function Save-Img($img, $name) {
    $img.G.Dispose()
    $img.Bmp.Save((Join-Path $OutDir ($name + ".png")), [System.Drawing.Imaging.ImageFormat]::Png)
    $img.Bmp.Dispose()
}
function Brush($c) { New-Object System.Drawing.SolidBrush($c) }
function Pen($c, $w) { $p = New-Object System.Drawing.Pen($c, $w); $p.LineJoin = 'Round'; $p.StartCap = 'Round'; $p.EndCap = 'Round'; $p }
function Pts($arr) { $l = New-Object 'System.Collections.Generic.List[System.Drawing.PointF]'; for ($i = 0; $i -lt $arr.Count; $i += 2) { $l.Add((New-Object System.Drawing.PointF([single]$arr[$i], [single]$arr[$i+1]))) }; return ,($l.ToArray()) }
function Ell($g, $x, $y, $w, $h, $fill, $line = 4) { $g.FillEllipse((Brush $fill), $x, $y, $w, $h); if ($line -gt 0) { $g.DrawEllipse((Pen $Espresso $line), $x, $y, $w, $h) } }
function Poly($g, $pts, $fill, $line = 4) { $g.FillPolygon((Brush $fill), $pts); if ($line -gt 0) { $g.DrawPolygon((Pen $Espresso $line), $pts) } }
function RRectPath($x, $y, $w, $h, $r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath; $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90); $p.CloseFigure(); return $p
}
function RRect($g, $x, $y, $w, $h, $r, $fill, $line = 4) { $p = RRectPath $x $y $w $h $r; $g.FillPath((Brush $fill), $p); if ($line -gt 0) { $g.DrawPath((Pen $Espresso $line), $p) } }
function Shine($g, $x, $y, $w, $h) { $g.FillEllipse((Brush (C "FFFFFF" 110)), $x, $y, $w, $h) }

# --- meat on the grill: 4 states, different SHAPES of the badge (colour only reinforces) ---
function Meat-Blob($g, $fill) { RRect $g 14 34 100 62 28 $fill 5; Shine $g 30 42 34 14 }
function Badge-Check($g, $x, $y, $s) {
    Ell $g $x $y $s $s $Mint 4
    $g.DrawLines((Pen $White 6), (Pts @(($x+$s*0.25),($y+$s*0.52),($x+$s*0.43),($y+$s*0.7),($x+$s*0.76),($y+$s*0.32))))
}
function Badge-Warn($g, $x, $y, $s) {
    Poly $g (Pts @(($x+$s/2),$y,($x+$s),($y+$s*0.9),$x,($y+$s*0.9))) $Amber 4
    $g.DrawLine((Pen $Espresso 6), ($x+$s/2), ($y+$s*0.3), ($x+$s/2), ($y+$s*0.6)); $g.FillEllipse((Brush $Espresso), ($x+$s/2-4), ($y+$s*0.68), 8, 8)
}
function Badge-Cross($g, $x, $y, $s) {
    Ell $g $x $y $s $s $Ember 4; $p = Pen $White 6
    $g.DrawLine($p, ($x+$s*0.3), ($y+$s*0.3), ($x+$s*0.7), ($y+$s*0.7)); $g.DrawLine($p, ($x+$s*0.7), ($y+$s*0.3), ($x+$s*0.3), ($y+$s*0.7))
}

$i = New-Img 128 128; Meat-Blob $i.G (C "E8837A"); Save-Img $i "icon_meat_raw"
# cooking: darker meat + wavy steam lines (shape: steam)
$i = New-Img 128 128; Meat-Blob $i.G (C "C46A4A")
$wp = Pen (C "FFFFFF" 200) 5
foreach ($x in 34, 62, 90) { $i.G.DrawCurve($wp, (Pts @($x,30,($x+8),20,$x,10,($x+8),0))) }
Save-Img $i "icon_meat_cooking"
# done: brown-gold + circle with check
$i = New-Img 128 128; Meat-Blob $i.G (C "9C5A2B"); Badge-Check $i.G 72 62 50; Save-Img $i "icon_meat_done"
# almost burnt: darker + triangle "!" + thin bar
$i = New-Img 128 128; Meat-Blob $i.G (C "7A4220"); Badge-Warn $i.G 72 56 52
$i.G.FillRectangle((Brush $Amber), 14, 108, 70, 8); Save-Img $i "icon_meat_warning"
# burnt: almost black + soot spots + circle with cross
$i = New-Img 128 128; Meat-Blob $i.G (C "2A1A12")
$i.G.FillEllipse((Brush (C "000000")), 28, 60, 12, 12); $i.G.FillEllipse((Brush (C "000000")), 48, 74, 9, 9)
Badge-Cross $i.G 72 62 50; Save-Img $i "icon_meat_burnt"

# --- ingredients (silhouette + outline, readable in black and white) ---
$i = New-Img 128 128; $g = $i.G   # pita: half-round flatbread with pocket
$g.FillPie((Brush (C "E9B872")), 8, 28, 112, 112, 180, 180); $g.DrawPie((Pen $Espresso 4), 8, 28, 112, 112, 180, 180)
$g.DrawArc((Pen (C "B98544") 4), 28, 48, 72, 72, 190, 160); Save-Img $i "ing_pita"
$i = New-Img 128 128; $g = $i.G   # meat slab with highlight
RRect $g 12 30 104 68 30 (C "9C5A2B") 4; Shine $g 28 40 36 14
$sp = Pen (C "6B3A18") 4; $g.DrawLine($sp, 40, 70, 56, 82); $g.DrawLine($sp, 66, 62, 82, 76); Save-Img $i "ing_meat"
$i = New-Img 128 128; $g = $i.G   # salad: leaf with veins
Poly $g (Pts @(64,10,100,40,108,84,64,118,20,84,28,40)) (C "6CBF3C") 4
$g.DrawLine((Pen (C "3E8A1F") 4), 64, 24, 64, 108); $g.DrawLine((Pen (C "3E8A1F") 3), 64, 60, 88, 46); $g.DrawLine((Pen (C "3E8A1F") 3), 64, 76, 40, 62); Save-Img $i "ing_salad"
$i = New-Img 128 128; $g = $i.G   # onion: round with rings and a tip
Ell $g 18 30 92 88 (C "D9A6E0") 4; $g.DrawEllipse((Pen (C "A66BB0") 3), 38, 48, 52, 52)
Poly $g (Pts @(64,6,76,34,52,34)) (C "6CBF3C") 4; Save-Img $i "ing_onion"
$i = New-Img 128 128; $g = $i.G   # garlic sauce: bottle + drop, cream
RRect $g 40 40 48 76 14 (C "FFF8E7") 4; RRect $g 52 14 24 30 6 $Sand 4
$g.FillPolygon((Brush $Espresso), (Pts @(64,60,54,82,74,82))); Save-Img $i "ing_sauce_garlic"
$i = New-Img 128 128; $g = $i.G   # hot sauce: bottle + flame (different badge shape)
RRect $g 40 40 48 76 14 (C "E85D04") 4; RRect $g 52 14 24 30 6 $Chili 4
Poly $g (Pts @(64,56,80,82,72,96,56,96,48,82)) $Saffron 3; Save-Img $i "ing_sauce_hot"

# --- UI icons ---
function Heart-Path($x, $y, $s) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc($x, $y, $s/2, $s/2, 180, 180); $p.AddArc($x + $s/2, $y, $s/2, $s/2, 180, 180)
    $p.AddLine($x + $s, $y + $s*0.28, $x + $s/2, $y + $s*0.95); $p.AddLine($x + $s/2, $y + $s*0.95, $x, $y + $s*0.28); $p.CloseFigure(); return $p
}
$i = New-Img 96 96; $p = Heart-Path 8 12 80; $i.G.FillPath((Brush (C "D62828")), $p); $i.G.DrawPath((Pen $Espresso 4), $p); Shine $i.G 22 22 18 10; Save-Img $i "icon_heart_full"
$i = New-Img 96 96; $p = Heart-Path 8 12 80; $i.G.FillPath((Brush (C "F7E2B0" 120)), $p); $i.G.DrawPath((Pen $Espresso 4), $p); Save-Img $i "icon_heart_empty"
$i = New-Img 96 96; Ell $i.G 8 8 80 80 (C "FFC933") 4; Ell $i.G 22 22 52 52 (C "FFC933") 3; $i.G.DrawLine((Pen $Espresso 5), 48, 34, 48, 62); Save-Img $i "icon_coin"
function Star-Pts($cx, $cy, $outer, $inner) { $l = @(); for ($k = 0; $k -lt 10; $k++) { $rad = if ($k % 2 -eq 0) { $outer } else { $inner }; $a = -[Math]::PI/2 + $k * [Math]::PI / 5; $l += ($cx + $rad*[Math]::Cos($a)); $l += ($cy + $rad*[Math]::Sin($a)) }; return (Pts $l) }
$i = New-Img 96 96; Poly $i.G (Star-Pts 48 50 42 18) (C "FFC933") 4; Save-Img $i "icon_star_full"
$i = New-Img 96 96; Poly $i.G (Star-Pts 48 50 42 18) (C "F7E2B0" 120) 4; Save-Img $i "icon_star_empty"
$i = New-Img 96 96; $g = $i.G   # lock
$g.DrawArc((Pen $Espresso 9), 26, 8, 44, 48, 180, 180); RRect $g 18 40 60 46 10 $Saffron 4; Ell $g 42 56 12 12 $Espresso 0; Save-Img $i "icon_lock"
$i = New-Img 96 96; $g = $i.G   # pause
RRect $g 6 6 84 84 20 $Sand 4; $g.FillRectangle((Brush $Espresso), 30, 26, 11, 44); $g.FillRectangle((Brush $Espresso), 55, 26, 11, 44); Save-Img $i "icon_pause"
$i = New-Img 96 96; $g = $i.G   # menu: three bars
RRect $g 6 6 84 84 20 $Sand 4; foreach ($y in 28, 44, 60) { $g.FillRectangle((Brush $Espresso), 24, $y, 48, 8) }; Save-Img $i "icon_menu"

# --- portraits (256x256): different silhouettes and accessories, friendly, no stereotypes ---
function Portrait($name, $skin, $hairColor, $shirt, $style) {
    $i = New-Img 256 256; $g = $i.G
    Ell $g 48 150 160 120 $shirt 5
    if ($style -eq "olya") { Ell $g 40 30 176 150 $hairColor 5 }
    if ($style -eq "hanna") { Ell $g 60 40 136 130 $hairColor 5 }
    Ell $g 66 50 124 128 $skin 5
    if ($style -eq "marko") { RRect $g 56 36 144 40 20 $Chili 5; RRect $g 130 56 90 18 8 $Chili 4 }
    if ($style -eq "olya") { Ell $g 66 36 124 60 $hairColor 4 }
    if ($style -eq "hanna") { $g.DrawArc((Pen $Mint 14), 70, 128, 116, 70, 20, 140) }
    if ($style -eq "ali") { Ell $g 60 40 136 60 (C "D8D8D8") 4 }
    $g.FillEllipse((Brush $Espresso), 98, 98, 16, 22); $g.FillEllipse((Brush $Espresso), 142, 98, 16, 22)
    $g.DrawArc((Pen $Espresso 6), 100, 120, 56, 36, 20, 140)
    $g.FillEllipse((Brush (C "FF9A8A" 140)), 82, 128, 22, 14); $g.FillEllipse((Brush (C "FF9A8A" 140)), 152, 128, 22, 14)
    if ($style -eq "ali") {
        $g.FillPie((Brush (C "D8D8D8")), 92, 122, 72, 40, 0, 180); $g.DrawPie((Pen $Espresso 4), 92, 122, 72, 40, 0, 180)
        $g.DrawEllipse((Pen $Espresso 5), 88, 90, 36, 34); $g.DrawEllipse((Pen $Espresso 5), 132, 90, 36, 34); $g.DrawLine((Pen $Espresso 5), 124, 104, 132, 104)
        RRect $g 78 190 100 66 18 $White 5; Ell $g 96 208 18 14 $Chili 0; Ell $g 140 222 14 12 (C "E85D04") 0
    }
    if ($style -eq "marko") { $g.DrawEllipse((Pen $Espresso 5), 92, 92, 30, 28); $g.DrawEllipse((Pen $Espresso 5), 134, 92, 30, 28) }
    Save-Img $i $name
}
Portrait "chr_olya" (C "C68642") (C "3B2314") (C "4CC9F0") "olya"
Portrait "chr_marko" (C "F1C27D") (C "6B4630") (C "8E5CF6") "marko"
Portrait "chr_hanna" (C "FFDBAC") (C "B0B0B0") (C "E5383B") "hanna"
Portrait "chr_ali" (C "E0A370") (C "D8D8D8") (C "2E9E4F") "ali"

# --- bar background 540x960: wall, awning, wooden counter ---
$i = New-Img 540 960; $g = $i.G
$lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle 0,0,540,960), (C "6B3A2A"), (C "3B2314"), 90)
$g.FillRectangle($lg, 0, 0, 540, 960)
for ($k = 0; $k -lt 9; $k++) { $col = if ($k % 2 -eq 0) { $Chili } else { $Cream }; $g.FillRectangle((Brush $col), $k * 60, 0, 60, 84) }
for ($k = 0; $k -lt 9; $k++) { $col = if ($k % 2 -eq 0) { $Chili } else { $Cream }; $g.FillPie((Brush $col), $k * 60, 54, 60, 60, 0, 180) }
RRect $g 30 180 120 150 14 (C "F59E0B" 90) 4; RRect $g 390 180 120 150 14 (C "F59E0B" 90) 4
$g.FillRectangle((Brush (C "7A4A24")), 0, 560, 540, 400); $g.FillRectangle((Brush $Espresso), 0, 556, 540, 8)
for ($k = 0; $k -lt 10; $k++) { $g.DrawLine((Pen (C "5E3818") 3), 0, 600 + $k*40, 540, 600 + $k*40) }
Save-Img $i "bg_bar"
Write-Host "Generated in $OutDir"
