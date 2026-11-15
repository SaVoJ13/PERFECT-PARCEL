"""Q-06: makes static Nunito Regular (400) and ExtraBold (800) from the official Google Fonts variable file.

Usage: python -I tools/art/make_nunito_static.py <dir with Nunito[wght].ttf> <output dir>
Needs: pip install fonttools (a developer tool, not a project dependency). Source: https://github.com/google/fonts/tree/main/ofl/nunito (OFL 1.1).
"""
import os
import sys

from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

src, out = sys.argv[1], sys.argv[2]
for weight, name in ((400, "Nunito-Regular"), (800, "Nunito-ExtraBold")):
    font = TTFont(os.path.join(src, "Nunito[wght].ttf"))
    instancer.instantiateVariableFont(font, {"wght": weight}, updateFontNames=True).save(os.path.join(out, name + ".ttf"))
