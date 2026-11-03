#!/bin/bash
# renders the SVG icons to the PNGs the game loads, at twice the size they are drawn
cd "$(dirname "$0")"
out=../Common/Textures/UI/JustTerminal
mkdir -p "$out" ../Common/Textures/UI/JustDesktop/Tabs
for f in *.svg; do
  rsvg-convert -w 40 -h 40 "$f" -o "$out/${f%.svg}.png"
done
# JustDesktop looks for a line icon for each bottom bar button here
cp "$out/Terminal.png" ../Common/Textures/UI/JustDesktop/Tabs/JTM_Terminal.png
