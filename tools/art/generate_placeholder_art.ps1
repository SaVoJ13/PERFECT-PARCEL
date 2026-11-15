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

# --- Q-06c: volume helpers (light top-left, dark bottom-right, contact shadow, gloss, thick dark outline) ---
function Shadow($g, $x, $y, $w, $h) { $g.FillEllipse((Brush (C "000000" 70)), $x, $y, $w, $h) }
function Vol($g, $path, $c1, $c2, $line = 5) {
    $rf = $path.GetBounds()
    $lb = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rf, $c1, $c2, [single]60)
    $g.FillPath($lb, $path); $lb.Dispose()
    if ($line -gt 0) { $g.DrawPath((Pen $Espresso $line), $path) }
}
function Gloss($g, $x, $y, $w, $h) { $g.FillEllipse((Brush (C "FFFFFF" 150)), $x, $y, $w, $h) }
function Marks($g, $path, $col, $wd, $n) {   # diagonal grill marks, clipped to the body
    $g.SetClip($path); $p = Pen $col $wd
    for ($k = 0; $k -lt $n; $k++) { $x = 24 + $k * 26; $g.DrawLine($p, $x, 110, ($x + 46), 22) }
    $g.ResetClip()
}

# --- meat on the grill: 4 states, different SHAPES of the badge (colour only reinforces) ---
function Meat-Body($g, $c1, $c2, $marks, $markCol) {
    Shadow $g 14 88 100 22
    $p = RRectPath 14 30 100 64 28; Vol $g $p $c1 $c2 5
    if ($marks -gt 0) { Marks $g $p $markCol 8 $marks; $g.DrawPath((Pen $Espresso 5), $p) }
    Gloss $g 30 38 36 12
}
function Badge-Check($g, $x, $y, $s) {
    Ell $g $x $y $s $s $Mint 4; Gloss $g ($x+$s*0.18) ($y+$s*0.1) ($s*0.4) ($s*0.2)
    $g.DrawLines((Pen $White 7), (Pts @(($x+$s*0.25),($y+$s*0.52),($x+$s*0.43),($y+$s*0.7),($x+$s*0.76),($y+$s*0.32))))
}
function Badge-Warn($g, $x, $y, $s) {
    Poly $g (Pts @(($x+$s/2),$y,($x+$s),($y+$s*0.9),$x,($y+$s*0.9))) $Amber 4
    $g.DrawLine((Pen $Espresso 7), ($x+$s/2), ($y+$s*0.3), ($x+$s/2), ($y+$s*0.6)); $g.FillEllipse((Brush $Espresso), ($x+$s/2-5), ($y+$s*0.68), 10, 10)
}
function Badge-Cross($g, $x, $y, $s) {
    Ell $g $x $y $s $s $Ember 4; $p = Pen $White 7
    $g.DrawLine($p, ($x+$s*0.3), ($y+$s*0.3), ($x+$s*0.7), ($y+$s*0.7)); $g.DrawLine($p, ($x+$s*0.7), ($y+$s*0.3), ($x+$s*0.3), ($y+$s*0.7))
}

# raw: pale, glossy, marbling, no marks
$i = New-Img 128 128; $g = $i.G; Meat-Body $g (C "FFB0A5") (C "D9625A") 0 $Espresso
$g.DrawCurve((Pen (C "FFE3DC" 200) 4), (Pts @(40,70,56,62,72,72,90,64))); Save-Img $i "icon_meat_raw"
# cooking: warmer, 2 pale marks, steam
$i = New-Img 128 128; $g = $i.G; Meat-Body $g (C "E39070") (C "A9553A") 2 (C "7A3A24" 150)
$wp = Pen (C "FFFFFF" 220) 6
foreach ($x in 34, 62, 90) { $g.DrawCurve($wp, (Pts @($x,26,($x+8),17,$x,8,($x+8),0))) }
Save-Img $i "icon_meat_cooking"
# done: golden brown, 3 bold marks, check
$i = New-Img 128 128; $g = $i.G; Meat-Body $g (C "C98545") (C "7A4220") 3 (C "4A2410"); Badge-Check $g 72 62 50; Save-Img $i "icon_meat_done"
# almost burnt: dark, 3 marks, triangle "!" + bar
$i = New-Img 128 128; $g = $i.G; Meat-Body $g (C "8A5230") (C "4A2514") 3 (C "1F100A"); Badge-Warn $g 72 56 52
$g.FillRectangle((Brush $Amber), 14, 108, 70, 8); Save-Img $i "icon_meat_warning"
# burnt: black, cracks and soot, circle with cross
$i = New-Img 128 128; $g = $i.G; Meat-Body $g (C "4A3A33") (C "120A07") 3 (C "000000")
$cp = Pen (C "8A7A70") 3; $g.DrawLines($cp, (Pts @(30,50,40,58,34,68))); $g.DrawLines($cp, (Pts @(54,44,60,54,52,62)))
$g.FillEllipse((Brush (C "000000")), 28, 72, 12, 12); $g.FillEllipse((Brush (C "000000")), 48, 78, 9, 9)
Badge-Cross $g 72 62 50; Save-Img $i "icon_meat_burnt"

