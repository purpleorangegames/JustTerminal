# pawns: everyone with mood, health, food and what they're doing

out dim {pad("NAME", 14)}
out dim {pad("AGE", 5)}
out dim {pad("MOOD", 13)}
out dim {pad("HEALTH", 13)}
out dim {pad("FOOD", 6)}
print dim DOING

n = 0
for p in /pawns
  out bright {pad(p/name, 14)}
  out {pad(p/age, 5)}
  # some pawns have no mood, those get an empty bar
  mood = p/mood
  if mood = none
    mood = 0
  end
  bar mood 10
  out {"   "}
  bar p/health 10
  out {"   "}
  # the food number, so the hungriest shows without reading each pawn
  if p/food = none
    out {pad("", 6)}
  else
    out {pad(p/food + "%", 6)}
  end
  if p/mental != none
    print bad {p/doing}
  else
    print {p/doing}
  end
  n = n + 1
end
print dim {n} colonists