# --- ingredients (silhouette + volume + outline, readable in black and white) ---
# pita: domed flatbread with dark pocket opening along the flat edge and toast spots
$i = New-Img 128 128; $g = $i.G; Shadow $g 8 100 112 18
$pp = New-Object System.Drawing.Drawing2D.GraphicsPath; $pp.AddArc(10, 26, 108, 150, 180, 180); $pp.AddLine(118, 101, 10, 101); $pp.CloseFigure()
Vol $g $pp (C "FFE0A0") (C "C98B44") 5
$g.SetClip($pp); $g.FillEllipse((Brush (C "7A4A20")), 22, 84, 84, 34); $g.ResetClip(); $g.DrawPath((Pen $Espresso 5), $pp)
$g.DrawArc((Pen (C "9C5E26" 200) 4), 30, 44, 68, 60, 200, 140)
$g.FillEllipse((Brush (C "B26E2E" 190)), 44, 56, 9, 9); $g.FillEllipse((Brush (C "B26E2E" 190)), 74, 50, 8, 8); $g.FillEllipse((Brush (C "B26E2E" 190)), 86, 68, 7, 7)
Gloss $g 24 38 30 12; Save-Img $i "ing_pita"
# meat: thick slab with 3 grill marks
$i = New-Img 128 128; $g = $i.G; Shadow $g 10 88 108 22
$mp = RRectPath 12 28 104 68 30; Vol $g $mp (C "D9955A") (C "7A4220") 5; Marks $g $mp (C "4A2410") 8 3; $g.DrawPath((Pen $Espresso 5), $mp); Gloss $g 28 36 36 12; Save-Img $i "ing_meat"
# salad: ruffled leaf with veins
$i = New-Img 128 128; $g = $i.G; Shadow $g 24 100 80 16
$sp = Pts @(64,8,80,18,98,26,106,46,112,64,104,84,88,102,64,114,40,102,24,84,16,64,22,46,30,26,48,18)
$lp = New-Object System.Drawing.Drawing2D.GraphicsPath; $lp.AddClosedCurve($sp, [single]0.35)
Vol $g $lp (C "B5EE72") (C "3F8F25") 5
$vp = Pen (C "2E6E18") 4; $g.DrawLine($vp, 64, 24, 64, 104); $g.DrawLine((Pen (C "2E6E18") 3), 64, 56, 90, 40); $g.DrawLine((Pen (C "2E6E18") 3), 64, 74, 38, 58); $g.DrawLine((Pen (C "2E6E18") 3), 64, 88, 88, 74)
Gloss $g 34 30 24 10; Save-Img $i "ing_salad"
# onion: bulb with pointed top, curved layer lines, purple gradient
$i = New-Img 128 128; $g = $i.G; Shadow $g 18 100 92 16
$op = New-Object System.Drawing.Drawing2D.GraphicsPath; $op.AddEllipse(16, 32, 96, 82)
Poly $g (Pts @(64,4,78,40,50,40)) (C "9BD060") 5
Vol $g $op (C "F2CBEF") (C "A55AAE") 5
$g.DrawArc((Pen (C "7E3C88") 3), 34, 44, 60, 62, 100, 160); $g.DrawArc((Pen (C "7E3C88") 3), 50, 44, 28, 62, 90, 180)
Gloss $g 30 44 26 12; Save-Img $i "ing_onion"
# bottles: cap + neck + body + label
function Bottle($g, $body1, $body2, $cap) {
    Shadow $g 30 104 70 14
    $bp = RRectPath 36 40 56 70 16; Vol $g $bp $body1 $body2 5
    $nk = RRectPath 50 16 28 32 6; Vol $g $nk $cap $cap 5
    RRect $g 46 6 36 14 5 $Espresso 0
    Gloss $g 42 46 12 26
}
$i = New-Img 128 128; $g = $i.G   # garlic sauce: cream bottle + garlic clove
Bottle $g (C "FFFFFF") (C "E8D9B5") $Sand
$g.FillPie((Brush (C "FFF8E7")), 52, 60, 24, 34, 0, 360); Poly $g (Pts @(64,56,74,76,70,92,58,92,54,76)) (C "FFFFFF") 3
$g.DrawLine((Pen (C "B59A62") 2), 64, 62, 64, 90); Save-Img $i "ing_sauce_garlic"
$i = New-Img 128 128; $g = $i.G   # hot sauce: red bottle + flame
Bottle $g (C "FF7A2E") (C "B02A0A") $Chili
Poly $g (Pts @(64,56,80,80,72,96,56,96,48,80)) $Saffron 3; Poly $g (Pts @(64,72,70,84,64,92,58,84)) (C "FFF3D6") 0; Save-Img $i "ing_sauce_hot"

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
Portrait "chr_ali" (C "E0A370") (C "D8D8D8") (C "2E9E4F") "ali"

# --- Q-06b: client portraits with volume and 4 expressions (neutral = chr_<id>; happy / worried / angry = chr_<id>_<mood>).
# Moods differ by eyes, brows, mouth and a symbol (sparkle / sweat drop / anger mark), not by colour alone. No stereotypes.
function EllPath($x, $y, $w, $h) { $p = New-Object System.Drawing.Drawing2D.GraphicsPath; $p.AddEllipse([single]$x, [single]$y, [single]$w, [single]$h); return $p }
function Face($g, $mood) {
    $pen = Pen $Espresso 7; $thin = Pen $Espresso 5; $thick = Pen $Espresso 9
    $cheek = if ($mood -eq "happy") { 190 } else { 120 }
    $g.FillEllipse((Brush (C "FF6F61" $cheek)), 78, 128, 26, 16); $g.FillEllipse((Brush (C "FF6F61" $cheek)), 152, 128, 26, 16)
    if ($mood -eq "neutral") {
        $g.FillEllipse((Brush $Espresso), 98, 98, 16, 22); $g.FillEllipse((Brush $Espresso), 142, 98, 16, 22)
        $g.DrawLine($thin, 94, 88, 120, 88); $g.DrawLine($thin, 136, 88, 162, 88)
        $g.DrawArc($pen, 108, 126, 40, 22, 20, 140)
    }
    if ($mood -eq "happy") {
        $g.DrawArc($thick, 94, 100, 28, 24, 200, 140); $g.DrawArc($thick, 134, 100, 28, 24, 200, 140)
        $g.DrawArc($thin, 94, 80, 28, 14, 200, 140); $g.DrawArc($thin, 134, 80, 28, 14, 200, 140)
        $g.FillPie((Brush (C "8B1E2D")), 100, 122, 56, 44, 0, 180); $g.FillPie((Brush (C "FF8A80")), 114, 144, 28, 20, 0, 180)
        $g.FillRectangle((Brush $White), 114, 125, 28, 6); $g.DrawPie($pen, 100, 122, 56, 44, 0, 180)
    }
    if ($mood -eq "worried") {
        Ell $g 92 90 30 34 $White 5; Ell $g 134 90 30 34 $White 5
        $g.FillEllipse((Brush $Espresso), 104, 98, 12, 16); $g.FillEllipse((Brush $Espresso), 140, 98, 12, 16)
        $g.DrawLine($thick, 90, 84, 118, 72); $g.DrawLine($thick, 138, 72, 166, 84)
        $g.DrawLines($pen, (Pts @(104,142, 114,134, 124,142, 134,134, 144,142, 152,136)))
        $drop = Pts @(194,66, 184,86, 186,94, 194,98, 202,94, 204,86)
        Poly $g $drop (C "5CC8FF") 4
    }
    if ($mood -eq "angry") {
        $g.FillEllipse((Brush $White), 94, 98, 26, 20); $g.FillEllipse((Brush $White), 136, 98, 26, 20)
        $g.FillEllipse((Brush $Espresso), 102, 104, 12, 14); $g.FillEllipse((Brush $Espresso), 142, 104, 12, 14)
        $g.DrawLine($thick, 88, 76, 122, 96); $g.DrawLine($thick, 134, 96, 168, 76)
        $g.DrawArc($thick, 106, 138, 44, 24, 200, 140)
        $g.DrawLine((Pen $Ember 8), 180, 50, 200, 70); $g.DrawLine((Pen $Ember 8), 200, 50, 180, 70)
    }
}
function Portrait2($name, $skin, $hairColor, $shirt, $style, $mood) {
    $i = New-Img 256 256; $g = $i.G
    Shadow $g 40 236 176 18
    $b = RRectPath 44 160 168 90 36; Vol $g $b (C "FFFFFF" 255) $shirt 5
    $g.FillPath((Brush (C $shirt.Name.Substring(2) 140)), $b); $g.DrawPath((Pen $Espresso 5), $b)
    if ($style -eq "olya") { foreach ($c in @(@(44,52,64),@(70,26,64),@(122,16,64),@(158,26,64),@(150,52,64),@(26,92,56),@(174,92,56))) { Ell $g $c[0] $c[1] $c[2] $c[2] $hairColor 5 } }
    if ($style -eq "hanna") { Ell $g 92 4 72 64 (C "B0B0B0") 5; Ell $g 54 40 148 140 (C "B0B0B0") 5 }
    $h = EllPath 66 50 124 128; Vol $g $h (C "FFFFFF") $skin 5
    $g.FillPath((Brush (C $skin.Name.Substring(2) 215)), $h); $g.DrawPath((Pen $Espresso 5), $h)
    Gloss $g 84 62 40 14
    if ($mood -eq "angry") { $g.FillPath((Brush (C "E5383B" 80)), $h) }
    if ($style -eq "olya") { Ell $g 62 40 132 52 $hairColor 4; $g.DrawArc((Pen (C "FFB703") 10), 66, 50, 124, 40, 190, 160) }
    if ($style -eq "hanna") { Ell $g 62 44 132 46 (C "B0B0B0") 4; $g.DrawArc((Pen $Mint 16), 70, 138, 116, 62, 20, 140) }
    Face $g $mood
    if ($style -eq "marko") {
        RRect $g 54 28 148 46 22 $Chili 5; RRect $g 132 52 92 20 9 $Chili 4; Gloss $g 78 40 40 12
        $g.DrawEllipse((Pen $Espresso 5), 90, 90, 34, 34); $g.DrawEllipse((Pen $Espresso 5), 132, 90, 34, 34); $g.DrawLine((Pen $Espresso 5), 124, 104, 132, 104)
    }
    if ($style -eq "hanna") { $g.DrawEllipse((Pen $Espresso 4), 92, 92, 30, 30); $g.DrawEllipse((Pen $Espresso 4), 134, 92, 30, 30); $g.DrawLine((Pen $Espresso 4), 122, 104, 134, 104) }
    Save-Img $i $name
}
$clients = @(
    @{ Id = "olya"; Skin = (C "C68642"); Hair = (C "3B2314"); Shirt = (C "4CC9F0") },
    @{ Id = "marko"; Skin = (C "F1C27D"); Hair = (C "6B4630"); Shirt = (C "8E5CF6") },
    @{ Id = "hanna"; Skin = (C "FFDBAC"); Hair = (C "B0B0B0"); Shirt = (C "E5383B") }
)
foreach ($cl in $clients) {
    foreach ($m in @("neutral", "happy", "worried", "angry")) {
        $n = if ($m -eq "neutral") { "chr_" + $cl.Id } else { "chr_" + $cl.Id + "_" + $m }
        Portrait2 $n $cl.Skin $cl.Hair $cl.Shirt $cl.Id $m
    }
}

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
